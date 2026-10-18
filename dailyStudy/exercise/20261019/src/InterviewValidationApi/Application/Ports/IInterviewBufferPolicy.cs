using InterviewValidationApi.Domain;

namespace InterviewValidationApi.Application.Ports;

/// <summary>
/// 면접 방식별 준비·정리 시간 계산을 교체 가능한 Strategy로 정의합니다.
/// </summary>
public interface IInterviewBufferPolicy
{
    /// <summary>
    /// 이 Strategy가 담당하는 면접 방식을 알려 줍니다.
    /// </summary>
    /// <returns>Remote 또는 Onsite 중 하나를 반환합니다.</returns>
    InterviewMode SupportedMode { get; }

    /// <summary>
    /// 담당 면접 방식에 필요한 앞뒤 여유 시간을 계산합니다.
    /// </summary>
    /// <param name="draft">검증을 마친 면접 초안입니다.</param>
    /// <returns>Repository 충돌 검사에 사용할 앞뒤 여유 시간을 반환합니다.</returns>
    InterviewBuffer GetBuffer(InterviewDraft draft);
}
