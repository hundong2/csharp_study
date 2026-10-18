using InterviewValidationApi.Domain;

namespace InterviewValidationApi.Application.Ports;

/// <summary>
/// Application이 저장 기술을 몰라도 면접 일정을 원자적으로 예약하고 조회하게 하는 Port입니다.
/// </summary>
public interface IInterviewRepository
{
    /// <summary>
    /// 같은 면접관의 준비 시간까지 겹치는 일정이 없을 때만 새 일정을 한 번에 저장합니다.
    /// </summary>
    /// <param name="interview">저장하려는 완성된 면접 일정입니다.</param>
    /// <param name="buffer">이 일정 앞뒤에 확보할 준비·정리 시간입니다.</param>
    /// <param name="cancellationToken">호출자가 더 기다리지 않을 때 작업을 멈추라는 신호입니다.</param>
    /// <returns>저장했으면 true, 원자적 충돌 검사에서 겹침을 찾았으면 false를 반환합니다.</returns>
    ValueTask<bool> TryScheduleAsync(
        Interview interview,
        InterviewBuffer buffer,
        CancellationToken cancellationToken);

    /// <summary>
    /// 식별자로 저장된 면접 일정을 찾습니다.
    /// </summary>
    /// <param name="id">조회할 면접 일정 식별자입니다.</param>
    /// <param name="cancellationToken">호출자가 조회를 취소할 때 전달하는 신호입니다.</param>
    /// <returns>찾은 면접 또는 존재하지 않을 때 null을 반환합니다.</returns>
    ValueTask<Interview?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}
