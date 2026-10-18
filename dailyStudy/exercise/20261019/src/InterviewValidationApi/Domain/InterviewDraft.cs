using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace InterviewValidationApi.Domain;

/// <summary>
/// 외부 입력을 정규화하고 순수한 Domain 불변식을 통과한 예약 전 면접 정보를 나타냅니다.
/// </summary>
public sealed partial record InterviewDraft
{
    /// <summary>
    /// 팩터리 검증을 통과한 값만 받을 수 있도록 생성 경로를 이 형식 내부로 제한합니다.
    /// </summary>
    /// <param name="candidateName">정규화한 지원자 이름입니다.</param>
    /// <param name="candidateEmail">정규화한 지원자 이메일입니다.</param>
    /// <param name="startsAtUtc">UTC 시작 시각입니다.</param>
    /// <param name="endsAtUtc">표현 범위를 확인한 UTC 종료 시각입니다.</param>
    /// <param name="mode">해석을 마친 면접 방식입니다.</param>
    /// <param name="location">방식에 맞는 URL 또는 회의실입니다.</param>
    /// <param name="interviewerIds">방어적으로 복사한 불변 면접관 목록입니다.</param>
    /// <returns>생성자이므로 값을 반환하지 않고 검증 완료 초안을 초기화합니다.</returns>
    private InterviewDraft(
        string candidateName,
        string candidateEmail,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        InterviewMode mode,
        string location,
        ImmutableArray<string> interviewerIds)
    {
        CandidateName = candidateName;
        CandidateEmail = candidateEmail;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        Mode = mode;
        Location = location;
        InterviewerIds = interviewerIds;
    }

    /// <summary>정규화한 지원자 이름입니다.</summary>
    public string CandidateName { get; }

    /// <summary>정규화한 지원자 이메일입니다.</summary>
    public string CandidateEmail { get; }

    /// <summary>UTC 기준 면접 시작 시각입니다.</summary>
    public DateTimeOffset StartsAtUtc { get; }

    /// <summary>UTC 기준 면접 종료 시각입니다.</summary>
    public DateTimeOffset EndsAtUtc { get; }

    /// <summary>해석을 마친 원격 또는 대면 면접 방식입니다.</summary>
    public InterviewMode Mode { get; }

    /// <summary>면접 방식에 맞는 HTTPS URL 또는 회의실입니다.</summary>
    public string Location { get; }

    /// <summary>정규화·정렬 후 방어적으로 복사한 불변 면접관 ID 목록입니다.</summary>
    public ImmutableArray<string> InterviewerIds { get; }

    /// <summary>
    /// HTTP를 거치지 않은 호출도 안전하도록 원시 입력을 다시 검증하고 Domain 초안을 만듭니다.
    /// </summary>
    /// <param name="candidateName">지원자 이름 원문입니다.</param>
    /// <param name="candidateEmail">지원자 이메일 원문입니다.</param>
    /// <param name="startsAtUtc">면접 시작 시각입니다.</param>
    /// <param name="durationMinutes">면접 길이(분)입니다.</param>
    /// <param name="modeText">remote 또는 onsite 문자열입니다.</param>
    /// <param name="meetingUrl">원격 면접 URL이며 대면 면접에서는 비어 있어야 합니다.</param>
    /// <param name="room">대면 회의실이며 원격 면접에서는 비어 있어야 합니다.</param>
    /// <param name="interviewerIds">면접관 식별자 원문 목록입니다.</param>
    /// <returns>모든 불변식이 맞으면 InterviewDraft, 아니면 첫 번째 Domain 오류를 반환합니다.</returns>
    public static Result<InterviewDraft> Create(
        string? candidateName,
        string? candidateEmail,
        DateTimeOffset? startsAtUtc,
        int durationMinutes,
        string? modeText,
        string? meetingUrl,
        string? room,
        IReadOnlyList<string>? interviewerIds)
    {
        var normalizedName = candidateName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length is < 2 or > 60)
        {
            return Result<InterviewDraft>.Failure(
                new Error("interview.invalid_candidate_name", "지원자 이름은 공백 제외 2~60자여야 합니다.", "candidateName"));
        }

        var normalizedEmail = candidateEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !SimpleEmailRegex().IsMatch(normalizedEmail))
        {
            return Result<InterviewDraft>.Failure(
                new Error("interview.invalid_candidate_email", "지원자 이메일 형식을 확인해 주세요.", "candidateEmail"));
        }

        if (startsAtUtc is null || startsAtUtc.Value.Offset != TimeSpan.Zero)
        {
            return Result<InterviewDraft>.Failure(
                new Error(
                    "interview.invalid_start",
                    "시작 시각은 UTC의 0 오프셋 값이어야 합니다.",
                    "startsAtUtc"));
        }

        if (durationMinutes is < 30 or > 180 || durationMinutes % 15 != 0)
        {
            return Result<InterviewDraft>.Failure(
                new Error("interview.invalid_duration", "면접 길이는 30~180분 범위의 15분 단위여야 합니다.", "durationMinutes"));
        }

        // switch 식은 문자열을 제한된 enum 값으로 바꾸고, 모르는 값은 null로 표시합니다.
        InterviewMode? mode = modeText?.Trim().ToLowerInvariant() switch
        {
            "remote" => InterviewMode.Remote,
            "onsite" => InterviewMode.Onsite,
            _ => null
        };

        if (mode is null)
        {
            return Result<InterviewDraft>.Failure(
                new Error("interview.invalid_mode", "mode는 remote 또는 onsite여야 합니다.", "mode"));
        }

        var locationResult = CreateLocation(mode.Value, meetingUrl, room);
        if (!locationResult.IsSuccess)
        {
            // !는 위에서 성공 여부를 검사했음을 compiler에 알려 nullable 경고 없이 Error를 전달하게 합니다.
            return Result<InterviewDraft>.Failure(locationResult.Error!);
        }

        if (interviewerIds is null || interviewerIds.Count is < 1 or > 5)
        {
            return Result<InterviewDraft>.Failure(
                new Error("interview.invalid_interviewers", "면접관은 1~5명이어야 합니다.", "interviewerIds"));
        }

        // LINQ는 각 ID를 정규화한 뒤 새 배열을 만들며, 입력 목록 자체는 변경하지 않습니다.
        var normalizedInterviewers = interviewerIds
            .Select(id => id?.Trim().ToUpperInvariant() ?? string.Empty)
            .ToArray();

        if (normalizedInterviewers.Any(id => !InterviewerIdRegex().IsMatch(id)))
        {
            return Result<InterviewDraft>.Failure(
                new Error("interview.invalid_interviewer_id", "면접관 ID는 영문자로 시작하는 3~20자의 영문자·숫자·하이픈이어야 합니다.", "interviewerIds"));
        }

        if (normalizedInterviewers.Distinct(StringComparer.Ordinal).Count() != normalizedInterviewers.Length)
        {
            return Result<InterviewDraft>.Failure(
                new Error("interview.duplicate_interviewer", "같은 면접관 ID를 두 번 넣을 수 없습니다.", "interviewerIds"));
        }

        Array.Sort(normalizedInterviewers, StringComparer.Ordinal);
        var latestRepresentableStart = DateTimeOffset.MaxValue.AddMinutes(-durationMinutes);
        if (startsAtUtc.Value > latestRepresentableStart)
        {
            return Result<InterviewDraft>.Failure(
                new Error(
                    "interview.invalid_time_range",
                    "시작 시각과 면접 길이를 더한 종료 시각을 표현할 수 없습니다.",
                    "startsAtUtc"));
        }

        var end = startsAtUtc.Value.AddMinutes(durationMinutes);
        return Result<InterviewDraft>.Success(
            new InterviewDraft(
                normalizedName,
                normalizedEmail,
                startsAtUtc.Value,
                end,
                mode.Value,
                locationResult.Value!,
                // ToImmutableArray는 지역 배열을 방어적으로 복사해 외부 cast로 저장 상태를 바꾸지 못하게 합니다.
                normalizedInterviewers.ToImmutableArray()));
    }

    /// <summary>
    /// 면접 방식에 따라 필요한 위치 필드를 고르고 서로 모순된 조합을 거절합니다.
    /// </summary>
    /// <param name="mode">이미 해석된 원격 또는 대면 방식입니다.</param>
    /// <param name="meetingUrl">원격 회의 주소 후보입니다.</param>
    /// <param name="room">대면 회의실 후보입니다.</param>
    /// <returns>정규화한 위치 문자열 또는 필드 조합 오류를 반환합니다.</returns>
    private static Result<string> CreateLocation(InterviewMode mode, string? meetingUrl, string? room)
    {
        var normalizedUrl = meetingUrl?.Trim();
        var normalizedRoom = room?.Trim();

        if (mode == InterviewMode.Remote)
        {
            if (!string.IsNullOrWhiteSpace(normalizedRoom))
            {
                return Result<string>.Failure(
                    new Error("interview.remote_room_not_allowed", "원격 면접에는 room을 보낼 수 없습니다.", "room"));
            }

            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return Result<string>.Failure(
                    new Error("interview.invalid_meeting_url", "원격 면접에는 절대 HTTPS meetingUrl이 필요합니다.", "meetingUrl"));
            }

            return Result<string>.Success(uri.AbsoluteUri);
        }

        if (!string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return Result<string>.Failure(
                new Error("interview.onsite_url_not_allowed", "대면 면접에는 meetingUrl을 보낼 수 없습니다.", "meetingUrl"));
        }

        if (string.IsNullOrWhiteSpace(normalizedRoom) || normalizedRoom.Length is < 2 or > 40)
        {
            return Result<string>.Failure(
                new Error("interview.invalid_room", "대면 면접에는 2~40자의 room이 필요합니다.", "room"));
        }

        return Result<string>.Success(normalizedRoom);
    }

    /// <summary>
    /// 학습 예제에서 사용할 보수적인 이메일 모양 검사기를 compile time에 생성합니다.
    /// </summary>
    /// <returns>공백과 @ 위치의 기본 모양을 확인하는 Regex를 반환합니다.</returns>
    // GeneratedRegex와 partial 메서드는 반복 실행 때 정규식을 다시 해석하지 않게 compiler가 구현을 생성합니다.
    [GeneratedRegex("^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SimpleEmailRegex();

    /// <summary>
    /// 면접관 ID에 허용한 문자와 길이를 검사할 정규식을 compile time에 생성합니다.
    /// </summary>
    /// <returns>영문자로 시작하는 3~20자 ID를 확인하는 Regex를 반환합니다.</returns>
    [GeneratedRegex("^[A-Z][A-Z0-9-]{2,19}$", RegexOptions.CultureInvariant)]
    private static partial Regex InterviewerIdRegex();
}
