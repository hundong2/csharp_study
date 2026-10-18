using System.Collections.Immutable;

namespace InterviewValidationApi.Domain;

/// <summary>
/// 검증을 통과해 실제로 예약된 면접 일정을 나타내며 private 생성자로 우회 생성을 막습니다.
/// </summary>
public sealed record Interview
{
    /// <summary>
    /// 검증된 초안과 서버 값만 받아 저장 완료 일정을 초기화합니다.
    /// </summary>
    /// <param name="id">서버가 발급한 비어 있지 않은 일정 식별자입니다.</param>
    /// <param name="draft">모든 순수 불변식을 통과한 면접 초안입니다.</param>
    /// <param name="createdAtUtc">서버가 저장을 시도한 UTC 시각입니다.</param>
    /// <returns>생성자이므로 값을 반환하지 않고 저장 완료 일정을 초기화합니다.</returns>
    private Interview(Guid id, InterviewDraft draft, DateTimeOffset createdAtUtc)
    {
        Id = id;
        CandidateName = draft.CandidateName;
        CandidateEmail = draft.CandidateEmail;
        StartsAtUtc = draft.StartsAtUtc;
        EndsAtUtc = draft.EndsAtUtc;
        Mode = draft.Mode;
        Location = draft.Location;
        InterviewerIds = draft.InterviewerIds;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>서버가 발급한 일정 식별자입니다.</summary>
    public Guid Id { get; }

    /// <summary>앞뒤 공백을 제거한 지원자 이름입니다.</summary>
    public string CandidateName { get; }

    /// <summary>소문자로 정규화한 지원자 이메일입니다.</summary>
    public string CandidateEmail { get; }

    /// <summary>UTC 기준 면접 시작 시각입니다.</summary>
    public DateTimeOffset StartsAtUtc { get; }

    /// <summary>UTC 기준 면접 종료 시각입니다.</summary>
    public DateTimeOffset EndsAtUtc { get; }

    /// <summary>원격 또는 대면 진행 방식입니다.</summary>
    public InterviewMode Mode { get; }

    /// <summary>면접 방식에 맞는 원격 URL 또는 대면 회의실입니다.</summary>
    public string Location { get; }

    /// <summary>정규화하고 정렬한 뒤 방어적으로 복사한 불변 면접관 목록입니다.</summary>
    public ImmutableArray<string> InterviewerIds { get; }

    /// <summary>서버가 일정을 만든 UTC 시각입니다.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// 검증 완료 초안에 서버 식별자와 생성 시각을 더해 저장할 Interview를 만듭니다.
    /// </summary>
    /// <param name="id">서버가 새로 발급한 비어 있지 않은 GUID입니다.</param>
    /// <param name="draft">InterviewDraft.Create를 통과한 초안입니다.</param>
    /// <param name="createdAtUtc">TimeProvider에서 얻은 UTC 생성 시각입니다.</param>
    /// <returns>외부에서 내부 값을 바꿀 수 없는 새 Interview를 반환합니다.</returns>
    public static Interview Create(Guid id, InterviewDraft draft, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("면접 ID는 비어 있을 수 없습니다.", nameof(id));
        }

        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("생성 시각은 UTC여야 합니다.", nameof(createdAtUtc));
        }

        return new Interview(id, draft, createdAtUtc);
    }
}
