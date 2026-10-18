using InterviewValidationApi.Application.Ports;
using InterviewValidationApi.Domain;

namespace InterviewValidationApi.Application;

/// <summary>
/// 화상 연결 점검을 위해 원격 면접 앞뒤에 짧은 여유 시간을 두는 Strategy입니다.
/// </summary>
public sealed class RemoteInterviewBufferPolicy : IInterviewBufferPolicy
{
    /// <summary>
    /// 이 정책이 원격 면접용임을 알려 줍니다.
    /// </summary>
    /// <returns>InterviewMode.Remote를 반환합니다.</returns>
    public InterviewMode SupportedMode => InterviewMode.Remote;

    /// <summary>
    /// 화상 장비 점검과 기록 정리를 위해 앞뒤 5분을 확보합니다.
    /// </summary>
    /// <param name="draft">정책을 적용할 검증된 원격 면접 초안입니다.</param>
    /// <returns>앞 5분, 뒤 5분의 InterviewBuffer를 반환합니다.</returns>
    public InterviewBuffer GetBuffer(InterviewDraft draft)
    {
        // 현재 규칙은 초안의 다른 값과 무관하지만 매개변수는 향후 직무별 정책 확장을 가능하게 합니다.
        ArgumentNullException.ThrowIfNull(draft);
        return new InterviewBuffer(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }
}

/// <summary>
/// 이동과 회의실 정리를 위해 대면 면접 앞뒤에 넉넉한 여유 시간을 두는 Strategy입니다.
/// </summary>
public sealed class OnsiteInterviewBufferPolicy : IInterviewBufferPolicy
{
    /// <summary>
    /// 이 정책이 대면 면접용임을 알려 줍니다.
    /// </summary>
    /// <returns>InterviewMode.Onsite를 반환합니다.</returns>
    public InterviewMode SupportedMode => InterviewMode.Onsite;

    /// <summary>
    /// 이동과 회의실 준비를 위해 면접 앞뒤 15분을 확보합니다.
    /// </summary>
    /// <param name="draft">정책을 적용할 검증된 대면 면접 초안입니다.</param>
    /// <returns>앞 15분, 뒤 15분의 InterviewBuffer를 반환합니다.</returns>
    public InterviewBuffer GetBuffer(InterviewDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return new InterviewBuffer(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
    }
}
