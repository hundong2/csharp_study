using InterviewValidationApi.Application.Ports;
using InterviewValidationApi.Domain;

namespace InterviewValidationApi.Application;

/// <summary>
/// 면접 생성 유스케이스의 검증, 정책 선택, 원자적 저장 순서를 조정하는 Application Service입니다.
/// </summary>
public sealed class ScheduleInterviewService
{
    private static readonly TimeSpan MinimumLeadTime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaximumLeadTime = TimeSpan.FromDays(180);
    private readonly IInterviewRepository _repository;
    private readonly IReadOnlyDictionary<InterviewMode, IInterviewBufferPolicy> _policies;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 저장 Port, 여러 Strategy, 교체 가능한 시계를 받아 유스케이스를 구성합니다.
    /// </summary>
    /// <param name="repository">충돌 검사와 저장을 원자적으로 수행할 Repository Port입니다.</param>
    /// <param name="policies">면접 방식별 준비 시간을 계산할 Strategy 모음입니다.</param>
    /// <param name="timeProvider">현재 시각 규칙을 테스트에서 고정할 수 있게 하는 시계입니다.</param>
    /// <returns>생성자이므로 값을 반환하지 않고 서비스를 사용할 준비를 마칩니다.</returns>
    public ScheduleInterviewService(
        IInterviewRepository repository,
        IEnumerable<IInterviewBufferPolicy> policies,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        // ToDictionary는 enum마다 정확히 한 Strategy만 있게 하며 중복 등록은 시작 단계의 구성 오류로 드러냅니다.
        _policies = policies.ToDictionary(policy => policy.SupportedMode);

        if (Enum.GetValues<InterviewMode>().Any(mode => !_policies.ContainsKey(mode)))
        {
            throw new InvalidOperationException("모든 InterviewMode에 IInterviewBufferPolicy가 등록되어야 합니다.");
        }
    }

    /// <summary>
    /// 요청을 Domain 값으로 만들고 시간 규칙을 확인한 뒤 충돌이 없을 때만 일정을 저장합니다.
    /// </summary>
    /// <param name="command">Presentation 또는 다른 호출자가 전달한 원시 일정 요청입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료 등으로 더 처리하지 않아도 될 때 전달하는 신호입니다.</param>
    /// <returns>저장된 Interview 또는 예상 가능한 검증·충돌 오류를 반환합니다.</returns>
    // async/await는 저장 작업을 기다리는 동안 요청 thread를 막아 두지 않고, 완료 뒤 같은 흐름을 이어 줍니다.
    public async Task<Result<Interview>> ScheduleAsync(
        ScheduleInterviewCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var draftResult = InterviewDraft.Create(
            command.CandidateName,
            command.CandidateEmail,
            command.StartsAtUtc,
            command.DurationMinutes,
            command.Mode,
            command.MeetingUrl,
            command.Room,
            command.InterviewerIds);

        if (!draftResult.IsSuccess)
        {
            return Result<Interview>.Failure(draftResult.Error!);
        }

        var draft = draftResult.Value!;
        var now = _timeProvider.GetUtcNow();
        if (draft.StartsAtUtc < now.Add(MinimumLeadTime))
        {
            return Result<Interview>.Failure(
                new Error("interview.too_soon", "면접은 현재 시각에서 최소 15분 뒤에 시작해야 합니다.", "startsAtUtc"));
        }

        if (draft.StartsAtUtc > now.Add(MaximumLeadTime))
        {
            return Result<Interview>.Failure(
                new Error("interview.too_far", "면접은 현재 시각에서 180일 이내에 예약해야 합니다.", "startsAtUtc"));
        }

        var interview = Interview.Create(Guid.NewGuid(), draft, now);
        var buffer = _policies[draft.Mode].GetBuffer(draft);

        // Repository가 충돌 확인과 추가를 같은 임계 구역에서 수행해야 동시에 온 요청도 이중 예약되지 않습니다.
        var scheduled = await _repository.TryScheduleAsync(interview, buffer, cancellationToken);
        if (!scheduled)
        {
            return Result<Interview>.Failure(
                new Error(
                    "interview.slot_conflict",
                    "면접관 중 한 명에게 준비 시간을 포함해 겹치는 일정이 있습니다.",
                    "interviewerIds"));
        }

        return Result<Interview>.Success(interview);
    }

    /// <summary>
    /// 저장된 면접을 식별자로 조회해 HTTP 계층이 저장 구현에 직접 의존하지 않게 합니다.
    /// </summary>
    /// <param name="id">조회할 면접 식별자입니다.</param>
    /// <param name="cancellationToken">호출자가 조회를 더 기다리지 않을 때 전달하는 신호입니다.</param>
    /// <returns>찾은 Interview 또는 존재하지 않을 때 null을 반환합니다.</returns>
    public ValueTask<Interview?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _repository.FindByIdAsync(id, cancellationToken);
    }
}
