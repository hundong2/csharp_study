using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using InterviewValidationApi.Application;
using InterviewValidationApi.Domain;
using InterviewValidationApi.Presentation.Validation;

namespace InterviewValidationApi.Presentation;

/// <summary>
/// POST JSON의 모양과 전송 계층에서 즉시 확인할 규칙을 선언합니다.
/// </summary>
/// <param name="CandidateName">2~60자의 지원자 이름입니다.</param>
/// <param name="CandidateEmail">이메일 모양의 지원자 연락처입니다.</param>
/// <param name="StartsAtUtc">JSON binding 뒤 오프셋 값이 0인 시작 시각입니다.</param>
/// <param name="DurationMinutes">30~180분 범위의 면접 길이입니다.</param>
/// <param name="Mode">remote 또는 onsite입니다.</param>
/// <param name="MeetingUrl">원격 면접일 때 필요한 HTTPS 주소입니다.</param>
/// <param name="Room">대면 면접일 때 필요한 회의실입니다.</param>
/// <param name="InterviewerIds">1~5개의 면접관 ID입니다.</param>
public sealed partial record CreateInterviewRequest(
    [property: Required(ErrorMessage = "candidateName은 필수입니다.")]
    [property: StringLength(60, MinimumLength = 2, ErrorMessage = "candidateName은 2~60자여야 합니다.")]
    string? CandidateName,

    [property: Required(ErrorMessage = "candidateEmail은 필수입니다.")]
    [property: EmailAddress(ErrorMessage = "candidateEmail 형식을 확인해 주세요.")]
    string? CandidateEmail,

    [property: Required(ErrorMessage = "startsAtUtc는 필수입니다.")]
    [property: UtcDateTime(ErrorMessage = "startsAtUtc는 binding 뒤 UTC의 0 오프셋이어야 합니다.")]
    DateTimeOffset? StartsAtUtc,

    [property: Range(30, 180, ErrorMessage = "durationMinutes는 30~180이어야 합니다.")]
    int DurationMinutes,

    [property: Required(ErrorMessage = "mode는 필수입니다.")]
    [property: RegularExpression("^(remote|onsite)$", ErrorMessage = "mode는 remote 또는 onsite여야 합니다.")]
    string? Mode,

    [property: StringLength(200, ErrorMessage = "meetingUrl은 200자를 넘을 수 없습니다.")]
    string? MeetingUrl,

    [property: StringLength(40, ErrorMessage = "room은 40자를 넘을 수 없습니다.")]
    string? Room,

    [property: Required(ErrorMessage = "interviewerIds는 필수입니다.")]
    [property: MinLength(1, ErrorMessage = "면접관은 최소 1명이어야 합니다.")]
    [property: MaxLength(5, ErrorMessage = "면접관은 최대 5명이어야 합니다.")]
    IReadOnlyList<string>? InterviewerIds) : IValidatableObject
{
    /// <summary>
    /// 여러 필드를 함께 봐야 알 수 있는 15분 단위, 위치 조합, 면접관 ID 규칙을 검사합니다.
    /// </summary>
    /// <param name="validationContext">현재 요청 객체와 검증 서비스에 접근할 수 있는 DataAnnotations 문맥입니다.</param>
    /// <returns>발견한 교차 필드 오류를 차례로 내보내는 열거 가능한 결과를 반환합니다.</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DurationMinutes is >= 30 and <= 180 && DurationMinutes % 15 != 0)
        {
            // yield return은 모든 오류 목록을 미리 만들지 않고 발견한 검증 결과를 하나씩 호출자에게 전달합니다.
            yield return new ValidationResult(
                "durationMinutes는 15분 단위여야 합니다.",
                [nameof(DurationMinutes)]);
        }

        if (Mode == "remote")
        {
            if (!string.IsNullOrWhiteSpace(Room))
            {
                yield return new ValidationResult(
                    "원격 면접에는 room을 보낼 수 없습니다.",
                    [nameof(Room)]);
            }

            if (!Uri.TryCreate(MeetingUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                yield return new ValidationResult(
                    "원격 면접에는 절대 HTTPS meetingUrl이 필요합니다.",
                    [nameof(MeetingUrl)]);
            }
        }

        if (Mode == "onsite")
        {
            if (!string.IsNullOrWhiteSpace(MeetingUrl))
            {
                yield return new ValidationResult(
                    "대면 면접에는 meetingUrl을 보낼 수 없습니다.",
                    [nameof(MeetingUrl)]);
            }

            if (string.IsNullOrWhiteSpace(Room) || Room.Trim().Length < 2)
            {
                yield return new ValidationResult(
                    "대면 면접에는 2~40자의 room이 필요합니다.",
                    [nameof(Room)]);
            }
        }

        if (InterviewerIds is null)
        {
            yield break;
        }

        var normalizedIds = InterviewerIds
            .Select(id => id?.Trim().ToUpperInvariant() ?? string.Empty)
            .ToArray();

        if (normalizedIds.Any(id => !InterviewerIdRegex().IsMatch(id)))
        {
            yield return new ValidationResult(
                "각 interviewer ID는 영문자로 시작하는 3~20자의 영문자·숫자·하이픈이어야 합니다.",
                [nameof(InterviewerIds)]);
        }

        if (normalizedIds.Distinct(StringComparer.Ordinal).Count() != normalizedIds.Length)
        {
            yield return new ValidationResult(
                "interviewerIds에 같은 ID를 두 번 넣을 수 없습니다.",
                [nameof(InterviewerIds)]);
        }
    }

    /// <summary>
    /// 전송 DTO를 Application Service가 이해하는 command로 복사합니다.
    /// </summary>
    /// <returns>같은 원시 값을 가진 ScheduleInterviewCommand를 반환합니다.</returns>
    public ScheduleInterviewCommand ToCommand()
    {
        return new ScheduleInterviewCommand(
            CandidateName,
            CandidateEmail,
            StartsAtUtc,
            DurationMinutes,
            Mode,
            MeetingUrl,
            Room,
            InterviewerIds);
    }

    /// <summary>
    /// 면접관 ID 모양 검사용 Regex를 compile time에 생성합니다.
    /// </summary>
    /// <returns>대문자로 정규화된 ID를 검사하는 Regex를 반환합니다.</returns>
    [GeneratedRegex("^[A-Z][A-Z0-9-]{2,19}$", RegexOptions.CultureInvariant)]
    private static partial Regex InterviewerIdRegex();
}

/// <summary>
/// 내부 Domain 객체에서 공개 가능한 필드만 골라 API 성공 JSON으로 내보냅니다.
/// </summary>
/// <param name="Id">면접 일정 식별자입니다.</param>
/// <param name="CandidateName">지원자 이름입니다.</param>
/// <param name="CandidateEmail">지원자 이메일입니다.</param>
/// <param name="StartsAtUtc">UTC 시작 시각입니다.</param>
/// <param name="EndsAtUtc">UTC 종료 시각입니다.</param>
/// <param name="Mode">remote 또는 onsite 문자열입니다.</param>
/// <param name="Location">화상 URL 또는 회의실입니다.</param>
/// <param name="InterviewerIds">정렬된 면접관 ID 목록입니다.</param>
/// <param name="CreatedAtUtc">서버 저장 시각입니다.</param>
public sealed record InterviewResponse(
    Guid Id,
    string CandidateName,
    string CandidateEmail,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string Mode,
    string Location,
    IReadOnlyList<string> InterviewerIds,
    DateTimeOffset CreatedAtUtc)
{
    /// <summary>
    /// Domain Interview를 외부 응답 계약으로 변환합니다.
    /// </summary>
    /// <param name="interview">변환할 저장 완료 면접입니다.</param>
    /// <returns>외부에 공개해도 되는 값만 담은 InterviewResponse를 반환합니다.</returns>
    public static InterviewResponse FromDomain(Interview interview)
    {
        ArgumentNullException.ThrowIfNull(interview);
        return new InterviewResponse(
            interview.Id,
            interview.CandidateName,
            interview.CandidateEmail,
            interview.StartsAtUtc,
            interview.EndsAtUtc,
            interview.Mode == InterviewMode.Remote ? "remote" : "onsite",
            interview.Location,
            interview.InterviewerIds,
            interview.CreatedAtUtc);
    }
}
