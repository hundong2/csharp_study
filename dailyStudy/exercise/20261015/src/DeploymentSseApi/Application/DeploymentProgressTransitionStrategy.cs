using DeploymentSseApi.Domain;

namespace DeploymentSseApi.Application;

/// <summary>
/// 외부 Draft와 이전 Event를 비교해 다음 Event를 만들 수 있는지 결정하는 Strategy 계약입니다.
/// Application Service는 구체 규칙을 몰라도 이 계약만 호출하므로 규칙을 교체하거나 단위 테스트하기 쉽습니다.
/// </summary>
public interface IDeploymentProgressTransitionStrategy
{
    /// <summary>
    /// 입력 모양, 단계 전이, 진행률 단조 증가를 모두 검증하고 다음 불변 Event를 만듭니다.
    /// </summary>
    /// <param name="draft">아직 신뢰할 수 없는 배포 ID, 단계, 진행률, 설명입니다.</param>
    /// <param name="previous">저장소의 최신 Event이며 첫 게시라면 null입니다.</param>
    /// <param name="occurredAtUtc">새 Event에 기록할 요청 기준 UTC 시각입니다.</param>
    /// <returns>검증된 다음 Event 또는 호출자가 고칠 수 있는 Domain 오류를 반환합니다.</returns>
    Result<DeploymentProgressEvent> CreateNext(
        DeploymentProgressDraft draft,
        DeploymentProgressEvent? previous,
        DateTimeOffset occurredAtUtc);
}

/// <summary>
/// Queued → Building → Deploying → Completed 순서와 중간 Failed 종료를 적용하는 기본 Strategy입니다.
/// 같은 Building 또는 Deploying 단계 안에서는 여러 진행 메시지를 허용하되 Percent가 뒤로 가지 못하게 합니다.
/// </summary>
public sealed class DeploymentProgressTransitionStrategy : IDeploymentProgressTransitionStrategy
{
    /// <summary>
    /// Draft를 정규화하고 이전 Event와 비교하여 EventId가 하나 증가한 다음 Event를 만듭니다.
    /// </summary>
    /// <param name="draft">검증할 배포 진행 후보입니다.</param>
    /// <param name="previous">같은 배포의 최신 Event이며 처음이면 null입니다.</param>
    /// <param name="occurredAtUtc">Application Service가 TimeProvider에서 읽은 발생 시각입니다.</param>
    /// <returns>저장 가능한 Event 또는 첫 번째 검증 실패를 반환합니다.</returns>
    public Result<DeploymentProgressEvent> CreateNext(
        DeploymentProgressDraft draft,
        DeploymentProgressEvent? previous,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var deploymentIdResult = DeploymentIdentity.Normalize(draft.DeploymentId);
        if (!deploymentIdResult.IsSuccess)
        {
            // postfix !는 위 실패 검사 덕분에 Error가 null이 아님을 nullable 분석기에 알려 줍니다.
            return Result<DeploymentProgressEvent>.Failure(deploymentIdResult.Error!);
        }

        var stageResult = ParseStage(draft.Stage);
        if (!stageResult.IsSuccess)
        {
            return Result<DeploymentProgressEvent>.Failure(stageResult.Error!);
        }

        if (draft.Percent is < 0 or > 100)
        {
            return Result<DeploymentProgressEvent>.Failure(
                new Error(
                    "deployment.percent.out_of_range",
                    "percent는 0부터 100 사이여야 합니다.",
                    "percent"));
        }

        var message = draft.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message) || message.Length > 200)
        {
            return Result<DeploymentProgressEvent>.Failure(
                new Error(
                    "deployment.message.invalid",
                    "message는 공백이 아닌 1~200자여야 합니다.",
                    "message"));
        }

        var stage = stageResult.Value;
        // switch expression의 when guard로 단계별 범위를 표처럼 나열하면 업무 경계를 빠뜨리지 않고 검토하기 쉽습니다.
        Error? stagePercentError = stage switch
        {
            DeploymentStage.Queued when draft.Percent != 0 =>
                new Error(
                    "deployment.queued_percent.invalid",
                    "Queued 단계의 percent는 0이어야 합니다.",
                    "percent"),
            DeploymentStage.Building when draft.Percent is < 1 or > 69 =>
                new Error(
                    "deployment.building_percent.invalid",
                    "Building 단계의 percent는 1부터 69 사이여야 합니다.",
                    "percent"),
            DeploymentStage.Deploying when draft.Percent is < 70 or > 99 =>
                new Error(
                    "deployment.deploying_percent.invalid",
                    "Deploying 단계의 percent는 70부터 99 사이여야 합니다.",
                    "percent"),
            DeploymentStage.Completed when draft.Percent != 100 =>
                new Error(
                    "deployment.completed_percent.invalid",
                    "Completed 단계의 percent는 100이어야 합니다.",
                    "percent"),
            DeploymentStage.Failed when draft.Percent > 99 =>
                new Error(
                    "deployment.failed_percent.invalid",
                    "Failed 단계의 percent는 0부터 99 사이여야 합니다.",
                    "percent"),
            _ => null
        };
        if (stagePercentError is not null)
        {
            return Result<DeploymentProgressEvent>.Failure(
                stagePercentError);
        }

        if (previous is null)
        {
            if (stage is not DeploymentStage.Queued)
            {
                return Result<DeploymentProgressEvent>.Failure(
                    new Error(
                        "deployment.initial_stage.invalid",
                        "첫 이벤트는 Queued 단계여야 합니다.",
                        "stage"));
            }

            return Result<DeploymentProgressEvent>.Success(
                new DeploymentProgressEvent(
                    EventId: 1,
                    deploymentIdResult.Value!,
                    stage,
                    draft.Percent,
                    message,
                    occurredAtUtc.ToUniversalTime()));
        }

        if (!string.Equals(
                previous.DeploymentId,
                deploymentIdResult.Value,
                StringComparison.Ordinal))
        {
            return Result<DeploymentProgressEvent>.Failure(
                new Error(
                    "deployment.id.mismatch",
                    "이전 이벤트와 새 이벤트의 deploymentId가 다릅니다.",
                    "deploymentId"));
        }

        if (previous.IsTerminal)
        {
            return Result<DeploymentProgressEvent>.Failure(
                new Error(
                    "deployment.already_terminal",
                    "Completed 또는 Failed 배포에는 새 이벤트를 추가할 수 없습니다.",
                    "stage"));
        }

        if (!IsAllowedTransition(previous.Stage, stage))
        {
            return Result<DeploymentProgressEvent>.Failure(
                new Error(
                    "deployment.transition.invalid",
                    $"{previous.Stage} 단계에서 {stage} 단계로 이동할 수 없습니다.",
                    "stage"));
        }

        if (draft.Percent < previous.Percent)
        {
            return Result<DeploymentProgressEvent>.Failure(
                new Error(
                    "deployment.percent.decreased",
                    $"percent는 이전 값 {previous.Percent}보다 작아질 수 없습니다.",
                    "percent"));
        }

        return Result<DeploymentProgressEvent>.Success(
            new DeploymentProgressEvent(
                // checked는 EventId가 long 최댓값을 넘으면 조용히 음수가 되는 대신 OverflowException으로 불변식 위반을 드러냅니다.
                EventId: checked(previous.EventId + 1),
                previous.DeploymentId,
                stage,
                draft.Percent,
                message,
                occurredAtUtc.ToUniversalTime()));
    }

    /// <summary>
    /// 대소문자를 무시한 단계 문자열을 허용된 DeploymentStage 값으로 바꿉니다.
    /// </summary>
    /// <param name="stage">외부 요청에서 받은 아직 검증하지 않은 단계 문자열입니다.</param>
    /// <returns>정의된 단계 또는 stage 필드 오류를 반환합니다.</returns>
    private static Result<DeploymentStage> ParseStage(string? stage)
    {
        var normalizedStage = stage?.Trim();
        // out var는 TryParse가 만든 값을 새 지역 변수로 선언하면서 받고, ignoreCase는 queued와 Queued를 같은 값으로 취급합니다.
        // 첫 글자 검사는 "0" 같은 숫자 문자열을, Enum.GetName과의 정확한 비교는 "Building,Deploying" 같은 합성 문자열을 막습니다.
        // Enum.TryParse는 [Flags]가 아닌 enum에서도 쉼표 이름을 비트 OR로 합칠 수 있으므로 TryParse 성공만 믿으면 안 됩니다.
        if (!string.IsNullOrEmpty(normalizedStage) &&
            char.IsAsciiLetter(normalizedStage[0]) &&
            Enum.TryParse<DeploymentStage>(normalizedStage, ignoreCase: true, out var parsed) &&
            Enum.GetName(parsed) is string definedName &&
            string.Equals(normalizedStage, definedName, StringComparison.OrdinalIgnoreCase))
        {
            return Result<DeploymentStage>.Success(parsed);
        }

        return Result<DeploymentStage>.Failure(
            new Error(
                "deployment.stage.invalid",
                "stage는 Queued, Building, Deploying, Completed, Failed 중 하나여야 합니다.",
                "stage"));
    }

    /// <summary>
    /// 이전 단계에서 새 단계로 이동하는 경로가 업무 흐름에 포함되는지 검사합니다.
    /// </summary>
    /// <param name="previous">현재 저장된 마지막 단계입니다.</param>
    /// <param name="next">새로 게시하려는 단계입니다.</param>
    /// <returns>허용된 다음 단계이거나 Building/Deploying 내부 진행 갱신이면 true를 반환합니다.</returns>
    private static bool IsAllowedTransition(DeploymentStage previous, DeploymentStage next)
    {
        // tuple switch는 (이전, 다음) 조합을 허용 목록으로 직접 적어 빠뜨린 경로를 눈으로 검토하기 쉽게 합니다.
        return (previous, next) switch
        {
            (DeploymentStage.Queued, DeploymentStage.Building or DeploymentStage.Failed) => true,
            (DeploymentStage.Building, DeploymentStage.Building or DeploymentStage.Deploying or DeploymentStage.Failed) => true,
            (DeploymentStage.Deploying, DeploymentStage.Deploying or DeploymentStage.Completed or DeploymentStage.Failed) => true,
            _ => false
        };
    }
}
