using GlobalMeetingApi.Application.Ports;
using GlobalMeetingApi.Domain;

namespace GlobalMeetingApi.Application;

/// <summary>
/// 지역 입력 검증, 시간대 해석, 미래 경계, UTC 충돌 저장을 한 유스케이스로 조율합니다.
/// HTTP와 저장 기술을 직접 알지 않는 Application Service라서 업무 순서를 한곳에서 읽고 Port별 Test Double로 검증할 수 있습니다.
/// </summary>
/// <param name="localTimeResolver">지역 벽시계를 UTC로 바꾸는 기술을 숨긴 Port입니다.</param>
/// <param name="meetingRepository">구체 DB와 메모리 구현을 숨긴 저장 Port입니다.</param>
/// <param name="timeProvider">현재 시각을 운영과 테스트에서 교체하게 하는 시간 의존성입니다.</param>
// class 이름 옆 매개변수는 primary constructor입니다. DI가 세 의존성을 주입해 Service 내부의 new 결합을 없앱니다.
public sealed class ScheduleMeetingService(
    ILocalTimeResolver localTimeResolver,
    IMeetingRepository meetingRepository,
    TimeProvider timeProvider)
{
    /// <summary>
    /// 회의 명령을 UTC 예약으로 바꾸고 충돌 없이 한 번 저장합니다.
    /// </summary>
    /// <param name="command">Presentation이 만든 nullable 외부 입력 명령입니다.</param>
    /// <param name="cancellationToken">HTTP 연결 종료 등을 저장소까지 전달하는 취소 신호입니다.</param>
    /// <returns>저장된 예약 또는 사용자가 수정할 수 있는 예상 오류를 반환합니다.</returns>
    // async는 await로 비동기 저장을 기다릴 수 있게 하며, 호출 thread를 막지 않는 ValueTask 결과를 만듭니다.
    public async ValueTask<Result<MeetingReservation>> ScheduleAsync(
        ScheduleMeetingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        // var는 오른쪽 Result<LocalMeetingTime> 형식을 compiler가 정확히 추론합니다.
        var localResult = LocalMeetingTime.Create(
            command.LocalDate,
            command.LocalStartTime,
            command.TimeZoneId,
            command.PreferredUtcOffset);
        if (!localResult.IsSuccess)
        {
            return Result<MeetingReservation>.Failure(localResult.Error);
        }

        var resolvedResult = localTimeResolver.Resolve(localResult.Value);
        if (!resolvedResult.IsSuccess)
        {
            return Result<MeetingReservation>.Failure(resolvedResult.Error);
        }

        var slotResult = MeetingSlot.Create(
            resolvedResult.Value.UtcInstant,
            command.DurationMinutes);
        if (!slotResult.IsSuccess)
        {
            return Result<MeetingReservation>.Failure(slotResult.Error);
        }

        var now = timeProvider.GetUtcNow();
        if (slotResult.Value.StartsAtUtc < now.AddMinutes(30))
        {
            return Result<MeetingReservation>.Failure(
                new Error(
                    "meeting.too_soon",
                    "회의는 현재 UTC 시각보다 최소 30분 뒤에 시작해야 합니다.",
                    "localStartTime"));
        }

        var meetingResult = MeetingReservation.Create(
            command.MeetingId,
            command.OrganizerId,
            command.Title,
            localResult.Value,
            resolvedResult.Value,
            slotResult.Value);
        if (!meetingResult.IsSuccess)
        {
            return meetingResult;
        }

        // await는 thread를 붙잡지 않고 Repository의 비동기 완료를 기다립니다.
        var writeStatus = await meetingRepository.TryAddAsync(
            meetingResult.Value,
            cancellationToken);
        // switch 식과 각 => arm은 저장 상태 하나를 대응하는 Result 하나로 빠짐없이 변환합니다.
        return writeStatus switch
        {
            MeetingWriteStatus.Saved => meetingResult,
            MeetingWriteStatus.DuplicateMeetingId => Result<MeetingReservation>.Failure(
                new Error(
                    "meeting_id.duplicate",
                    "이미 사용된 meetingId입니다.",
                    "meetingId")),
            MeetingWriteStatus.OrganizerTimeConflict => Result<MeetingReservation>.Failure(
                new Error(
                    "organizer.time_conflict",
                    "같은 주최자의 기존 회의와 UTC 시간이 겹칩니다.",
                    "localStartTime")),
            _ => throw new InvalidOperationException($"알 수 없는 저장 상태: {writeStatus}")
        };
    }

    /// <summary>
    /// 예약 ID로 저장된 회의를 조회합니다.
    /// </summary>
    /// <param name="meetingId">대소문자와 양끝 공백을 정규화할 외부 ID입니다.</param>
    /// <param name="cancellationToken">조회 결과가 필요 없을 때 중단을 요청하는 신호입니다.</param>
    /// <returns>찾은 예약 또는 안정적인 not-found 오류를 반환합니다.</returns>
    public async ValueTask<Result<MeetingReservation>> GetAsync(
        string? meetingId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(meetingId))
        {
            return Result<MeetingReservation>.Failure(
                new Error("meeting_id.required", "meetingId가 필요합니다.", "meetingId"));
        }

        var meeting = await meetingRepository.FindByIdAsync(
            meetingId.Trim().ToUpperInvariant(),
            cancellationToken);
        // ?: 삼항 연산자는 조회값의 null 여부에 따라 실패와 성공 중 하나를 선택합니다.
        return meeting is null
            ? Result<MeetingReservation>.Failure(
                new Error("meeting.not_found", "예약을 찾을 수 없습니다.", "meetingId"))
            : Result<MeetingReservation>.Success(meeting);
    }
}
