using DeploymentSseApi.Application;
using DeploymentSseApi.Domain;
using DeploymentSseApi.Infrastructure;

namespace DeploymentSseApi.SelfTesting;

/// <summary>
/// 외부 테스트 package 없이 Domain 전이, Application Service, replay/live stream, 취소와 backpressure 계약을 실행합니다.
/// </summary>
public static class SelfTestRunner
{
    private static int _passedAssertions;

    /// <summary>
    /// 결정적인 시계와 실제 메모리 Adapter를 사용해 모든 회귀 검증을 순서대로 실행합니다.
    /// </summary>
    /// <returns>모든 assertion이 통과하면 0, 하나라도 실패하면 1인 종료 코드가 든 Task를 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        _passedAssertions = 0;

        try
        {
            TestTransitionStrategy();
            await TestApplicationAndReplayAsync();
            await TestLiveStreamAndCancellationAsync();
            await TestSubscriberBroadcastAsync();
            await TestSlowConsumerIsolationAsync();
            Console.WriteLine($"SELF-TEST PASSED: {_passedAssertions} assertions");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SELF-TEST FAILED: {exception}");
            return 1;
        }
    }

    /// <summary>
    /// 첫 단계, 단계별 진행률, 정규화, 허용 전이와 terminal 불변식을 Strategy 수준에서 검증합니다.
    /// </summary>
    /// <returns>검증만 수행하므로 반환값은 없으며 어긋나면 예외를 던집니다.</returns>
    private static void TestTransitionStrategy()
    {
        var strategy = new DeploymentProgressTransitionStrategy();
        var nowUtc = new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);

        AssertError(
            strategy.CreateNext(
                Draft(null, "Queued", 0, "대기"),
                previous: null,
                nowUtc),
            "deployment.id.invalid",
            "null deploymentId를 거절한다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", null, 0, "대기"),
                previous: null,
                nowUtc),
            "deployment.stage.invalid",
            "null stage를 거절한다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "3", 100, "숫자 단계"),
                previous: null,
                nowUtc),
            "deployment.stage.invalid",
            "숫자 enum 문자열을 단계로 받아들이지 않는다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Building,Deploying", 100, "합성 단계"),
                previous: null,
                nowUtc),
            "deployment.stage.invalid",
            "쉼표로 합친 enum 이름을 하나의 단계로 받아들이지 않는다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Queued", 0, "   "),
                previous: null,
                nowUtc),
            "deployment.message.invalid",
            "공백 message를 거절한다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Queued", 0, new string('가', 201)),
                previous: null,
                nowUtc),
            "deployment.message.invalid",
            "200자를 넘는 message를 거절한다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Queued", 101, "대기"),
                previous: null,
                nowUtc),
            "deployment.percent.out_of_range",
            "100을 넘는 percent를 거절한다.");

        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Building", 10, "빌드 시작"),
                previous: null,
                nowUtc),
            "deployment.initial_stage.invalid",
            "첫 Event가 Building이면 거절한다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Queued", 1, "대기"),
                previous: null,
                nowUtc),
            "deployment.queued_percent.invalid",
            "Queued 진행률은 정확히 0이어야 한다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Building", 0, "빌드"),
                previous: new DeploymentProgressEvent(
                    1,
                    "RELEASE-A",
                    DeploymentStage.Queued,
                    0,
                    "대기",
                    nowUtc),
                nowUtc),
            "deployment.building_percent.invalid",
            "Building 진행률은 1부터 69 사이다.");

        var queued = RequireSuccess(
            strategy.CreateNext(
                Draft(" release-a ", " queued ", 0, " 배포 대기 "),
                previous: null,
                nowUtc),
            "유효한 첫 Queued Event를 만든다.");
        Assert(queued.EventId == 1, "첫 EventId는 1이다.");
        Assert(queued.DeploymentId == "release-a", "deploymentId의 앞뒤 공백을 제거하고 대소문자는 보존한다.");
        Assert(queued.Message == "배포 대기", "message 앞뒤 공백을 제거한다.");
        Assert(!queued.IsTerminal, "Queued는 terminal이 아니다.");

        var building = RequireSuccess(
            strategy.CreateNext(
                Draft("release-a", "Building", 40, "컴파일 중"),
                queued,
                nowUtc.AddSeconds(1)),
            "Queued에서 Building으로 이동한다.");
        Assert(building.EventId == 2, "다음 EventId는 정확히 1 증가한다.");

        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Deploying", 30, "배포 중"),
                building,
                nowUtc.AddSeconds(2)),
            "deployment.deploying_percent.invalid",
            "Deploying 진행률은 70부터 99 사이다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Completed", 99, "완료"),
                building,
                nowUtc.AddSeconds(2)),
            "deployment.completed_percent.invalid",
            "Completed 진행률은 정확히 100이어야 한다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Completed", 100, "단계를 건너뜀"),
                building,
                nowUtc.AddSeconds(2)),
            "deployment.transition.invalid",
            "Building에서 Completed로 단계를 건너뛸 수 없다.");

        var failed = RequireSuccess(
            strategy.CreateNext(
                Draft("release-a", "Failed", 40, "검증 실패"),
                building,
                nowUtc.AddSeconds(3)),
            "진행 중 Failed 종료는 허용한다.");
        Assert(failed.IsTerminal, "Failed는 terminal Event다.");
        AssertError(
            strategy.CreateNext(
                Draft("release-a", "Deploying", 80, "다시 배포"),
                failed,
                nowUtc.AddSeconds(4)),
            "deployment.already_terminal",
            "terminal Event 뒤에는 새 Event를 만들지 않는다.");
    }

    /// <summary>
    /// Application Service 게시, 예상 오류, 전체 replay와 Last-Event-ID 이후 replay를 검증합니다.
    /// </summary>
    /// <returns>비동기 게시와 stream 수집 검증이 끝났을 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestApplicationAndReplayAsync()
    {
        var harness = CreateHarness(subscriberCapacity: 8);
        var queued = await PublishRequiredAsync(
            harness,
            Draft(" release-main ", "Queued", 0, "대기"));
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        var building = await PublishRequiredAsync(
            harness,
            Draft("release-main", "Building", 35, "빌드"));
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        var deploying = await PublishRequiredAsync(
            harness,
            Draft("release-main", "Deploying", 80, "배포"));
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        var completed = await PublishRequiredAsync(
            harness,
            Draft("release-main", "Completed", 100, "완료"));

        Assert(queued.EventId == 1 && completed.EventId == 4,
            "Application Service가 배포별 EventId를 1부터 연속 저장한다.");
        Assert(building.OccurredAtUtc < deploying.OccurredAtUtc,
            "주입한 TimeProvider의 시각을 Event에 기록한다.");

        var terminalUpdate = await harness.Service.PublishAsync(
            Draft("release-main", "Failed", 99, "늦은 실패"),
            CancellationToken.None);
        AssertError(
            terminalUpdate,
            "deployment.already_terminal",
            "완료된 배포에 추가 게시를 거절한다.");

        var fullStream = await harness.Service.OpenStreamAsync(
            "release-main",
            lastEventId: null,
            CancellationToken.None);
        var fullReplay = await CollectAsync(
            RequireSuccess(fullStream, "존재하는 배포의 전체 stream을 연다.").Events,
            CancellationToken.None);
        Assert(fullReplay.Select(progressEvent => progressEvent.EventId)
                .SequenceEqual(new long[] { 1, 2, 3, 4 }),
            "처음 연결은 Event 1~4를 순서대로 replay한다.");

        var resumedStream = await harness.Service.OpenStreamAsync(
            "release-main",
            "2",
            CancellationToken.None);
        var resumedReplay = await CollectAsync(
            RequireSuccess(resumedStream, "Last-Event-ID가 있는 재연결 stream을 연다.").Events,
            CancellationToken.None);
        Assert(resumedReplay.Select(progressEvent => progressEvent.EventId)
                .SequenceEqual(new long[] { 3, 4 }),
            "Last-Event-ID 2 뒤의 Event만 엄격히 replay한다.");
        Assert(resumedReplay[^1].IsTerminal, "replay가 terminal Event를 포함하면 stream이 자연스럽게 끝난다.");

        AssertError(
            await harness.Service.OpenStreamAsync(
                "release-main",
                "-1",
                CancellationToken.None),
            "deployment.last_event_id.invalid",
            "음수 Last-Event-ID를 거절한다.");
        AssertError(
            await harness.Service.OpenStreamAsync(
                "release-main",
                "1,2",
                CancellationToken.None),
            "deployment.last_event_id.invalid",
            "여러 Last-Event-ID 값이 합쳐진 문자열을 거절한다.");
        AssertError(
            await harness.Service.OpenStreamAsync(
                "missing-release",
                null,
                CancellationToken.None),
            "deployment.not_found",
            "기록이 없는 배포 stream은 not-found다.");
    }

    /// <summary>
    /// replay가 끝난 뒤 live Event를 기다리고 terminal에서 종료하며 취소 시 구독을 제거하는지 검증합니다.
    /// </summary>
    /// <returns>live stream과 취소 수명 검증이 끝났을 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestLiveStreamAndCancellationAsync()
    {
        var harness = CreateHarness(subscriberCapacity: 4);
        await PublishRequiredAsync(
            harness,
            Draft("release-live", "Queued", 0, "대기"));

        using var streamCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var openResult = await harness.Service.OpenStreamAsync(
            "release-live",
            "1",
            streamCancellation.Token);
        var stream = RequireSuccess(openResult, "최신 Event 뒤 live stream을 연다.");
        Assert(harness.Repository.ActiveSubscriberCount == 1,
            "terminal 전 live stream은 bounded Channel 구독자 하나를 등록한다.");

        // await using은 async enumerator가 예외나 조기 종료에서도 DisposeAsync로 구독을 정리하게 합니다.
        await using var enumerator = stream.Events.GetAsyncEnumerator(streamCancellation.Token);
        var waitingForBuilding = enumerator.MoveNextAsync().AsTask();
        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        var building = await PublishRequiredAsync(
            harness,
            Draft("release-live", "Building", 25, "빌드"));
        Assert(await waitingForBuilding.WaitAsync(TimeSpan.FromSeconds(2)),
            "대기 중인 live reader가 새 Building Event를 받는다.");
        Assert(enumerator.Current.EventId == building.EventId,
            "live reader가 게시된 EventId를 그대로 받는다.");

        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await PublishRequiredAsync(
            harness,
            Draft("release-live", "Deploying", 75, "배포"));
        Assert(await enumerator.MoveNextAsync(), "live reader가 Deploying Event를 받는다.");
        Assert(enumerator.Current.Stage == DeploymentStage.Deploying,
            "두 번째 live Event 단계는 Deploying이다.");

        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await PublishRequiredAsync(
            harness,
            Draft("release-live", "Completed", 100, "완료"));
        Assert(await enumerator.MoveNextAsync(), "live reader가 terminal Completed Event를 받는다.");
        Assert(enumerator.Current.IsTerminal, "마지막 live Event는 terminal이다.");
        Assert(!await enumerator.MoveNextAsync(), "terminal Event 다음에는 stream이 끝난다.");
        Assert(harness.Repository.ActiveSubscriberCount == 0,
            "terminal 완료 시 Repository가 live 구독을 제거한다.");

        var cancellationHarness = CreateHarness(subscriberCapacity: 4);
        await PublishRequiredAsync(
            cancellationHarness,
            Draft("release-cancel", "Queued", 0, "대기"));
        using var cancellation = new CancellationTokenSource();
        var cancellationStream = await cancellationHarness.Service.OpenStreamAsync(
            "release-cancel",
            "1",
            cancellation.Token);
        Assert(cancellationStream.IsSuccess, "취소 검증용 live stream을 연다.");
        Assert(cancellationHarness.Repository.ActiveSubscriberCount == 1,
            "취소 전에는 구독자 하나가 있다.");
        cancellation.Cancel();
        Assert(cancellationHarness.Repository.ActiveSubscriberCount == 0,
            "RequestAborted에 해당하는 취소가 구독자를 즉시 제거한다.");
    }

    /// <summary>
    /// 같은 deployment를 구독한 두 소비자가 하나의 Event를 나눠 갖지 않고 각각 받는 broadcast 계약을 검증합니다.
    /// </summary>
    /// <returns>두 구독자의 수신과 조기 종료 정리가 끝났을 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestSubscriberBroadcastAsync()
    {
        var harness = CreateHarness(subscriberCapacity: 4);
        await PublishRequiredAsync(
            harness,
            Draft("release-broadcast", "Queued", 0, "대기"));

        var firstStream = RequireSuccess(
            await harness.Service.OpenStreamAsync(
                "release-broadcast",
                "1",
                CancellationToken.None),
            "첫 번째 live 구독을 연다.");
        var secondStream = RequireSuccess(
            await harness.Service.OpenStreamAsync(
                "release-broadcast",
                "1",
                CancellationToken.None),
            "두 번째 live 구독을 연다.");
        Assert(harness.Repository.ActiveSubscriberCount == 2,
            "subscriber마다 독립된 Channel 하나가 등록된다.");

        await using (var first = firstStream.Events.GetAsyncEnumerator())
        await using (var second = secondStream.Events.GetAsyncEnumerator())
        {
            var firstMove = first.MoveNextAsync().AsTask();
            var secondMove = second.MoveNextAsync().AsTask();
            var building = await PublishRequiredAsync(
                harness,
                Draft("release-broadcast", "Building", 20, "빌드"));

            Assert(await firstMove.WaitAsync(TimeSpan.FromSeconds(2)),
                "첫 번째 subscriber가 live Event를 받는다.");
            Assert(await secondMove.WaitAsync(TimeSpan.FromSeconds(2)),
                "두 번째 subscriber가 같은 live Event를 받는다.");
            Assert(first.Current.EventId == building.EventId &&
                second.Current.EventId == building.EventId,
                "work queue처럼 나눠 갖지 않고 두 subscriber 모두 같은 EventId를 받는다.");
        }

        Assert(harness.Repository.ActiveSubscriberCount == 0,
            "두 enumerator를 조기 종료하면 두 구독 모두 정리된다.");
    }

    /// <summary>
    /// 느린 소비자 한 명의 bounded Channel이 가득 차도 게시 자체와 다른 저장 상태는 성공하는지 검증합니다.
    /// </summary>
    /// <returns>backpressure 격리 검증이 끝났을 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestSlowConsumerIsolationAsync()
    {
        var harness = CreateHarness(subscriberCapacity: 1);
        await PublishRequiredAsync(
            harness,
            Draft("release-slow", "Queued", 0, "대기"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var openResult = await harness.Service.OpenStreamAsync(
            "release-slow",
            "1",
            cancellation.Token);
        Assert(openResult.IsSuccess, "느린 소비자 검증용 live stream을 연다.");
        Assert(harness.Repository.ActiveSubscriberCount == 1,
            "bounded Channel 구독자가 등록된다.");

        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        await PublishRequiredAsync(
            harness,
            Draft("release-slow", "Building", 10, "첫 live Event"));
        Assert(harness.Repository.ActiveSubscriberCount == 1,
            "용량 1 Channel은 첫 live Event를 보관한다.");

        harness.Clock.Advance(TimeSpan.FromSeconds(1));
        var second = await PublishRequiredAsync(
            harness,
            Draft("release-slow", "Building", 20, "두 번째 live Event"));
        Assert(second.EventId == 3, "느린 소비자와 무관하게 두 번째 Event를 저장한다.");
        Assert(harness.Repository.ActiveSubscriberCount == 0,
            "가득 찬 구독자만 종료해 서버 메모리의 무한 증가를 막는다.");

        var latest = await harness.Repository.GetLatestAsync(
            "release-slow",
            CancellationToken.None);
        Assert(latest?.EventId == 3,
            "slow consumer 연결 종료 뒤에도 append-only 기록은 보존된다.");
    }

    /// <summary>
    /// 동일한 기본 의존성과 지정한 Channel 용량으로 테스트 조립 묶음을 만듭니다.
    /// </summary>
    /// <param name="subscriberCapacity">느린 소비자 경계를 재현할 구독자별 버퍼 크기입니다.</param>
    /// <returns>Application Service, 실제 Repository Adapter, 결정적 시계를 함께 반환합니다.</returns>
    private static TestHarness CreateHarness(int subscriberCapacity)
    {
        var repository = new InMemoryDeploymentProgressRepository(subscriberCapacity);
        var clock = new AdjustableTimeProvider(
            new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));
        var service = new DeploymentProgressApplicationService(
            repository,
            new DeploymentProgressTransitionStrategy(),
            clock);
        return new TestHarness(service, repository, clock);
    }

    /// <summary>
    /// 반복되는 테스트 입력을 짧고 같은 형태로 만드는 Domain draft factory입니다.
    /// </summary>
    /// <param name="deploymentId">정규화 전 배포 식별자입니다.</param>
    /// <param name="stage">검증할 단계 문자열입니다.</param>
    /// <param name="percent">검증할 진행률입니다.</param>
    /// <param name="message">검증할 사용자용 설명입니다.</param>
    /// <returns>아직 검증하지 않은 새 DeploymentProgressDraft를 반환합니다.</returns>
    private static DeploymentProgressDraft Draft(
        string? deploymentId,
        string? stage,
        int percent,
        string? message)
    {
        return new DeploymentProgressDraft(deploymentId, stage, percent, message);
    }

    /// <summary>
    /// Application Service 게시가 성공해야 하는 테스트에서 성공 Event를 꺼냅니다.
    /// </summary>
    /// <param name="harness">게시할 서비스와 시계를 가진 테스트 조립 묶음입니다.</param>
    /// <param name="draft">게시할 입력 후보입니다.</param>
    /// <returns>저장된 Event가 든 Task를 반환하며 실패하면 assertion 예외를 던집니다.</returns>
    private static async Task<DeploymentProgressEvent> PublishRequiredAsync(
        TestHarness harness,
        DeploymentProgressDraft draft)
    {
        var result = await harness.Service.PublishAsync(draft, CancellationToken.None);
        return RequireSuccess(result, $"{draft.Stage} Event 게시에 성공한다.");
    }

    /// <summary>
    /// terminal replay처럼 끝이 있는 비동기 stream을 List로 모아 순서와 내용을 검증할 수 있게 합니다.
    /// </summary>
    /// <param name="events">끝까지 읽을 Event 비동기 열거입니다.</param>
    /// <param name="cancellationToken">수집을 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>도착 순서대로 모은 Event 읽기 전용 목록이 든 Task를 반환합니다.</returns>
    private static async Task<IReadOnlyList<DeploymentProgressEvent>> CollectAsync(
        IAsyncEnumerable<DeploymentProgressEvent> events,
        CancellationToken cancellationToken)
    {
        var collected = new List<DeploymentProgressEvent>();
        // WithCancellation은 소비자가 가진 token을 async iterator의 EnumeratorCancellation 매개변수까지 전달합니다.
        await foreach (var progressEvent in events.WithCancellation(cancellationToken))
        {
            collected.Add(progressEvent);
        }

        return collected;
    }

    /// <summary>
    /// 성공 결과를 확인하고 이후 검증에서 사용할 값을 null 안전하게 꺼냅니다.
    /// </summary>
    /// <typeparam name="T">결과 안 성공 값의 형식입니다.</typeparam>
    /// <param name="result">성공이어야 하는 Result입니다.</param>
    /// <param name="message">assertion 출력에 사용할 초보자용 설명입니다.</param>
    /// <returns>검증을 통과한 null이 아닌 성공 값을 반환합니다.</returns>
    private static T RequireSuccess<T>(Result<T> result, string message)
    {
        Assert(result.IsSuccess, message);
        return result.Value!;
    }

    /// <summary>
    /// 실패 Result의 안정 오류 code가 기대와 같은지 검증합니다.
    /// </summary>
    /// <typeparam name="T">실패했으므로 사용하지 않을 성공 값의 형식입니다.</typeparam>
    /// <param name="result">실패여야 하는 Result입니다.</param>
    /// <param name="expectedCode">Presentation과 호출자가 분기할 기대 오류 code입니다.</param>
    /// <param name="message">assertion 실패 때 보여 줄 시나리오 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
    private static void AssertError<T>(
        Result<T> result,
        string expectedCode,
        string message)
    {
        Assert(
            !result.IsSuccess && result.Error?.Code == expectedCode,
            $"{message} (code={expectedCode})");
    }

    /// <summary>
    /// 조건을 확인하고 통과 수를 누적하거나 즉시 이해하기 쉬운 예외를 던집니다.
    /// </summary>
    /// <param name="condition">참이어야 하는 검증 조건입니다.</param>
    /// <param name="message">무엇을 검증하는지 설명하는 문장입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {message}");
        }

        _passedAssertions++;
        Console.WriteLine($"  [PASS] {message}");
    }

    /// <summary>
    /// 서로 관련된 테스트 의존성을 한 값으로 묶어 각 시나리오 준비 코드를 짧게 유지합니다.
    /// 괄호 안 매개변수는 주 생성자가 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
    /// </summary>
    /// <param name="Service">검증할 Application Service입니다.</param>
    /// <param name="Repository">실제 Channel 기반 메모리 Adapter입니다.</param>
    /// <param name="Clock">Event 시각을 결정적으로 움직이는 테스트 시계입니다.</param>
    private sealed record TestHarness(
        DeploymentProgressApplicationService Service,
        InMemoryDeploymentProgressRepository Repository,
        AdjustableTimeProvider Clock);

    /// <summary>
    /// 테스트가 운영 시계와 기다림에 의존하지 않도록 현재 UTC를 직접 움직일 수 있게 합니다.
    /// </summary>
    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        /// <summary>
        /// 지정한 UTC 시각으로 테스트 시계를 초기화합니다.
        /// </summary>
        /// <param name="utcNow">첫 Event에 사용할 UTC 시각입니다.</param>
        /// <returns>생성자는 시계를 초기화하므로 별도 반환값은 없습니다.</returns>
        public AdjustableTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow.ToUniversalTime();
        }

        /// <summary>
        /// Application Service가 Event에 기록할 현재 UTC 시각을 반환합니다.
        /// </summary>
        /// <returns>마지막으로 설정하거나 이동한 결정적 UTC 시각을 반환합니다.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        /// <summary>
        /// 실제로 기다리지 않고 테스트 시계를 지정한 시간만큼 앞으로 이동합니다.
        /// </summary>
        /// <param name="amount">0보다 커야 하는 이동 시간입니다.</param>
        /// <returns>시계를 제자리에서 바꾸므로 반환값은 없습니다.</returns>
        public void Advance(TimeSpan amount)
        {
            if (amount <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "테스트 시계는 앞으로만 이동할 수 있습니다.");
            }

            _utcNow = _utcNow.Add(amount);
        }
    }
}
