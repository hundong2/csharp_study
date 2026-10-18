namespace InterviewValidationApi.Application;

/// <summary>
/// Presentation 계층이 Application Service에 전달하는 원시 면접 일정 요청입니다.
/// </summary>
/// <param name="CandidateName">지원자 이름 원문입니다.</param>
/// <param name="CandidateEmail">지원자 이메일 원문입니다.</param>
/// <param name="StartsAtUtc">면접 시작 시각입니다.</param>
/// <param name="DurationMinutes">면접 길이(분)입니다.</param>
/// <param name="Mode">remote 또는 onsite 문자열입니다.</param>
/// <param name="MeetingUrl">원격 면접 URL입니다.</param>
/// <param name="Room">대면 면접 회의실입니다.</param>
/// <param name="InterviewerIds">면접관 식별자 목록입니다.</param>
public sealed record ScheduleInterviewCommand(
    string? CandidateName,
    string? CandidateEmail,
    DateTimeOffset? StartsAtUtc,
    int DurationMinutes,
    string? Mode,
    string? MeetingUrl,
    string? Room,
    IReadOnlyList<string>? InterviewerIds);
