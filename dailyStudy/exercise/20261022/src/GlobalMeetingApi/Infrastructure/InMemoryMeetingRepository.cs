using GlobalMeetingApi.Application.Ports;
using GlobalMeetingApi.Domain;

namespace GlobalMeetingApi.Infrastructure;

/// <summary>
/// 한 프로세스 안에서 ID 중복과 주최자 UTC 시간 충돌을 원자적으로 지키는 학습용 Adapter입니다.
/// </summary>
public sealed class InMemoryMeetingRepository : IMeetingRepository
{
    // target-typed new()는 왼쪽 선언으로 형식을 알 수 있어 생성자 형식 이름을 반복하지 않습니다.
    private readonly object _gate = new();
    private readonly Dictionary<string, MeetingReservation> _meetings =
        new(StringComparer.Ordinal);

    /// <summary>
    /// ID 확인, 주최자 구간 충돌 확인, 추가를 같은 lock 임계 구역에서 실행합니다.
    /// </summary>
    /// <param name="meeting">Domain과 Application 검증을 모두 통과한 저장 후보입니다.</param>
    /// <param name="cancellationToken">lock에 들어가기 전 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>즉시 계산된 저장 상태를 ValueTask에 담아 반환합니다.</returns>
    public ValueTask<MeetingWriteStatus> TryAddAsync(
        MeetingReservation meeting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        cancellationToken.ThrowIfCancellationRequested();

        // lock은 확인과 저장 사이에 다른 thread가 끼어드는 TOCTOU 경쟁 조건을 막습니다.
        lock (_gate)
        {
            if (_meetings.ContainsKey(meeting.MeetingId))
            {
                return ValueTask.FromResult(MeetingWriteStatus.DuplicateMeetingId);
            }

            // Any와 lambda는 '충돌이 하나라도 있는가'라는 의도를 드러내고 첫 충돌에서 멈춰 불필요한 순회를 피합니다.
            var overlaps = _meetings.Values.Any(existing =>
                string.Equals(
                    existing.OrganizerId,
                    meeting.OrganizerId,
                    StringComparison.Ordinal)
                && existing.Slot.Overlaps(meeting.Slot));
            if (overlaps)
            {
                return ValueTask.FromResult(MeetingWriteStatus.OrganizerTimeConflict);
            }

            _meetings.Add(meeting.MeetingId, meeting);
            return ValueTask.FromResult(MeetingWriteStatus.Saved);
        }
    }

    /// <summary>
    /// 정규화된 회의 ID로 저장된 불변 예약을 조회합니다.
    /// </summary>
    /// <param name="meetingId">대문자로 정규화된 회의 ID입니다.</param>
    /// <param name="cancellationToken">lock에 들어가기 전 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>찾은 예약 또는 null을 ValueTask에 담아 반환합니다.</returns>
    public ValueTask<MeetingReservation?> FindByIdAsync(
        string meetingId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(meetingId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return ValueTask.FromResult(
                _meetings.TryGetValue(meetingId, out var meeting)
                    ? meeting
                    : null);
        }
    }
}
