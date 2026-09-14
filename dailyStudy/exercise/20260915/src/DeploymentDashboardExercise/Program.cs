namespace DailyStudy.DeploymentDashboard;

/// <summary>결정적인 CQRS/이벤트 소싱 데모와 프레임워크 없는 자체 테스트의 진입점입니다.</summary>
public static class Program
{
    /// <summary>
    /// 명령줄 인수를 확인하여 데모 또는 자체 테스트를 실행합니다.
    /// </summary>
    /// <param name="args"><c>--self-test</c>가 있으면 검증 모드를 선택하는 명령줄 인수입니다.</param>
    /// <returns>정상 완료면 0, 예상하지 못한 오류가 있으면 1을 비동기로 반환합니다.</returns>
    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        try
        {
            if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            {
                await SelfTests.RunAllAsync().ConfigureAwait(false);
                return 0;
            }

            await RunDemoAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"실행 실패: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 두 배포의 이벤트를 만든 뒤 투영 전 stale 조회, 따라잡기, 최신 조회, 멱등 재실행을 차례로 보여 줍니다.
    /// </summary>
    /// <returns>모든 데모 단계가 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task RunDemoAsync()
    {
        var eventStore = new InMemoryDeploymentEventStore();
        var readModel = new InMemoryDeploymentReadModel();
        var commands = new DeploymentCommandService(eventStore);
        var runner = new DeploymentProjectionRunner(
            eventStore,
            readModel,
            new DeploymentDashboardProjection());
        var queries = new DeploymentQueryService(readModel, eventStore);
        var start = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

        EnsureSuccess(await commands.RegisterAsync(
            "DEPLOY-001",
            "production",
            "orders-api:2.4.0",
            start,
            CancellationToken.None).ConfigureAwait(false));
        EnsureSuccess(await commands.StartAsync(
            "DEPLOY-001",
            start.AddMinutes(1),
            CancellationToken.None).ConfigureAwait(false));
        EnsureSuccess(await commands.SucceedAsync(
            "DEPLOY-001",
            start.AddMinutes(2),
            CancellationToken.None).ConfigureAwait(false));

        EnsureSuccess(await commands.RegisterAsync(
            "DEPLOY-002",
            "staging",
            "web:2.4.1",
            start.AddMinutes(3),
            CancellationToken.None).ConfigureAwait(false));
        EnsureSuccess(await commands.StartAsync(
            "DEPLOY-002",
            start.AddMinutes(4),
            CancellationToken.None).ConfigureAwait(false));
        EnsureSuccess(await commands.FailAsync(
            "DEPLOY-002",
            "헬스 체크 제한 시간 초과",
            start.AddMinutes(5),
            CancellationToken.None).ConfigureAwait(false));

        var stale = await queries.GetDashboardAsync(CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine("[투영 전 조회: 의도적으로 stale]");
        Console.WriteLine($"행={stale.Rows.Count}, 체크포인트={stale.Checkpoint}, 원본={stale.SourceHeadPosition}, 지연={stale.Lag}");

        var catchUp = await runner.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine("[프로젝션 따라잡기]");
        Console.WriteLine($"새 적용={catchUp.AppliedCount}, 중복={catchUp.DuplicateCount}, 남은 지연={catchUp.RemainingLag}");

        var current = await queries.GetDashboardAsync(CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine("[투영 후 최신 조회]");
        foreach (var row in current.Rows)
        {
            var reason = row.FailureReason is null ? "-" : row.FailureReason;
            Console.WriteLine(
                $"{row.DeploymentId} | {row.Environment} | {row.ArtifactVersion} | {row.Status} | 실패 이유={reason}");
        }

        var rerun = await runner.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);
        Console.WriteLine("[같은 Runner 재실행]");
        Console.WriteLine($"새 적용={rerun.AppliedCount}, 중복={rerun.DuplicateCount}, 남은 지연={rerun.RemainingLag}");
    }

    /// <summary>
    /// 데모 명령이 실패했다면 조용히 다음 단계로 넘어가지 않고 구체적인 오류로 중단합니다.
    /// </summary>
    /// <param name="result">반드시 성공해야 하는 명령 처리 결과입니다.</param>
    /// <returns>성공이면 아무 값도 반환하지 않고, 실패면 예외를 던집니다.</returns>
    private static void EnsureSuccess(CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"{result.ErrorCode}: {result.ErrorMessage}");
        }
    }
}
