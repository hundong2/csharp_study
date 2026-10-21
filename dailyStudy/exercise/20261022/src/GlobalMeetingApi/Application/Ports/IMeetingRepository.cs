using GlobalMeetingApi.Domain;

namespace GlobalMeetingApi.Application.Ports;

/// <summary>
/// Application 계층이 구체 저장 기술을 모르고 예약을 저장·조회하게 하는 Repository Port입니다.
/// </summary>
public interface IMeetingRepository
{
    /// <summary>
    /// ID 중복과 같은 주최자의 UTC 시간 충돌을 원자적으로 검사한 뒤 저장합니다.
    /// </summary>
    /// <param name="meeting">모든 Domain 규칙을 통과한 저장 후보입니다.</param>
    /// <param name="cancellationToken">저장 결과가 더는 필요 없을 때 중단을 요청하는 신호입니다.</param>
    /// <returns>저장·ID 중복·시간 충돌 중 하나의 상태를 반환합니다.</returns>
    ValueTask<MeetingWriteStatus> TryAddAsync(
        MeetingReservation meeting,
        CancellationToken cancellationToken);

    /// <summary>
    /// 예약 ID로 저장된 회의를 조회합니다.
    /// </summary>
    /// <param name="meetingId">Application이 정규화한 예약 ID입니다.</param>
    /// <param name="cancellationToken">조회 결과가 더는 필요 없을 때 중단을 요청하는 신호입니다.</param>
    /// <returns>찾은 예약 또는 없음을 뜻하는 null을 반환합니다.</returns>
    ValueTask<MeetingReservation?> FindByIdAsync(
        string meetingId,
        CancellationToken cancellationToken);
}
/// <summary>
/// Repository의 원자적 저장 결과를 제한된 이름 집합으로 표현합니다.
/// </summary>
public enum MeetingWriteStatus
{
    Saved,
    DuplicateMeetingId,
    OrganizerTimeConflict
}
