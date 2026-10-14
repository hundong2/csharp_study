using System.Globalization;
using DeploymentSseApi.Application.Ports;
using DeploymentSseApi.Domain;

namespace DeploymentSseApi.Application;

/// <summary>
/// 진행 Event 게시와 재연결 가능한 SSE stream 열기 순서를 조율하는 Application Service입니다.
/// HTTP 표현, Domain 전이 규칙, 저장소 동시성 코드를 분리해 각 부분을 독립적으로 테스트하고 교체할 수 있게 합니다.
/// </summary>
public sealed class DeploymentProgressApplicationService
{
    private const int MaximumAppendAttempts = 5;

    private readonly IDeploymentProgressRepository _repository;
    private readonly IDeploymentProgressTransitionStrategy _transitionStrategy;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// use case에 필요한 Repository Port, 전이 Strategy, 교체 가능한 시계를 주입받습니다.
    /// </summary>
    /// <param name="repository">최신 Event 조회, 낙관적 추가, 원자적 stream 열기를 제공하는 Port입니다.</param>
    /// <param name="transitionStrategy">입력과 이전 상태를 비교해 다음 Event를 검증·생성하는 Strategy입니다.</param>
    /// <param name="timeProvider">운영에서는 실제 시각을, 테스트에서는 고정 시각을 제공하는 .NET 시계 추상화입니다.</param>
    /// <returns>생성자는 Application Service를 초기화하므로 별도 반환값은 없습니다.</returns>
    public DeploymentProgressApplicationService(
        IDeploymentProgressRepository repository,
        IDeploymentProgressTransitionStrategy transitionStrategy,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(transitionStrategy);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _transitionStrategy = transitionStrategy;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 최신 Event를 읽고 전이를 검증한 뒤, 동시에 다른 요청이 기록했다면 최신 상태로 다시 계산하여 게시합니다.
    /// </summary>
    /// <param name="draft">HTTP 입력을 framework 형식에서 분리한 아직 신뢰할 수 없는 진행 후보입니다.</param>
    /// <param name="cancellationToken">요청이 끝나 더는 게시 결과가 필요 없다는 취소 신호입니다.</param>
    /// <returns>저장되고 구독자에게 전달된 Event 또는 검증·동시성 오류가 든 Task를 반환합니다.</returns>
    // async/await는 저장소를 기다리는 동안 thread를 붙잡지 않으면서 취소와 예외 흐름을 그대로 보존합니다.
    public async Task<Result<DeploymentProgressEvent>> PublishAsync(
        DeploymentProgressDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var deploymentIdResult = DeploymentIdentity.Normalize(draft.DeploymentId);
        if (!deploymentIdResult.IsSuccess)
        {
            return Result<DeploymentProgressEvent>.Failure(deploymentIdResult.Error!);
        }

        // with는 원본 record를 바꾸지 않고 지정한 속성만 달라진 새 Draft를 만들어 불변성을 유지합니다.
        var normalizedDraft = draft with { DeploymentId = deploymentIdResult.Value! };
        // 재시도 중 시각이 흔들리지 않도록 한 요청에서는 TimeProvider를 정확히 한 번 읽습니다.
        var occurredAtUtc = _timeProvider.GetUtcNow();

        for (var attempt = 1; attempt <= MaximumAppendAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var previous = await _repository.GetLatestAsync(
                normalizedDraft.DeploymentId!,
                cancellationToken);
            var candidateResult = _transitionStrategy.CreateNext(
                normalizedDraft,
                previous,
                occurredAtUtc);
            if (!candidateResult.IsSuccess)
            {
                return Result<DeploymentProgressEvent>.Failure(candidateResult.Error!);
            }

            var candidate = candidateResult.Value!;
            // ?.는 previous가 null이면 EventId를 읽지 않고 null을 만들고, ??는 그때 첫 버전 0을 선택합니다.
            var expectedLastEventId = previous?.EventId ?? 0;
            var appendResult = await _repository.TryAppendAsync(
                candidate,
                expectedLastEventId,
                cancellationToken);
            if (appendResult.Status is DeploymentProgressAppendStatus.Appended)
            {
                return Result<DeploymentProgressEvent>.Success(candidate);
            }

            // Conflict이면 다른 요청이 먼저 기록했습니다. 다음 반복에서 최신 Event를 다시 읽고 규칙도 다시 검증해야 lost update가 생기지 않습니다.
        }

        return Result<DeploymentProgressEvent>.Failure(
            new Error(
                "deployment.concurrent_update",
                "동시에 진행 상태가 여러 번 바뀌어 게시하지 못했습니다. 잠시 후 다시 시도하세요."));
    }

    /// <summary>
    /// Last-Event-ID를 안전하게 해석한 뒤 누락 구간 replay와 live 구독이 이어진 stream을 엽니다.
    /// </summary>
    /// <param name="deploymentId">route에서 받은 아직 검증하지 않은 배포 식별자입니다.</param>
    /// <param name="lastEventId">SSE 재연결 헤더 값이며 없거나 공백이면 처음 연결로 처리합니다.</param>
    /// <param name="cancellationToken">HTTP 연결이 끊기면 Adapter가 구독자를 즉시 제거하도록 전달할 신호입니다.</param>
    /// <returns>순서가 보장된 Event stream 또는 입력·미존재 오류가 든 Task를 반환합니다.</returns>
    public async Task<Result<DeploymentProgressStream>> OpenStreamAsync(
        string? deploymentId,
        string? lastEventId,
        CancellationToken cancellationToken)
    {
        var deploymentIdResult = DeploymentIdentity.Normalize(deploymentId);
        if (!deploymentIdResult.IsSuccess)
        {
            return Result<DeploymentProgressStream>.Failure(deploymentIdResult.Error!);
        }

        var lastEventIdResult = ParseLastEventId(lastEventId);
        if (!lastEventIdResult.IsSuccess)
        {
            return Result<DeploymentProgressStream>.Failure(lastEventIdResult.Error!);
        }

        var stream = await _repository.OpenStreamAsync(
            deploymentIdResult.Value!,
            lastEventIdResult.Value,
            cancellationToken);
        if (stream is null)
        {
            return Result<DeploymentProgressStream>.Failure(
                new Error(
                    "deployment.not_found",
                    "요청한 배포 진행 기록을 찾을 수 없습니다.",
                    "deploymentId"));
        }

        return Result<DeploymentProgressStream>.Success(stream);
    }

    /// <summary>
    /// 선택적인 Last-Event-ID 문자열을 0 이상의 long 값으로 엄격하게 변환합니다.
    /// </summary>
    /// <param name="lastEventId">헤더에서 받은 값이며 처음 연결에서는 null 또는 공백일 수 있습니다.</param>
    /// <returns>처음 연결을 뜻하는 0, 파싱한 EventId, 또는 Last-Event-ID 오류를 반환합니다.</returns>
    private static Result<long> ParseLastEventId(string? lastEventId)
    {
        if (string.IsNullOrWhiteSpace(lastEventId))
        {
            return Result<long>.Success(0);
        }

        // NumberStyles.None은 부호·소수점 같은 문자를 막고, InvariantCulture는 실행 서버 언어와 무관하게 같은 숫자 규칙을 적용합니다.
        if (!long.TryParse(
                lastEventId.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed < 0)
        {
            return Result<long>.Failure(
                new Error(
                    "deployment.last_event_id.invalid",
                    "Last-Event-ID는 0 이상의 정수여야 합니다.",
                    "Last-Event-ID"));
        }

        return Result<long>.Success(parsed);
    }
}
