namespace DailyStudy.DeploymentDashboard;

/// <summary>외부 테스트 패키지 없이 핵심 계약을 실행 검증하는 자체 테스트 모음입니다.</summary>
public static class SelfTests
{
    // new(...)는 왼쪽 DateTimeOffset 타입이 분명해서 생성자 앞 타입 이름을 생략한 target-typed new 문법입니다.
    private static readonly DateTimeOffset FixedTime =
        new(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 등록된 자체 테스트를 차례로 실행하고 통과 수를 출력합니다.
    /// </summary>
    /// <returns>모든 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    public static async Task RunAllAsync()
    {
        // (이름, 함수) 튜플 배열은 테스트 표시 이름과 실행할 메서드를 한 항목으로 묶습니다.
        (string Name, Func<Task> Run)[] tests =
        [
            ("잘못된 상태 전이", InvalidTransitionsReturnFailuresAsync),
            ("이벤트 재수화", RehydrateRestoresStateAsync),
            ("stale 조회 후 따라잡기", StaleQueryThenCatchUpAsync),
            ("중복 프로젝션 멱등성", DuplicateProjectionIsIdempotentAsync),
            ("전체 재생으로 읽기 모델 재구축", ReplayRebuildsEquivalentReadModelAsync),
            ("낙관적 충돌과 부분 쓰기 방지", OptimisticConflictWritesNothingAsync),
            ("동시 등록 낙관적 충돌", ConcurrentRegistersProduceOneConflictAsync),
            ("결정적인 전역 순서", GlobalOrderIsDeterministicAsync),
            ("동시 프로젝터 수렴", ConcurrentProjectionRunnersConvergeAsync),
            ("취소 시 쓰기 없음", CancellationWritesNothingAsync),
            ("잘못된 값/봉투 계약 거부", MalformedContractsAreRejectedAsync),
            ("불가능한 이벤트 기록 거부", InvalidHistoryIsRejectedAsync),
            ("조회 컬렉션 방어적 복사", ReturnedCollectionsAreDefensiveAsync),
        ];

        var passed = 0;
        foreach (var test in tests)
        {
            await RunTestAsync(test.Name, test.Run).ConfigureAwait(false);
            passed++;
        }

        Console.WriteLine($"자체 테스트 통과: {passed}/{tests.Length}");
    }

    /// <summary>
    /// 테스트 하나를 실행하고 성공 이름을 출력하며, 실패는 테스트 이름을 덧붙여 다시 던집니다.
    /// </summary>
    /// <param name="name">콘솔과 실패 메시지에 표시할 테스트 이름입니다.</param>
    /// <param name="test">실제로 검증을 수행하는 비동기 함수입니다.</param>
    /// <returns>테스트가 통과하면 완료되는 비동기 작업입니다.</returns>
    private static async Task RunTestAsync(string name, Func<Task> test)
    {
        try
        {
            await test().ConfigureAwait(false);
            Console.WriteLine($"[PASS] {name}");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"[FAIL] {name}: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// 등록 전 시작, 실행 전 완료, 중복 시작, 종료 후 변경이 모두 이벤트 없이 실패하는지 확인합니다.
    /// </summary>
    /// <returns>동기 검증을 마친 완료 Task를 반환합니다.</returns>
    private static Task InvalidTransitionsReturnFailuresAsync()
    {
        var empty = Deployment.Rehydrate("DEPLOY-INVALID", []);
        var beforeRegister = empty.Start(FixedTime);
        AssertFailure(beforeRegister, "deployment.cannot_start");

        var registeredEvent = new DeploymentRegistered(
            "DEPLOY-INVALID",
            "staging",
            "api:1.0.0",
            FixedTime);
        var registered = Deployment.Rehydrate("DEPLOY-INVALID", [registeredEvent]);
        AssertFailure(registered.Succeed(FixedTime.AddMinutes(1)), "deployment.cannot_succeed");

        var startedEvent = new DeploymentStarted("DEPLOY-INVALID", FixedTime.AddMinutes(1));
        var running = Deployment.Rehydrate("DEPLOY-INVALID", [registeredEvent, startedEvent]);
        AssertFailure(running.Start(FixedTime.AddMinutes(2)), "deployment.cannot_start");

        var succeededEvent = new DeploymentSucceeded("DEPLOY-INVALID", FixedTime.AddMinutes(2));
        var succeeded = Deployment.Rehydrate(
            "DEPLOY-INVALID",
            [registeredEvent, startedEvent, succeededEvent]);
        AssertFailure(
            succeeded.Fail("이미 끝난 배포", FixedTime.AddMinutes(3)),
            "deployment.cannot_fail");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 등록·시작·실패 이벤트를 재생하면 모든 필드와 버전이 마지막 사실대로 복원되는지 확인합니다.
    /// </summary>
    /// <returns>동기 검증을 마친 완료 Task를 반환합니다.</returns>
    private static Task RehydrateRestoresStateAsync()
    {
        DeploymentEvent[] history =
        [
            new DeploymentRegistered("DEPLOY-REHYDRATE", "production", "api:7.1", FixedTime),
            new DeploymentStarted("DEPLOY-REHYDRATE", FixedTime.AddMinutes(1)),
            new DeploymentFailed("DEPLOY-REHYDRATE", "롤백 필요", FixedTime.AddMinutes(2)),
        ];

        var deployment = Deployment.Rehydrate("DEPLOY-REHYDRATE", history);

        Assert(deployment.IsRegistered, "등록 상태가 복원되어야 합니다.");
        AssertEqual("production", deployment.Environment, "환경이 복원되어야 합니다.");
        AssertEqual("api:7.1", deployment.ArtifactVersion, "산출물 버전이 복원되어야 합니다.");
        AssertEqual(DeploymentStatus.Failed, deployment.Status, "실패 상태가 복원되어야 합니다.");
        AssertEqual("롤백 필요", deployment.FailureReason, "실패 이유가 복원되어야 합니다.");
        AssertEqual(3, deployment.Version, "적용한 이벤트 수가 스트림 버전이어야 합니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 명령 직후 읽기 모델은 비어 있고 지연이 보이며, Runner 실행 뒤 최신 상태와 지연 0이 되는지 확인합니다.
    /// </summary>
    /// <returns>명령·조회·프로젝션 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task StaleQueryThenCatchUpAsync()
    {
        var eventStore = new InMemoryDeploymentEventStore();
        var readModel = new InMemoryDeploymentReadModel();
        var commands = new DeploymentCommandService(eventStore);
        var queries = new DeploymentQueryService(readModel, eventStore);
        var runner = CreateRunner(eventStore, readModel);

        RequireSuccess(await commands.RegisterAsync(
            "DEPLOY-LAG",
            "production",
            "worker:3",
            FixedTime,
            CancellationToken.None).ConfigureAwait(false));
        RequireSuccess(await commands.StartAsync(
            "DEPLOY-LAG",
            FixedTime.AddMinutes(1),
            CancellationToken.None).ConfigureAwait(false));

        var stale = await queries.GetDashboardAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(0, stale.Rows.Count, "투영 전 조회에는 행이 없어야 합니다.");
        AssertEqual(0L, stale.Checkpoint, "투영 전 체크포인트는 0이어야 합니다.");
        AssertEqual(2L, stale.Lag, "투영 전에는 이벤트 두 개만큼 지연되어야 합니다.");

        var report = await runner.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);
        var current = await queries.GetDashboardAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(2, report.AppliedCount, "두 이벤트를 새로 적용해야 합니다.");
        AssertEqual(0L, report.RemainingLag, "Runner가 원본을 모두 따라잡아야 합니다.");
        AssertEqual(1, current.Rows.Count, "투영 후 행 하나가 보여야 합니다.");
        AssertEqual(DeploymentStatus.Running, current.Rows[0].Status, "최신 Running 상태가 보여야 합니다.");
        AssertEqual(0L, current.Lag, "최신 조회의 지연은 0이어야 합니다.");
    }

    /// <summary>
    /// 같은 전역 이벤트를 두 번씩 전달해도 행이 한 번씩만 바뀌고 체크포인트가 정확한지 확인합니다.
    /// </summary>
    /// <returns>중복 이벤트 프로젝션 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task DuplicateProjectionIsIdempotentAsync()
    {
        var eventStore = new InMemoryDeploymentEventStore();
        var commands = new DeploymentCommandService(eventStore);
        await SeedSucceededAsync(commands, "DEPLOY-DUPLICATE", FixedTime).ConfigureAwait(false);
        var original = await eventStore.ReadAllAfterAsync(0, CancellationToken.None).ConfigureAwait(false);

        // =>는 각 envelope를 두 개짜리 배열로 바꾸는 람다 식이며, 동일 재전달 입력을 만들려고 LINQ에 전달합니다.
        var duplicated = original.Events
            .SelectMany(envelope => new[] { envelope, envelope })
            .ToArray();
        var batch = new GlobalEventBatch(original.SourceHeadPosition, duplicated);
        var readModel = new InMemoryDeploymentReadModel();
        var runner = CreateRunner(eventStore, readModel);

        var first = await runner.ProjectBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
        var second = await runner.ProjectBatchAsync(batch, CancellationToken.None).ConfigureAwait(false);
        var state = await readModel.ReadStateAsync(CancellationToken.None).ConfigureAwait(false);

        AssertEqual(3, first.AppliedCount, "서로 다른 세 이벤트만 적용되어야 합니다.");
        AssertEqual(3, first.DuplicateCount, "첫 배치 안의 재전달 세 개를 무시해야 합니다.");
        AssertEqual(0, second.AppliedCount, "같은 배치 재실행은 새 변경을 만들면 안 됩니다.");
        AssertEqual(6, second.DuplicateCount, "재실행의 모든 항목이 동일 중복이어야 합니다.");
        AssertEqual(3L, state.Checkpoint, "체크포인트는 실제 마지막 전역 위치여야 합니다.");
        AssertEqual(DeploymentStatus.Succeeded, state.Rows[0].Status, "최종 성공 상태가 유지되어야 합니다.");
    }

    /// <summary>
    /// 같은 원본 로그를 새 읽기 모델에 처음부터 재생하면 기존 읽기 모델과 같은 결과가 되는지 확인합니다.
    /// </summary>
    /// <returns>두 읽기 모델 비교가 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task ReplayRebuildsEquivalentReadModelAsync()
    {
        var eventStore = new InMemoryDeploymentEventStore();
        var commands = new DeploymentCommandService(eventStore);
        await SeedSucceededAsync(commands, "DEPLOY-REPLAY-A", FixedTime).ConfigureAwait(false);
        await SeedFailedAsync(commands, "DEPLOY-REPLAY-B", FixedTime.AddHours(1)).ConfigureAwait(false);

        var firstModel = new InMemoryDeploymentReadModel();
        var rebuiltModel = new InMemoryDeploymentReadModel();
        await CreateRunner(eventStore, firstModel).RunOnceAsync(CancellationToken.None).ConfigureAwait(false);
        await CreateRunner(eventStore, rebuiltModel).RunOnceAsync(CancellationToken.None).ConfigureAwait(false);

        var first = await firstModel.ReadStateAsync(CancellationToken.None).ConfigureAwait(false);
        var rebuilt = await rebuiltModel.ReadStateAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(first.Checkpoint, rebuilt.Checkpoint, "재구축 체크포인트가 원본과 같아야 합니다.");
        AssertEqual(first.Rows.Count, rebuilt.Rows.Count, "재구축 행 수가 원본과 같아야 합니다.");
        for (var index = 0; index < first.Rows.Count; index++)
        {
            AssertEqual(first.Rows[index], rebuilt.Rows[index], "재구축한 각 행의 모든 값이 같아야 합니다.");
        }
    }

    /// <summary>
    /// 오래된 예상 버전으로 두 이벤트를 append하면 예외가 나고 어느 이벤트도 부분 저장되지 않는지 확인합니다.
    /// </summary>
    /// <returns>충돌 전후 스트림과 전역 로그 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task OptimisticConflictWritesNothingAsync()
    {
        var eventStore = new InMemoryDeploymentEventStore();
        await eventStore.AppendAsync(
            "DEPLOY-CONFLICT",
            0,
            [new DeploymentRegistered("DEPLOY-CONFLICT", "test", "api:1", FixedTime)],
            CancellationToken.None).ConfigureAwait(false);
        var headBefore = await eventStore.GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false);

        await AssertThrowsAsync<ExpectedVersionConflictException>(
            () => eventStore.AppendAsync(
                "DEPLOY-CONFLICT",
                0,
                [
                    new DeploymentStarted("DEPLOY-CONFLICT", FixedTime.AddMinutes(1)),
                    new DeploymentSucceeded("DEPLOY-CONFLICT", FixedTime.AddMinutes(2)),
                ],
                CancellationToken.None),
            "오래된 예상 버전은 충돌 예외여야 합니다.").ConfigureAwait(false);

        var slice = await eventStore.ReadStreamAsync("DEPLOY-CONFLICT", CancellationToken.None).ConfigureAwait(false);
        var headAfter = await eventStore.GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(1, slice.CurrentVersion, "충돌 뒤 스트림에는 기존 등록만 남아야 합니다.");
        AssertEqual(headBefore, headAfter, "충돌은 전역 로그에도 부분 쓰기를 남기면 안 됩니다.");
    }

    /// <summary>
    /// 같은 빈 스트림을 읽은 두 등록 명령을 동시에 append시켜 하나만 성공하고 하나는 낙관적 충돌이 되는지 확인합니다.
    /// </summary>
    /// <returns>두 명령과 최종 스트림 상태 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task ConcurrentRegistersProduceOneConflictAsync()
    {
        var innerStore = new InMemoryDeploymentEventStore();
        var coordinatedStore = new PairCoordinatedEventStore(
            innerStore,
            coordinateEmptyStreamReads: true,
            coordinateGlobalReads: false);
        var firstService = new DeploymentCommandService(coordinatedStore);
        var secondService = new DeploymentCommandService(coordinatedStore);
        // RunContinuationsAsynchronously는 게이트를 연 스레드 하나가 두 작업을 연달아 독점하지 않도록 후속 실행을 작업 큐로 보냅니다.
        var startGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // 테스트 회귀로 두 번째 참가자가 배리어에 못 와도 자동화가 무한 대기하지 않도록 10초의 안전 경계를 둡니다.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var firstTask = Task.Run(async () =>
        {
            await startGate.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            return await firstService.RegisterAsync(
                "DEPLOY-RACE",
                "production",
                "api:first",
                FixedTime,
                timeout.Token).ConfigureAwait(false);
        });
        var secondTask = Task.Run(async () =>
        {
            await startGate.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            return await secondService.RegisterAsync(
                "DEPLOY-RACE",
                "production",
                "api:second",
                FixedTime,
                timeout.Token).ConfigureAwait(false);
        });

        startGate.SetResult(true);
        var results = await AwaitPairWithTimeoutAsync(
            firstTask,
            secondTask,
            timeout.Token,
            "동시 등록 참가자 두 개가 배리어를 통과하지 못했습니다.").ConfigureAwait(false);
        AssertEqual(1, results.Count(result => result.IsSuccess), "동시 등록 중 정확히 하나만 성공해야 합니다.");
        AssertEqual(
            1,
            results.Count(result => result.ErrorCode == "deployment.concurrency_conflict"),
            "패배한 등록 하나는 낙관적 충돌로 매핑되어야 합니다.");

        var slice = await innerStore.ReadStreamAsync("DEPLOY-RACE", CancellationToken.None).ConfigureAwait(false);
        var head = await innerStore.GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(1, slice.CurrentVersion, "동시 등록 뒤 스트림에는 이벤트 하나만 있어야 합니다.");
        AssertEqual(1L, head, "동시 등록 충돌은 전역 위치를 추가로 소비하면 안 됩니다.");
    }

    /// <summary>
    /// 서로 다른 스트림을 번갈아 저장해도 전역 위치가 호출 순서대로 1씩 증가하는지 확인합니다.
    /// </summary>
    /// <returns>전역 순서 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task GlobalOrderIsDeterministicAsync()
    {
        var eventStore = new InMemoryDeploymentEventStore();
        var commands = new DeploymentCommandService(eventStore);
        RequireSuccess(await commands.RegisterAsync(
            "DEPLOY-ORDER-A", "test", "a:1", FixedTime, CancellationToken.None).ConfigureAwait(false));
        RequireSuccess(await commands.RegisterAsync(
            "DEPLOY-ORDER-B", "test", "b:1", FixedTime, CancellationToken.None).ConfigureAwait(false));
        RequireSuccess(await commands.StartAsync(
            "DEPLOY-ORDER-A", FixedTime.AddMinutes(1), CancellationToken.None).ConfigureAwait(false));
        RequireSuccess(await commands.StartAsync(
            "DEPLOY-ORDER-B", FixedTime.AddMinutes(1), CancellationToken.None).ConfigureAwait(false));

        var batch = await eventStore.ReadAllAfterAsync(0, CancellationToken.None).ConfigureAwait(false);
        AssertSequenceEqual(
            new long[] { 1, 2, 3, 4 },
            batch.Events.Select(envelope => envelope.GlobalPosition),
            "전역 위치는 빈틈없이 증가해야 합니다.");
        AssertSequenceEqual(
            new[] { "DEPLOY-ORDER-A", "DEPLOY-ORDER-B", "DEPLOY-ORDER-A", "DEPLOY-ORDER-B" },
            batch.Events.Select(envelope => envelope.StreamId),
            "전역 순서는 append 호출 순서를 보존해야 합니다.");
    }

    /// <summary>
    /// 같은 전역 배치를 동시에 읽은 두 Runner가 경쟁해도 예외나 중복 행 없이 하나의 최신 읽기 모델로 수렴하는지 확인합니다.
    /// </summary>
    /// <returns>두 Runner와 최종 체크포인트·행 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task ConcurrentProjectionRunnersConvergeAsync()
    {
        var innerStore = new InMemoryDeploymentEventStore();
        var commands = new DeploymentCommandService(innerStore);
        await SeedSucceededAsync(commands, "DEPLOY-PROJECTION-A", FixedTime).ConfigureAwait(false);
        await SeedFailedAsync(commands, "DEPLOY-PROJECTION-B", FixedTime.AddHours(1)).ConfigureAwait(false);
        var coordinatedStore = new PairCoordinatedEventStore(
            innerStore,
            coordinateEmptyStreamReads: false,
            coordinateGlobalReads: true);
        var readModel = new InMemoryDeploymentReadModel();
        var firstRunner = CreateRunner(coordinatedStore, readModel);
        var secondRunner = CreateRunner(coordinatedStore, readModel);
        var startGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var firstTask = Task.Run(async () =>
        {
            await startGate.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            return await firstRunner.RunOnceAsync(timeout.Token).ConfigureAwait(false);
        });
        var secondTask = Task.Run(async () =>
        {
            await startGate.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            return await secondRunner.RunOnceAsync(timeout.Token).ConfigureAwait(false);
        });

        startGate.SetResult(true);
        var reports = await AwaitPairWithTimeoutAsync(
            firstTask,
            secondTask,
            timeout.Token,
            "동시 프로젝터 두 개가 배리어를 통과하지 못했습니다.").ConfigureAwait(false);
        var state = await readModel.ReadStateAsync(CancellationToken.None).ConfigureAwait(false);
        var head = await innerStore.GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false);

        AssertEqual(6, reports.Sum(report => report.AppliedCount), "각 원본 이벤트는 두 Runner 중 하나만 적용해야 합니다.");
        AssertEqual(6, reports.Sum(report => report.DuplicateCount), "다른 Runner의 같은 이벤트는 중복으로 확인되어야 합니다.");
        AssertEqual(head, state.Checkpoint, "동시 Runner 뒤 체크포인트가 원본 끝과 같아야 합니다.");
        AssertEqual(6L, head, "두 배포의 이벤트 여섯 개가 원본 로그에 있어야 합니다.");
        AssertEqual(2, state.Rows.Count, "배포별로 정확히 한 행만 남아야 합니다.");
        AssertEqual(DeploymentStatus.Succeeded, state.Rows[0].Status, "첫 배포의 성공 상태가 보존되어야 합니다.");
        AssertEqual(DeploymentStatus.Failed, state.Rows[1].Status, "둘째 배포의 실패 상태가 보존되어야 합니다.");
    }

    /// <summary>
    /// 이미 취소된 토큰으로 append하면 OperationCanceledException이 전파되고 저장소가 비어 있는지 확인합니다.
    /// </summary>
    /// <returns>취소와 무변경 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task CancellationWritesNothingAsync()
    {
        var eventStore = new InMemoryDeploymentEventStore();
        // using var는 메서드가 끝날 때 CancellationTokenSource.Dispose를 자동 호출해 자원을 정리합니다.
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await AssertThrowsAsync<OperationCanceledException>(
            () => eventStore.AppendAsync(
                "DEPLOY-CANCEL",
                0,
                [new DeploymentRegistered("DEPLOY-CANCEL", "test", "api:1", FixedTime)],
                cancellation.Token),
            "취소된 append는 OperationCanceledException이어야 합니다.").ConfigureAwait(false);

        var slice = await eventStore.ReadStreamAsync("DEPLOY-CANCEL", CancellationToken.None).ConfigureAwait(false);
        var head = await eventStore.GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual(0, slice.CurrentVersion, "취소된 스트림은 여전히 비어 있어야 합니다.");
        AssertEqual(0L, head, "취소는 전역 위치도 소비하면 안 됩니다.");
    }

    /// <summary>
    /// null/공백 필수 값, 0 버전, 스트림 불일치 같은 잘못된 공개 계약이 즉시 거부되는지 확인합니다.
    /// </summary>
    /// <returns>동기 계약 검증을 마친 완료 Task를 반환합니다.</returns>
    private static Task MalformedContractsAreRejectedAsync()
    {
        AssertThrows<ArgumentException>(
            () => new DeploymentRegistered(null, "test", "api:1", FixedTime),
            "null 배포 ID는 이벤트가 될 수 없습니다.");
        AssertThrows<ArgumentException>(
            () => new DeploymentFailed("DEPLOY-BAD", "   ", FixedTime),
            "공백 실패 이유는 이벤트가 될 수 없습니다.");

        var valid = new DeploymentRegistered("DEPLOY-BAD", "test", "api:1", FixedTime);
        AssertThrows<ArgumentOutOfRangeException>(
            () => new EventEnvelope("DEPLOY-BAD", 0, 1, valid),
            "스트림 버전 0인 봉투는 거부되어야 합니다.");
        AssertThrows<ArgumentException>(
            () => new EventEnvelope("DEPLOY-BAD", 2, 1, valid),
            "스트림 버전은 전역 위치보다 클 수 없습니다.");
        AssertThrows<ArgumentException>(
            () => new EventEnvelope("OTHER", 1, 1, valid),
            "봉투와 이벤트의 스트림 ID 불일치는 거부되어야 합니다.");
        var firstEnvelope = new EventEnvelope("DEPLOY-BAD", 1, 1, valid);
        var changedEnvelope = new EventEnvelope(
            "DEPLOY-BAD",
            1,
            1,
            new DeploymentRegistered("DEPLOY-BAD", "changed", "api:1", FixedTime));
        AssertThrows<ArgumentException>(
            () => new GlobalEventBatch(1, [firstEnvelope, changedEnvelope]),
            "같은 전역 위치에 다른 계약을 가진 재전달은 배치 생성부터 거부되어야 합니다.");
        AssertThrows<ArgumentException>(
            () => new DashboardRow(
                "DEPLOY-BAD",
                "test",
                "api:1",
                DeploymentStatus.Failed,
                null,
                FixedTime,
                1,
                1),
            "실패 행에는 실패 이유가 필요합니다.");
        AssertThrows<ArgumentOutOfRangeException>(
            () => new DashboardRow(
                "DEPLOY-BAD",
                "test",
                "api:1",
                (DeploymentStatus)999,
                null,
                FixedTime,
                1,
                1),
            "정의되지 않은 상태 숫자는 읽기 모델에 들어갈 수 없습니다.");
        AssertThrows<ArgumentException>(
            () => new DashboardRow(
                "DEPLOY-BAD",
                "test",
                "api:1",
                DeploymentStatus.Running,
                null,
                FixedTime,
                2,
                1),
            "행의 스트림 버전은 전역 위치를 넘을 수 없습니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 시작 이벤트만 있는 기록과 새 스트림에 시작을 직접 append하는 시도가 불변식 위반으로 거부되는지 확인합니다.
    /// </summary>
    /// <returns>재수화와 저장소 불변식 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task InvalidHistoryIsRejectedAsync()
    {
        AssertThrows<InvalidDataException>(
            () => Deployment.Rehydrate(
                "DEPLOY-HISTORY",
                [new DeploymentStarted("DEPLOY-HISTORY", FixedTime)]),
            "등록보다 먼저 시작한 기록은 복원할 수 없어야 합니다.");

        var eventStore = new InMemoryDeploymentEventStore();
        await AssertThrowsAsync<InvalidDataException>(
            () => eventStore.AppendAsync(
                "DEPLOY-HISTORY",
                0,
                [new DeploymentStarted("DEPLOY-HISTORY", FixedTime)],
                CancellationToken.None),
            "저장소도 불가능한 이벤트 기록을 받아들이면 안 됩니다.").ConfigureAwait(false);
        await AssertThrowsAsync<InvalidDataException>(
            () => eventStore.AppendAsync(
                "DEPLOY-HISTORY-BATCH",
                0,
                [
                    new DeploymentRegistered("DEPLOY-HISTORY-BATCH", "test", "api:1", FixedTime),
                    new DeploymentSucceeded("DEPLOY-HISTORY-BATCH", FixedTime.AddMinutes(1)),
                ],
                CancellationToken.None),
            "유효한 첫 이벤트 뒤의 잘못된 둘째 이벤트도 batch 전체를 거부해야 합니다.").ConfigureAwait(false);
        var rejectedBatch = await eventStore
            .ReadStreamAsync("DEPLOY-HISTORY-BATCH", CancellationToken.None)
            .ConfigureAwait(false);
        AssertEqual(0, rejectedBatch.CurrentVersion, "잘못된 둘째 이벤트가 첫 이벤트만 부분 저장하면 안 됩니다.");
        AssertEqual(
            0L,
            await eventStore.GetHeadPositionAsync(CancellationToken.None).ConfigureAwait(false),
            "거부한 불변식 위반 이벤트는 전역 로그를 바꾸면 안 됩니다.");
    }

    /// <summary>
    /// 조회 결과의 배열 항목을 바꿔도 이벤트 저장소와 읽기 모델의 내부 상태가 변하지 않는지 확인합니다.
    /// </summary>
    /// <returns>두 저장소의 방어적 복사 검증이 끝나면 완료되는 비동기 작업입니다.</returns>
    private static async Task ReturnedCollectionsAreDefensiveAsync()
    {
        var eventStore = new InMemoryDeploymentEventStore();
        var commands = new DeploymentCommandService(eventStore);
        RequireSuccess(await commands.RegisterAsync(
            "DEPLOY-COPY", "test", "api:1", FixedTime, CancellationToken.None).ConfigureAwait(false));
        var slice = await eventStore.ReadStreamAsync("DEPLOY-COPY", CancellationToken.None).ConfigureAwait(false);
        var exposedEvents = (EventEnvelope[])slice.Events;
        exposedEvents[0] = new EventEnvelope(
            "DEPLOY-COPY",
            1,
            99,
            new DeploymentRegistered("DEPLOY-COPY", "changed", "changed", FixedTime));

        var reread = await eventStore.ReadStreamAsync("DEPLOY-COPY", CancellationToken.None).ConfigureAwait(false);
        var originalRegistration = (DeploymentRegistered)reread.Events[0].Event;
        AssertEqual("test", originalRegistration.Environment, "외부 배열 변경이 이벤트 저장소에 반영되면 안 됩니다.");
        AssertEqual(1L, reread.Events[0].GlobalPosition, "외부 배열 변경이 전역 위치를 바꾸면 안 됩니다.");

        var readModel = new InMemoryDeploymentReadModel();
        await CreateRunner(eventStore, readModel).RunOnceAsync(CancellationToken.None).ConfigureAwait(false);
        var firstState = await readModel.ReadStateAsync(CancellationToken.None).ConfigureAwait(false);
        var exposedRows = (DashboardRow[])firstState.Rows;
        exposedRows[0] = new DashboardRow(
            "DEPLOY-COPY",
            "changed",
            "changed",
            DeploymentStatus.Registered,
            null,
            FixedTime,
            1,
            1);
        var secondState = await readModel.ReadStateAsync(CancellationToken.None).ConfigureAwait(false);
        AssertEqual("test", secondState.Rows[0].Environment, "외부 행 배열 변경이 읽기 모델에 반영되면 안 됩니다.");
    }

    /// <summary>
    /// 고정 시각으로 등록·시작·성공 명령을 실행해 테스트 원본 이벤트를 준비합니다.
    /// </summary>
    /// <param name="commands">이벤트 저장소에 연결된 명령 서비스입니다.</param>
    /// <param name="deploymentId">다른 테스트와 충돌하지 않을 배포 식별자입니다.</param>
    /// <param name="start">첫 등록 이벤트에 사용할 고정 UTC 시각입니다.</param>
    /// <returns>세 명령이 모두 성공하면 완료되는 비동기 작업입니다.</returns>
    private static async Task SeedSucceededAsync(
        DeploymentCommandService commands,
        string deploymentId,
        DateTimeOffset start)
    {
        RequireSuccess(await commands.RegisterAsync(
            deploymentId, "production", "api:1", start, CancellationToken.None).ConfigureAwait(false));
        RequireSuccess(await commands.StartAsync(
            deploymentId, start.AddMinutes(1), CancellationToken.None).ConfigureAwait(false));
        RequireSuccess(await commands.SucceedAsync(
            deploymentId, start.AddMinutes(2), CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 고정 시각으로 등록·시작·실패 명령을 실행해 테스트 원본 이벤트를 준비합니다.
    /// </summary>
    /// <param name="commands">이벤트 저장소에 연결된 명령 서비스입니다.</param>
    /// <param name="deploymentId">다른 테스트와 충돌하지 않을 배포 식별자입니다.</param>
    /// <param name="start">첫 등록 이벤트에 사용할 고정 UTC 시각입니다.</param>
    /// <returns>세 명령이 모두 성공하면 완료되는 비동기 작업입니다.</returns>
    private static async Task SeedFailedAsync(
        DeploymentCommandService commands,
        string deploymentId,
        DateTimeOffset start)
    {
        RequireSuccess(await commands.RegisterAsync(
            deploymentId, "staging", "web:2", start, CancellationToken.None).ConfigureAwait(false));
        RequireSuccess(await commands.StartAsync(
            deploymentId, start.AddMinutes(1), CancellationToken.None).ConfigureAwait(false));
        RequireSuccess(await commands.FailAsync(
            deploymentId, "테스트 실패", start.AddMinutes(2), CancellationToken.None).ConfigureAwait(false));
    }

    /// <summary>
    /// 주어진 저장소와 읽기 모델을 기본 프로젝션으로 연결한 Runner를 만듭니다.
    /// </summary>
    /// <param name="eventStore">전역 이벤트 원본입니다.</param>
    /// <param name="readModel">체크포인트와 행을 보존할 대상입니다.</param>
    /// <returns>즉시 실행 가능한 새 DeploymentProjectionRunner를 반환합니다.</returns>
    private static DeploymentProjectionRunner CreateRunner(
        IDeploymentEventStore eventStore,
        IDeploymentReadModel readModel)
    {
        return new DeploymentProjectionRunner(
            eventStore,
            readModel,
            new DeploymentDashboardProjection());
    }

    /// <summary>
    /// 테스트 준비 명령이 성공했는지 확인하고 실패라면 원인을 포함한 예외를 던집니다.
    /// </summary>
    /// <param name="result">성공해야 하는 명령 처리 결과입니다.</param>
    /// <returns>성공이면 아무 값도 반환하지 않습니다.</returns>
    private static void RequireSuccess(CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"테스트 준비 명령 실패: {result.ErrorCode} - {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// 도메인 판단이 지정한 오류 코드로 실패했고 저장할 이벤트가 없는지 확인합니다.
    /// </summary>
    /// <param name="decision">실패여야 하는 도메인 판단입니다.</param>
    /// <param name="expectedCode">예상하는 안정적인 업무 오류 코드입니다.</param>
    /// <returns>조건이 맞으면 아무 값도 반환하지 않습니다.</returns>
    private static void AssertFailure(DeploymentDecision decision, string expectedCode)
    {
        Assert(!decision.IsSuccess, "도메인 판단은 실패여야 합니다.");
        AssertEqual(expectedCode, decision.ErrorCode, "업무 오류 코드가 예상과 달라서는 안 됩니다.");
        AssertEqual(0, decision.Events.Count, "실패 판단은 저장할 이벤트를 만들면 안 됩니다.");
    }

    /// <summary>
    /// 조건이 거짓이면 설명을 담은 테스트 실패 예외를 던집니다.
    /// </summary>
    /// <param name="condition">반드시 true여야 하는 검증 조건입니다.</param>
    /// <param name="message">조건이 거짓일 때 원인을 알려 줄 설명입니다.</param>
    /// <returns>조건이 참이면 아무 값도 반환하지 않습니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 실제 값이 예상 값과 같은지 기본 타입 비교 규칙으로 확인합니다.
    /// </summary>
    /// <typeparam name="T">문자열, 숫자, enum, record 등 비교할 값의 타입입니다.</typeparam>
    /// <param name="expected">테스트가 기대하는 값입니다.</param>
    /// <param name="actual">실제 실행에서 얻은 값입니다.</param>
    /// <param name="message">값이 다를 때 보여 줄 설명입니다.</param>
    /// <returns>두 값이 같으면 아무 값도 반환하지 않습니다.</returns>
    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} expected={expected}, actual={actual}");
        }
    }

    /// <summary>
    /// 두 열거 결과가 같은 항목을 같은 순서로 가지는지 확인합니다.
    /// </summary>
    /// <typeparam name="T">각 순서에서 비교할 항목 타입입니다.</typeparam>
    /// <param name="expected">기대하는 항목 순서입니다.</param>
    /// <param name="actual">실제 실행에서 얻은 항목 순서입니다.</param>
    /// <param name="message">순서가 다를 때 보여 줄 설명입니다.</param>
    /// <returns>두 순서가 같으면 아무 값도 반환하지 않습니다.</returns>
    private static void AssertSequenceEqual<T>(
        IEnumerable<T> expected,
        IEnumerable<T> actual,
        string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 두 비동기 참가자를 함께 기다리되 테스트 안전 시간이 지나면 원인을 알 수 있는 실패로 바꿉니다.
    /// </summary>
    /// <typeparam name="T">각 참가자가 성공했을 때 돌려주는 결과 타입입니다.</typeparam>
    /// <param name="first">첫 번째 경쟁 참가자의 비동기 작업입니다.</param>
    /// <param name="second">두 번째 경쟁 참가자의 비동기 작업입니다.</param>
    /// <param name="timeoutToken">안전 시간이 끝났음을 알리는 테스트 전용 취소 신호입니다.</param>
    /// <param name="timeoutMessage">배리어 회귀를 설명할 구체적인 실패 메시지입니다.</param>
    /// <returns>두 작업이 시간 안에 끝나면 입력 순서대로 결과 두 개를 반환합니다.</returns>
    private static async Task<T[]> AwaitPairWithTimeoutAsync<T>(
        Task<T> first,
        Task<T> second,
        CancellationToken timeoutToken,
        string timeoutMessage)
    {
        try
        {
            return await Task.WhenAll(first, second)
                .WaitAsync(timeoutToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested)
        {
            // catch의 when 필터는 테스트용 제한 시간이 실제로 끝난 취소만 TimeoutException으로 번역합니다.
            throw new TimeoutException(timeoutMessage);
        }
    }

    /// <summary>
    /// 동기 동작이 지정한 예외 타입을 실제로 던지는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">발생해야 하는 예외 타입입니다.</typeparam>
    /// <param name="action">예외가 나야 하는 동기 동작입니다.</param>
    /// <param name="message">예외가 없을 때 보여 줄 테스트 실패 설명입니다.</param>
    /// <returns>기대한 예외를 확인하면 아무 값도 반환하지 않습니다.</returns>
    private static void AssertThrows<TException>(Action action, string message)
        // where는 타입 매개변수로 Exception 계열만 받을 수 있게 제한합니다.
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 비동기 동작이 지정한 예외 타입을 실제로 전파하는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">발생해야 하는 예외 타입입니다.</typeparam>
    /// <param name="action">예외가 나야 하는 비동기 동작입니다.</param>
    /// <param name="message">예외가 없을 때 보여 줄 테스트 실패 설명입니다.</param>
    /// <returns>기대한 예외를 확인하면 완료되는 비동기 작업입니다.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 두 호출이 같은 읽기 결과를 확보할 때까지 반환을 맞춰 실제 경쟁 조건을 결정적으로 만드는 테스트 전용 Decorator입니다.
    /// 운영 코드에 지연을 넣지 않고도 "둘 다 읽은 뒤 둘이 경쟁"하는 상황을 재현합니다.
    /// </summary>
    private sealed class PairCoordinatedEventStore : IDeploymentEventStore
    {
        private readonly IDeploymentEventStore _inner;
        private readonly bool _coordinateEmptyStreamReads;
        private readonly bool _coordinateGlobalReads;
        private readonly TaskCompletionSource<bool> _bothEmptyStreamReads =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _bothGlobalReads =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _emptyStreamReadCount;
        private int _globalReadCount;

        /// <summary>
        /// 실제 저장소와 두 종류의 읽기 배리어 사용 여부를 보관합니다.
        /// </summary>
        /// <param name="inner">모든 실제 읽기와 쓰기를 위임할 스레드 안전 이벤트 저장소입니다.</param>
        /// <param name="coordinateEmptyStreamReads">true면 빈 스트림 읽기 두 번이 모두 끝난 뒤 함께 반환합니다.</param>
        /// <param name="coordinateGlobalReads">true면 전역 배치 읽기 두 번이 모두 끝난 뒤 함께 반환합니다.</param>
        /// <remarks>테스트용 조정 상태를 초기화하는 생성자이므로 별도 반환값은 없습니다.</remarks>
        public PairCoordinatedEventStore(
            IDeploymentEventStore inner,
            bool coordinateEmptyStreamReads,
            bool coordinateGlobalReads)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _coordinateEmptyStreamReads = coordinateEmptyStreamReads;
            _coordinateGlobalReads = coordinateGlobalReads;
        }

        /// <summary>
        /// 실제 스트림을 먼저 읽고, 설정된 경우 두 호출이 모두 빈 스냅샷을 얻을 때까지 반환을 기다립니다.
        /// </summary>
        /// <param name="streamId">읽을 배포 스트림 식별자입니다.</param>
        /// <param name="cancellationToken">대기와 내부 조회를 중단할 취소 신호입니다.</param>
        /// <returns>두 경쟁 명령이 동일 시점에 읽은 스트림 스냅샷을 비동기로 반환합니다.</returns>
        public async Task<StreamSlice> ReadStreamAsync(
            string? streamId,
            CancellationToken cancellationToken)
        {
            var slice = await _inner.ReadStreamAsync(streamId, cancellationToken).ConfigureAwait(false);
            if (_coordinateEmptyStreamReads && slice.CurrentVersion == 0)
            {
                // Interlocked는 두 스레드가 동시에 증가시켜도 호출 수를 잃지 않는 원자 연산입니다.
                if (Interlocked.Increment(ref _emptyStreamReadCount) >= 2)
                {
                    _bothEmptyStreamReads.TrySetResult(true);
                }

                // WaitAsync는 Thread.Sleep 없이 게이트 신호 또는 취소를 비동기로 기다립니다.
                await _bothEmptyStreamReads.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return slice;
        }

        /// <summary>
        /// 실제 전역 배치를 먼저 읽고, 설정된 경우 두 Runner가 같은 배치를 확보할 때까지 반환을 기다립니다.
        /// </summary>
        /// <param name="positionExclusive">각 Runner가 이미 처리했다고 본 마지막 위치입니다.</param>
        /// <param name="cancellationToken">대기와 내부 조회를 중단할 취소 신호입니다.</param>
        /// <returns>두 Runner가 경쟁에 사용할 같은 원본 전역 배치를 비동기로 반환합니다.</returns>
        public async Task<GlobalEventBatch> ReadAllAfterAsync(
            long positionExclusive,
            CancellationToken cancellationToken)
        {
            var batch = await _inner
                .ReadAllAfterAsync(positionExclusive, cancellationToken)
                .ConfigureAwait(false);
            if (_coordinateGlobalReads)
            {
                if (Interlocked.Increment(ref _globalReadCount) >= 2)
                {
                    _bothGlobalReads.TrySetResult(true);
                }

                await _bothGlobalReads.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return batch;
        }

        /// <summary>
        /// 조정 없이 실제 저장소의 원자 append와 낙관적 버전 검사를 그대로 호출합니다.
        /// </summary>
        /// <param name="streamId">이벤트를 추가할 배포 스트림 식별자입니다.</param>
        /// <param name="expectedVersion">경쟁 명령이 읽었던 예상 스트림 버전입니다.</param>
        /// <param name="events">한꺼번에 추가할 도메인 이벤트입니다.</param>
        /// <param name="cancellationToken">내부 append를 중단할 취소 신호입니다.</param>
        /// <returns>실제 저장소가 만든 append 영수증 Task를 그대로 반환합니다.</returns>
        public Task<AppendReceipt> AppendAsync(
            string? streamId,
            int expectedVersion,
            IReadOnlyList<DeploymentEvent> events,
            CancellationToken cancellationToken)
        {
            return _inner.AppendAsync(streamId, expectedVersion, events, cancellationToken);
        }

        /// <summary>
        /// 조정 없이 실제 저장소의 현재 전역 끝 위치를 조회합니다.
        /// </summary>
        /// <param name="cancellationToken">내부 조회를 중단할 취소 신호입니다.</param>
        /// <returns>실제 저장소가 돌려주는 현재 전역 끝 위치 Task입니다.</returns>
        public Task<long> GetHeadPositionAsync(CancellationToken cancellationToken)
        {
            return _inner.GetHeadPositionAsync(cancellationToken);
        }
    }
}
