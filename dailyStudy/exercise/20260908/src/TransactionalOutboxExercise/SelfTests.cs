namespace DailyStudy.TransactionalOutbox;

/// <summary>
/// 외부 테스트 패키지 없이 핵심 신뢰성 규칙을 검증하는 작은 자체 테스트 러너입니다.
/// 모든 시간과 실패 순서를 고정하여 여러 번 실행해도 같은 결과가 나오게 합니다.
/// </summary>
internal static class SelfTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 9, 8, 1, 2, 3, TimeSpan.Zero);

    /// <summary>
    /// 등록된 테스트를 순서대로 실행하고 통과·실패 결과를 콘솔에 출력합니다.
    /// </summary>
    /// <returns>모두 통과하면 0, 하나라도 실패하면 1을 비동기로 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        // (string, Func<Task>)는 이름과 실행 함수를 한 쌍으로 묶는 tuple입니다.
        // 메서드 이름을 값처럼 넘기면 작은 테스트 러너가 같은 방식으로 각 테스트를 실행할 수 있습니다.
        (string Name, Func<Task> Test)[] tests =
        [
            ("정상 커밋은 주문과 메시지를 함께 저장한다", AtomicCommitStoresBothAsync),
            ("입력 실패는 아무것도 저장하지 않는다", InvalidInputStoresNothingAsync),
            ("커밋 실패는 두 저장을 모두 롤백한다", CommitFailureRollsBackBothAsync),
            ("서로 다른 주문과 메시지 짝은 커밋하지 않는다", MismatchedAggregateIsRejectedAsync),
            ("빈 메시지 ID는 커밋 경계에서 거부한다", EmptyMessageIdIsRejectedAsync),
            ("중복 주문은 두 번째 메시지를 만들지 않는다", DuplicateOrderCreatesNoExtraMessageAsync),
            ("발행 실패는 Pending이고 재시도 뒤 완료된다", PublishFailureCanRetryAsync),
            ("완료 표시 장애 모의는 같은 ID 중복을 드러낸다", MarkFailureSimulationCanDuplicateAsync),
            ("한 메시지 실패가 다음 메시지를 막지 않는다", BatchContinuesAfterExpectedFailureAsync),
            ("같은 시각 메시지는 ID 순서로 처리한다", SameTimeMessagesUseIdTieBreakAsync),
            ("취소는 실패 Result로 숨기지 않는다", CancellationPropagatesAsync),
            ("브로커 수락 직후 취소는 Pending을 유지한다", CancellationAfterAcceptLeavesPendingAsync),
            ("취소와 발행 실패가 겹쳐도 취소를 전파한다", CancellationWithFailurePropagatesAsync),
            ("빈 조회 결과와 취소가 겹쳐도 취소를 전파한다", CancellationAfterEmptySnapshotPropagatesAsync),
            ("커밋 실패와 취소가 겹쳐도 취소를 전파한다", CancellationWithCommitFailurePropagatesAsync),
            ("주문 저장 전 취소는 아무것도 남기지 않는다", CreateCancellationStoresNothingAsync),
        ];

        var passed = 0;

        // tuple deconstruction은 한 쌍의 Name과 Test를 읽기 좋은 두 변수로 바로 나눕니다.
        foreach (var (name, test) in tests)
        {
            try
            {
                await test().ConfigureAwait(false);
                passed++;
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[FAIL] {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"자체 테스트: {passed}/{tests.Length} 통과");
        return passed == tests.Length ? 0 : 1;
    }

    /// <summary>
    /// 정상 주문이 커밋될 때 주문과 Pending 메시지가 함께 생기고 발행은 아직 없음을 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task AtomicCommitStoresBothAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var publisher = new ScriptedEventPublisher();
        var service = CreateService(database);

        var result = await service
            .CreateAsync(ValidCommand("ORDER-ATOMIC"), FixedTime, CancellationToken.None)
            .ConfigureAwait(false);

        Assert(result.IsSuccess, "정상 주문은 성공해야 합니다.");
        Assert(database.OrderCount == 1, "주문이 정확히 한 건 저장되어야 합니다.");
        Assert(database.OutboxCount == 1, "Outbox 메시지가 정확히 한 건 저장되어야 합니다.");

        var pending = await database
            .GetPendingAsync(CancellationToken.None)
            .ConfigureAwait(false);
        var message = pending.Single();

        Assert(message.MessageId == result.Value.MessageId, "영수증과 저장 메시지 ID가 같아야 합니다.");
        Assert(message.AggregateId == result.Value.OrderId, "메시지가 같은 주문 ID를 가리켜야 합니다.");
        Assert(message.Payload.Contains("ORDER-ATOMIC", StringComparison.Ordinal), "JSON payload에 주문 ID가 있어야 합니다.");
        Assert(message.PublishedAtUtc is null, "커밋만으로는 발행 완료가 되면 안 됩니다.");
        Assert(message.AttemptCount == 0, "발행 전 시도 횟수는 0이어야 합니다.");
        Assert(publisher.AttemptedMessageIds.Count == 0, "커밋 중 외부 발행을 호출하면 안 됩니다.");
    }

    /// <summary>
    /// 잘못된 금액이 Result 실패가 되고 주문과 메시지가 모두 0건인지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task InvalidInputStoresNothingAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var service = CreateService(database);
        var command = new CreateOrderCommand("ORDER-INVALID", "CUSTOMER-1", 0m);

        var result = await service
            .CreateAsync(command, FixedTime, CancellationToken.None)
            .ConfigureAwait(false);

        Assert(result.IsFailure, "0원 주문은 예상 가능한 실패여야 합니다.");
        Assert(result.Error.Code == "order.amount_positive", "입력 오류 코드를 구분할 수 있어야 합니다.");
        Assert(database.OrderCount == 0, "잘못된 주문은 저장하면 안 됩니다.");
        Assert(database.OutboxCount == 0, "잘못된 주문의 이벤트도 저장하면 안 됩니다.");
    }

    /// <summary>
    /// 상태 교체 직전의 커밋 실패가 주문과 메시지 어느 쪽에도 부분 데이터를 남기지 않는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task CommitFailureRollsBackBothAsync()
    {
        var database = new InMemoryOutboxDatabase();
        database.FailNextCommit();
        var service = CreateService(database);

        var result = await service
            .CreateAsync(ValidCommand("ORDER-ROLLBACK"), FixedTime, CancellationToken.None)
            .ConfigureAwait(false);

        Assert(result.IsFailure, "주입한 커밋 실패가 Result로 보여야 합니다.");
        Assert(result.Error.Code == "storage.commit_failed", "저장 실패 코드를 보존해야 합니다.");
        Assert(database.OrderCount == 0, "실패 뒤 주문이 남으면 부분 커밋입니다.");
        Assert(database.OutboxCount == 0, "실패 뒤 메시지가 남으면 부분 커밋입니다.");
    }

    /// <summary>
    /// 주문 ID와 메시지 AggregateId가 다르면 Unit of Work가 계약 위반으로 거부하는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task MismatchedAggregateIsRejectedAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var orderResult = Order.Create("ORDER-A", "CUSTOMER-TEST", 12_345m, FixedTime);
        Assert(orderResult.IsSuccess, "테스트 준비 주문이 유효해야 합니다.");

        var wrongEvent = new OrderPlaced("ORDER-B", "CUSTOMER-TEST", 12_345m, FixedTime);
        var payload = new JsonOrderPlacedSerializer().Serialize(wrongEvent);
        var wrongMessage = OutboxMessage.Create(Guid.NewGuid(), wrongEvent, payload);

        await AssertThrowsAsync<ArgumentException>(
            () => database.CommitAsync(orderResult.Value, wrongMessage, CancellationToken.None),
            "서로 다른 aggregate의 주문과 메시지를 함께 저장하면 안 됩니다.").ConfigureAwait(false);

        Assert(database.OrderCount == 0, "계약 위반 뒤 주문이 저장되면 안 됩니다.");
        Assert(database.OutboxCount == 0, "계약 위반 뒤 메시지가 저장되면 안 됩니다.");
    }

    /// <summary>
    /// 공개 record 생성자로 Factory를 우회해도 빈 dedupe 키가 저장 경계를 통과하지 못하는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task EmptyMessageIdIsRejectedAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var orderResult = Order.Create("ORDER-EMPTY-MESSAGE-ID", "CUSTOMER-TEST", 12_345m, FixedTime);
        Assert(orderResult.IsSuccess, "테스트 준비 주문이 유효해야 합니다.");

        var invalidMessage = new OutboxMessage(
            Guid.Empty,
            orderResult.Value.Id,
            nameof(OrderPlaced),
            EventVersion: 1,
            FixedTime,
            Payload: "{}",
            PublishedAtUtc: null,
            AttemptCount: 0);

        await AssertThrowsAsync<ArgumentException>(
            () => database.CommitAsync(orderResult.Value, invalidMessage, CancellationToken.None),
            "빈 MessageId는 소비자 중복 제거 키로 사용할 수 없습니다.").ConfigureAwait(false);

        Assert(database.OrderCount == 0, "잘못된 메시지 뒤 주문이 저장되면 안 됩니다.");
        Assert(database.OutboxCount == 0, "빈 ID 메시지가 저장되면 안 됩니다.");
    }

    /// <summary>
    /// 같은 주문 ID를 다시 커밋하면 Conflict Result가 되고 기존 한 쌍만 유지되는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task DuplicateOrderCreatesNoExtraMessageAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var service = CreateService(database);
        var command = ValidCommand("ORDER-DUPLICATE");

        var first = await service
            .CreateAsync(command, FixedTime, CancellationToken.None)
            .ConfigureAwait(false);
        var second = await service
            .CreateAsync(command, FixedTime.AddSeconds(1), CancellationToken.None)
            .ConfigureAwait(false);

        Assert(first.IsSuccess, "첫 주문은 저장되어야 합니다.");
        Assert(second.IsFailure, "같은 주문 ID의 두 번째 요청은 실패해야 합니다.");
        Assert(second.Error.Code == "order.duplicate", "중복 오류를 별도 코드로 알려야 합니다.");
        Assert(database.OrderCount == 1, "중복 뒤에도 주문은 한 건이어야 합니다.");
        Assert(database.OutboxCount == 1, "중복 요청이 메시지를 더 만들면 안 됩니다.");
    }

    /// <summary>
    /// 첫 발행의 예상 실패가 Pending을 유지하고 다음 실행이 같은 MessageId로 성공하는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task PublishFailureCanRetryAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var receipt = await CreateValidOrderAsync(database, "ORDER-RETRY", FixedTime).ConfigureAwait(false);
        var publisher = new ScriptedEventPublisher([true]);
        var dispatcher = new OutboxDispatcher(database, publisher);

        var first = await dispatcher
            .DispatchPendingAsync(FixedTime.AddMinutes(1), CancellationToken.None)
            .ConfigureAwait(false);
        var afterFailure = await FindRequiredMessageAsync(database, receipt.MessageId).ConfigureAwait(false);

        Assert(first.Published == 0 && first.Failed == 1, "첫 실행은 한 건 실패해야 합니다.");
        Assert(first.FailureCodes.SequenceEqual(["publisher.transient"]), "재시도 가능한 실패 코드를 보고해야 합니다.");
        Assert(afterFailure.PublishedAtUtc is null, "발행 실패 메시지는 Pending이어야 합니다.");
        Assert(afterFailure.AttemptCount == 1, "첫 발행 시도를 기록해야 합니다.");

        var second = await dispatcher
            .DispatchPendingAsync(FixedTime.AddMinutes(2), CancellationToken.None)
            .ConfigureAwait(false);
        var afterSuccess = await FindRequiredMessageAsync(database, receipt.MessageId).ConfigureAwait(false);

        Assert(second.Published == 1 && second.Failed == 0, "두 번째 실행은 성공해야 합니다.");
        Assert(afterSuccess.PublishedAtUtc == FixedTime.AddMinutes(2), "성공 시각을 저장해야 합니다.");
        Assert(afterSuccess.AttemptCount == 2, "두 번의 발행 시작을 기록해야 합니다.");
        Assert(publisher.AttemptedMessageIds.SequenceEqual([receipt.MessageId, receipt.MessageId]), "재시도는 저장된 같은 MessageId를 써야 합니다.");
        Assert(publisher.DeliveredMessageIds.SequenceEqual([receipt.MessageId]), "가짜 브로커는 성공한 한 번만 수락해야 합니다.");

        var third = await dispatcher
            .DispatchPendingAsync(FixedTime.AddMinutes(3), CancellationToken.None)
            .ConfigureAwait(false);

        Assert(third.Scanned == 0, "완료 메시지는 다음 실행에서 건너뛰어야 합니다.");
        Assert(publisher.AttemptedMessageIds.Count == 2, "완료 메시지를 다시 발행하면 안 됩니다.");
    }

    /// <summary>
    /// 브로커 수락 뒤 완료 표시가 실패하면 같은 ID가 다시 전달될 수 있음을 의도적으로 증명합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task MarkFailureSimulationCanDuplicateAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var receipt = await CreateValidOrderAsync(database, "ORDER-CRASH", FixedTime).ConfigureAwait(false);
        var publisher = new ScriptedEventPublisher();
        var dispatcher = new OutboxDispatcher(database, publisher);
        database.FailNextMarkPublished();

        await AssertThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchPendingAsync(FixedTime.AddMinutes(1), CancellationToken.None),
            "완료 표시 장애는 예상 밖 인프라 예외로 전파되어야 합니다.").ConfigureAwait(false);

        var afterCrash = await FindRequiredMessageAsync(database, receipt.MessageId).ConfigureAwait(false);
        Assert(afterCrash.PublishedAtUtc is null, "완료 표시 실패 뒤 메시지는 Pending이어야 합니다.");
        Assert(afterCrash.AttemptCount == 1, "장애 전 시작한 발행 시도를 보존해야 합니다.");
        Assert(publisher.DeliveredMessageIds.SequenceEqual([receipt.MessageId]), "브로커는 첫 메시지를 이미 수락했습니다.");

        var retry = await dispatcher
            .DispatchPendingAsync(FixedTime.AddMinutes(2), CancellationToken.None)
            .ConfigureAwait(false);
        var afterRetry = await FindRequiredMessageAsync(database, receipt.MessageId).ConfigureAwait(false);

        Assert(retry.Published == 1, "다음 Dispatcher 실행은 Pending 메시지를 다시 발행해야 합니다.");
        Assert(afterRetry.AttemptCount == 2, "재실행까지 두 번의 시도가 기록되어야 합니다.");
        Assert(afterRetry.PublishedAtUtc == FixedTime.AddMinutes(2), "재시도 성공 뒤 완료 표시되어야 합니다.");
        Assert(
            publisher.DeliveredMessageIds.SequenceEqual([receipt.MessageId, receipt.MessageId]),
            "at-least-once에서는 같은 MessageId가 두 번 전달될 수 있어야 합니다.");
    }

    /// <summary>
    /// 배치의 첫 메시지가 예상 실패해도 두 번째 메시지는 계속 발행되고 실패한 것만 남는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task BatchContinuesAfterExpectedFailureAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var firstReceipt = await CreateValidOrderAsync(database, "ORDER-BATCH-A", FixedTime).ConfigureAwait(false);
        var secondReceipt = await CreateValidOrderAsync(database, "ORDER-BATCH-B", FixedTime.AddSeconds(1)).ConfigureAwait(false);
        var publisher = new ScriptedEventPublisher([true, false]);
        var dispatcher = new OutboxDispatcher(database, publisher);

        var report = await dispatcher
            .DispatchPendingAsync(FixedTime.AddMinutes(1), CancellationToken.None)
            .ConfigureAwait(false);
        var pending = await database
            .GetPendingAsync(CancellationToken.None)
            .ConfigureAwait(false);

        Assert(report.Scanned == 2, "두 메시지를 모두 확인해야 합니다.");
        Assert(report.Published == 1 && report.Failed == 1, "성공과 실패를 각각 집계해야 합니다.");
        Assert(pending.Count == 1, "실패한 메시지만 Pending이어야 합니다.");
        Assert(pending.Single().MessageId == firstReceipt.MessageId, "먼저 발생해 실패한 메시지가 남아야 합니다.");
        Assert(publisher.DeliveredMessageIds.SequenceEqual([secondReceipt.MessageId]), "뒤의 정상 메시지는 실패에 막히면 안 됩니다.");

        var retry = await dispatcher
            .DispatchPendingAsync(FixedTime.AddMinutes(2), CancellationToken.None)
            .ConfigureAwait(false);
        Assert(retry.Published == 1 && retry.Failed == 0, "남은 메시지는 다음 실행에서 성공해야 합니다.");
    }

    /// <summary>
    /// 발생 시각이 같은 메시지를 MessageId 오름차순으로 처리해 순서가 결정적인지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task SameTimeMessagesUseIdTieBreakAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var smallerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var largerId = Guid.Parse("00000000-0000-0000-0000-000000000002");

        // 일부러 큰 ID부터 저장해 Dictionary 삽입 순서가 아니라 ThenBy 규칙을 검증합니다.
        await CommitFixedMessageAsync(database, "ORDER-TIE-B", largerId, FixedTime).ConfigureAwait(false);
        await CommitFixedMessageAsync(database, "ORDER-TIE-A", smallerId, FixedTime).ConfigureAwait(false);

        var publisher = new ScriptedEventPublisher([true, false]);
        var dispatcher = new OutboxDispatcher(database, publisher);
        await dispatcher
            .DispatchPendingAsync(FixedTime.AddMinutes(1), CancellationToken.None)
            .ConfigureAwait(false);

        Assert(
            publisher.AttemptedMessageIds.SequenceEqual([smallerId, largerId]),
            "같은 시각이면 MessageId가 작은 메시지를 먼저 시도해야 합니다.");
        var pending = await database.GetPendingAsync(CancellationToken.None).ConfigureAwait(false);
        Assert(pending.Single().MessageId == smallerId, "첫 스크립트 실패는 작은 ID 메시지에 적용되어야 합니다.");
    }

    /// <summary>
    /// 이미 취소된 Dispatcher 요청이 OperationCanceledException으로 전파되고 상태를 바꾸지 않는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task CancellationPropagatesAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var receipt = await CreateValidOrderAsync(database, "ORDER-CANCEL-DISPATCH", FixedTime).ConfigureAwait(false);
        var publisher = new ScriptedEventPublisher();
        var dispatcher = new OutboxDispatcher(database, publisher);

        // using var는 테스트가 끝날 때 CancellationTokenSource의 자원을 자동 정리합니다.
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await AssertThrowsAsync<OperationCanceledException>(
            () => dispatcher.DispatchPendingAsync(FixedTime.AddMinutes(1), cancellation.Token),
            "취소를 일반 실패 Result로 바꾸면 안 됩니다.").ConfigureAwait(false);

        var message = await FindRequiredMessageAsync(database, receipt.MessageId).ConfigureAwait(false);
        Assert(message.PublishedAtUtc is null, "취소된 메시지는 Pending이어야 합니다.");
        Assert(message.AttemptCount == 0, "발행 시작 전 취소라면 시도 횟수가 늘면 안 됩니다.");
        Assert(publisher.AttemptedMessageIds.Count == 0, "취소 뒤 외부 호출을 시작하면 안 됩니다.");
    }

    /// <summary>
    /// 브로커가 메시지를 받은 직후 취소되면 완료 표시가 되지 않고 같은 ID로 재시도되는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task CancellationAfterAcceptLeavesPendingAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var receipt = await CreateValidOrderAsync(database, "ORDER-CANCEL-AFTER-ACCEPT", FixedTime).ConfigureAwait(false);
        using var cancellation = new CancellationTokenSource();
        var cancelingPublisher = new CancelAfterAcceptPublisher(cancellation);
        var dispatcher = new OutboxDispatcher(database, cancelingPublisher);

        await AssertThrowsAsync<OperationCanceledException>(
            () => dispatcher.DispatchPendingAsync(FixedTime.AddMinutes(1), cancellation.Token),
            "브로커 수락 뒤 취소도 OperationCanceledException으로 전파되어야 합니다.").ConfigureAwait(false);

        var pendingAfterCancel = await FindRequiredMessageAsync(database, receipt.MessageId).ConfigureAwait(false);
        Assert(pendingAfterCancel.PublishedAtUtc is null, "취소 때문에 완료 표시되지 못한 메시지는 Pending이어야 합니다.");
        Assert(pendingAfterCancel.AttemptCount == 1, "브로커 호출까지 시작한 첫 시도는 기록되어야 합니다.");
        Assert(cancelingPublisher.DeliveredMessageIds.SequenceEqual([receipt.MessageId]), "브로커는 취소 직전에 메시지를 수락했습니다.");

        var retryPublisher = new ScriptedEventPublisher();
        var retryDispatcher = new OutboxDispatcher(database, retryPublisher);
        var retry = await retryDispatcher
            .DispatchPendingAsync(FixedTime.AddMinutes(2), CancellationToken.None)
            .ConfigureAwait(false);

        Assert(retry.Published == 1, "다음 실행에서 Pending 메시지를 재시도해야 합니다.");
        Assert(retryPublisher.DeliveredMessageIds.SequenceEqual([receipt.MessageId]), "재시도도 저장된 같은 MessageId를 사용해야 합니다.");
    }

    /// <summary>
    /// 발행기가 토큰을 취소하고 실패 Result도 반환할 때 취소가 우선 전파되는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task CancellationWithFailurePropagatesAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var receipt = await CreateValidOrderAsync(database, "ORDER-CANCEL-WITH-FAILURE", FixedTime).ConfigureAwait(false);
        using var cancellation = new CancellationTokenSource();
        var dispatcher = new OutboxDispatcher(database, new CancelThenFailPublisher(cancellation));

        await AssertThrowsAsync<OperationCanceledException>(
            () => dispatcher.DispatchPendingAsync(FixedTime.AddMinutes(1), cancellation.Token),
            "취소와 실패 Result가 겹치면 취소가 우선 전파되어야 합니다.").ConfigureAwait(false);

        var message = await FindRequiredMessageAsync(database, receipt.MessageId).ConfigureAwait(false);
        Assert(message.PublishedAtUtc is null, "취소된 실패 메시지는 Pending이어야 합니다.");
        Assert(message.AttemptCount == 1, "발행기를 호출한 시도는 기록되어야 합니다.");
    }

    /// <summary>
    /// 저장소가 빈 Pending 스냅샷을 반환하며 토큰도 취소하면 빈 정상 보고서로 숨기지 않는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task CancellationAfterEmptySnapshotPropagatesAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new CancelAfterEmptySnapshotRepository(cancellation);
        var publisher = new ScriptedEventPublisher();
        var dispatcher = new OutboxDispatcher(repository, publisher);

        await AssertThrowsAsync<OperationCanceledException>(
            () => dispatcher.DispatchPendingAsync(FixedTime, cancellation.Token),
            "빈 조회 결과와 취소가 겹쳐도 OperationCanceledException이어야 합니다.").ConfigureAwait(false);

        Assert(repository.GetPendingCallCount == 1, "Pending 조회는 한 번 실행되어야 합니다.");
        Assert(publisher.AttemptedMessageIds.Count == 0, "빈 스냅샷 뒤에는 발행을 시도하면 안 됩니다.");
    }

    /// <summary>
    /// Unit of Work가 실패 Result와 취소를 함께 반환할 때 업무 실패로 숨기지 않는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task CancellationWithCommitFailurePropagatesAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var unitOfWork = new CancelThenFailUnitOfWork(cancellation);
        var service = new OrderApplicationService(unitOfWork, new JsonOrderPlacedSerializer());

        await AssertThrowsAsync<OperationCanceledException>(
            () => service.CreateAsync(ValidCommand("ORDER-CANCEL-COMMIT-FAILURE"), FixedTime, cancellation.Token),
            "커밋 실패 Result와 취소가 겹치면 취소가 우선 전파되어야 합니다.").ConfigureAwait(false);

        Assert(unitOfWork.CommitCallCount == 1, "커밋은 한 번만 시도되어야 합니다.");
    }

    /// <summary>
    /// 이미 취소된 주문 생성 요청이 예외로 전파되고 원자 저장소를 전혀 바꾸지 않는지 확인합니다.
    /// </summary>
    /// <returns>검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task CreateCancellationStoresNothingAsync()
    {
        var database = new InMemoryOutboxDatabase();
        var service = CreateService(database);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await AssertThrowsAsync<OperationCanceledException>(
            () => service.CreateAsync(ValidCommand("ORDER-CANCEL-CREATE"), FixedTime, cancellation.Token),
            "저장 전 취소는 OperationCanceledException이어야 합니다.").ConfigureAwait(false);

        Assert(database.OrderCount == 0, "취소된 주문을 저장하면 안 됩니다.");
        Assert(database.OutboxCount == 0, "취소된 주문의 메시지를 저장하면 안 됩니다.");
    }

    /// <summary>
    /// 공통 직렬화 Adapter와 주어진 DB를 사용하는 주문 Application Service를 만듭니다.
    /// </summary>
    /// <param name="database">Unit of Work 역할을 할 메모리 DB입니다.</param>
    /// <returns>테스트에서 바로 호출할 수 있는 새 서비스를 반환합니다.</returns>
    private static OrderApplicationService CreateService(InMemoryOutboxDatabase database)
    {
        return new OrderApplicationService(database, new JsonOrderPlacedSerializer());
    }

    /// <summary>
    /// 테스트 이름만 바꿔 재사용할 수 있는 정상 주문 명령을 만듭니다.
    /// </summary>
    /// <param name="orderId">테스트끼리 충돌하지 않을 주문 ID입니다.</param>
    /// <returns>고정 고객과 양수 금액을 가진 유효한 명령을 반환합니다.</returns>
    private static CreateOrderCommand ValidCommand(string orderId)
    {
        return new CreateOrderCommand(orderId, "CUSTOMER-TEST", 12_345m);
    }

    /// <summary>
    /// 정상 주문을 저장하고 테스트가 이어서 사용할 영수증을 꺼냅니다.
    /// </summary>
    /// <param name="database">주문과 메시지를 함께 저장할 메모리 DB입니다.</param>
    /// <param name="orderId">새 주문에 사용할 고유 ID입니다.</param>
    /// <param name="occurredAtUtc">결정적 정렬을 위한 이벤트 UTC 시각입니다.</param>
    /// <returns>성공한 주문 ID와 저장된 메시지 ID를 비동기로 반환합니다.</returns>
    private static async Task<OrderReceipt> CreateValidOrderAsync(
        InMemoryOutboxDatabase database,
        string orderId,
        DateTimeOffset occurredAtUtc)
    {
        var result = await CreateService(database)
            .CreateAsync(ValidCommand(orderId), occurredAtUtc, CancellationToken.None)
            .ConfigureAwait(false);

        Assert(result.IsSuccess, $"테스트 준비 주문 '{orderId}'가 저장되어야 합니다.");
        return result.Value;
    }

    /// <summary>
    /// 정렬 테스트가 사용할 고정 MessageId의 주문과 Outbox 메시지를 직접 원자 커밋합니다.
    /// </summary>
    /// <param name="database">두 값을 저장할 교육용 Unit of Work입니다.</param>
    /// <param name="orderId">Order와 AggregateId가 공유할 ID입니다.</param>
    /// <param name="messageId">정렬 순서를 결정적으로 만들 고정 메시지 ID입니다.</param>
    /// <param name="occurredAtUtc">두 메시지에 똑같이 줄 이벤트 시각입니다.</param>
    /// <returns>커밋 검증이 끝날 때 완료되는 비동기 작업입니다.</returns>
    private static async Task CommitFixedMessageAsync(
        InMemoryOutboxDatabase database,
        string orderId,
        Guid messageId,
        DateTimeOffset occurredAtUtc)
    {
        var orderResult = Order.Create(orderId, "CUSTOMER-TEST", 12_345m, occurredAtUtc);
        Assert(orderResult.IsSuccess, "정렬 테스트 준비 주문이 유효해야 합니다.");

        var domainEvent = new OrderPlaced(orderId, "CUSTOMER-TEST", 12_345m, occurredAtUtc);
        var payload = new JsonOrderPlacedSerializer().Serialize(domainEvent);
        var message = OutboxMessage.Create(messageId, domainEvent, payload);
        var commit = await database
            .CommitAsync(orderResult.Value, message, CancellationToken.None)
            .ConfigureAwait(false);
        Assert(commit.IsSuccess, "정렬 테스트 준비 데이터를 커밋해야 합니다.");
    }

    /// <summary>
    /// 저장된 메시지를 반드시 찾아 테스트 본문의 반복적인 null 검사를 줄입니다.
    /// </summary>
    /// <param name="database">메시지를 조회할 메모리 DB입니다.</param>
    /// <param name="messageId">반드시 존재해야 하는 메시지 ID입니다.</param>
    /// <returns>찾은 Outbox 메시지를 비동기로 반환합니다.</returns>
    private static async Task<OutboxMessage> FindRequiredMessageAsync(
        InMemoryOutboxDatabase database,
        Guid messageId)
    {
        var message = await database
            .FindAsync(messageId, CancellationToken.None)
            .ConfigureAwait(false);
        return message ?? throw new InvalidOperationException($"메시지 '{messageId}'가 없습니다.");
    }

    /// <summary>
    /// 조건이 거짓이면 테스트 실패를 뜻하는 예외를 던집니다.
    /// </summary>
    /// <param name="condition">반드시 true여야 하는 검증 조건입니다.</param>
    /// <param name="message">조건이 거짓일 때 원인을 설명할 메시지입니다.</param>
    /// <returns>조건이 참이면 아무 값도 반환하지 않고 끝납니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 비동기 작업이 지정한 예외 타입을 실제로 전파하는지 확인합니다.
    /// </summary>
    /// <typeparam name="TException">발생해야 하는 예외 타입입니다.</typeparam>
    /// <param name="action">예외가 나야 하는 비동기 동작입니다.</param>
    /// <param name="message">예외가 없을 때 보여 줄 테스트 실패 설명입니다.</param>
    /// <returns>기대한 예외를 확인하면 완료되는 비동기 작업입니다.</returns>
    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
        // where는 TException으로 Exception 계열 타입만 받을 수 있게 제한하는 제네릭 제약입니다.
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
    /// 메시지를 수락한 직후 호출자의 토큰을 취소하여 publish와 mark 사이 경계를 재현합니다.
    /// </summary>
    private sealed class CancelAfterAcceptPublisher : IEventPublisher
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly List<Guid> _deliveredMessageIds = [];

        /// <summary>
        /// 발행 성공 직후 취소할 토큰 소스를 주입받습니다.
        /// </summary>
        /// <param name="cancellation">브로커 수락 뒤 취소 신호를 보낼 소스입니다.</param>
        /// <remarks>테스트용 의존성을 보관하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
        public CancelAfterAcceptPublisher(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        /// <summary>가짜 브로커가 수락한 메시지 ID의 읽기 전용 복사본입니다.</summary>
        public IReadOnlyList<Guid> DeliveredMessageIds => _deliveredMessageIds.ToArray();

        /// <summary>
        /// 메시지를 수락 목록에 넣은 다음 토큰을 취소하고 성공 Result를 반환합니다.
        /// </summary>
        /// <param name="message">수락할 Outbox 메시지입니다.</param>
        /// <param name="cancellationToken">수락 전에 확인할 호출자 취소 신호입니다.</param>
        /// <returns>브로커 수락 자체는 성공했으므로 성공 Result를 담은 완료 Task를 반환합니다.</returns>
        public Task<Result> PublishAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            cancellationToken.ThrowIfCancellationRequested();
            _deliveredMessageIds.Add(message.MessageId);
            _cancellation.Cancel();
            return Task.FromResult(Result.Success());
        }
    }

    /// <summary>
    /// 호출자 토큰을 취소한 뒤 실패 Result를 돌려주는 비협조적 발행기 구현입니다.
    /// </summary>
    private sealed class CancelThenFailPublisher : IEventPublisher
    {
        private readonly CancellationTokenSource _cancellation;

        /// <summary>취소 신호를 보낼 토큰 소스를 보관합니다.</summary>
        /// <param name="cancellation">발행 도중 취소할 호출자 토큰의 소스입니다.</param>
        /// <remarks>테스트용 의존성을 보관하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
        public CancelThenFailPublisher(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        /// <summary>토큰을 취소한 뒤 일시 실패 Result를 반환합니다.</summary>
        /// <param name="message">발행을 시도할 Outbox 메시지입니다.</param>
        /// <param name="cancellationToken">호출 시작 전에 확인할 취소 신호입니다.</param>
        /// <returns>취소와 함께 반환되는 가짜 발행 실패 Result입니다.</returns>
        public Task<Result> PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);
            cancellationToken.ThrowIfCancellationRequested();
            _cancellation.Cancel();
            return Task.FromResult(Result.Failure(
                new Error("publisher.cancelled_and_failed", "가짜 발행기가 취소와 실패를 동시에 만들었습니다.")));
        }
    }

    /// <summary>
    /// 빈 Pending 스냅샷을 돌려주기 직전에 호출자 토큰을 취소하는 저장소입니다.
    /// </summary>
    private sealed class CancelAfterEmptySnapshotRepository : IOutboxRepository
    {
        private readonly CancellationTokenSource _cancellation;

        /// <summary>조회 반환 직전에 취소할 토큰 소스를 보관합니다.</summary>
        /// <param name="cancellation">조회 도중 취소할 호출자 토큰의 소스입니다.</param>
        /// <remarks>테스트용 의존성을 보관하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
        public CancelAfterEmptySnapshotRepository(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        /// <summary>Pending 조회가 실행된 횟수입니다.</summary>
        /// <remarks><c>get; private set;</c>은 테스트가 값을 읽되 이 가짜 객체만 바꾸게 하여 관측값 위조를 막습니다.</remarks>
        public int GetPendingCallCount { get; private set; }

        /// <summary>빈 스냅샷을 만들고 토큰을 취소한 뒤 그 스냅샷을 반환합니다.</summary>
        /// <param name="cancellationToken">조회 시작 전에 확인할 취소 신호입니다.</param>
        /// <returns>항목이 없는 읽기 전용 Outbox 메시지 목록입니다.</returns>
        public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetPendingCallCount++;
            _cancellation.Cancel();
            IReadOnlyList<OutboxMessage> empty = [];
            return Task.FromResult(empty);
        }

        /// <summary>이 테스트에서는 개별 메시지 조회가 호출되면 안 됨을 드러냅니다.</summary>
        /// <param name="messageId">호출되면 안 되는 메시지 ID입니다.</param>
        /// <param name="cancellationToken">호출되면 안 되는 취소 신호입니다.</param>
        /// <returns>정상 경로에서는 반환되지 않는 메시지 Task입니다.</returns>
        public Task<OutboxMessage?> FindAsync(Guid messageId, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("빈 스냅샷 테스트에서는 FindAsync를 호출하면 안 됩니다.");
        }

        /// <summary>이 테스트에서는 발행 시도 기록이 호출되면 안 됨을 드러냅니다.</summary>
        /// <param name="messageId">호출되면 안 되는 메시지 ID입니다.</param>
        /// <param name="cancellationToken">호출되면 안 되는 취소 신호입니다.</param>
        /// <returns>정상 경로에서는 반환되지 않는 메시지 Task입니다.</returns>
        public Task<OutboxMessage> StartAttemptAsync(Guid messageId, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("빈 스냅샷 테스트에서는 StartAttemptAsync를 호출하면 안 됩니다.");
        }

        /// <summary>이 테스트에서는 완료 표시가 호출되면 안 됨을 드러냅니다.</summary>
        /// <param name="messageId">호출되면 안 되는 메시지 ID입니다.</param>
        /// <param name="publishedAtUtc">호출되면 안 되는 완료 시각입니다.</param>
        /// <param name="cancellationToken">호출되면 안 되는 취소 신호입니다.</param>
        /// <returns>정상 경로에서는 반환되지 않는 Task입니다.</returns>
        public Task MarkPublishedAsync(
            Guid messageId,
            DateTimeOffset publishedAtUtc,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("빈 스냅샷 테스트에서는 MarkPublishedAsync를 호출하면 안 됩니다.");
        }
    }

    /// <summary>
    /// 커밋 실패 Result를 돌려주기 직전에 호출자 토큰을 취소하는 Unit of Work입니다.
    /// </summary>
    private sealed class CancelThenFailUnitOfWork : IOrderUnitOfWork
    {
        private readonly CancellationTokenSource _cancellation;

        /// <summary>커밋 반환 직전에 취소할 토큰 소스를 보관합니다.</summary>
        /// <param name="cancellation">커밋 도중 취소할 호출자 토큰의 소스입니다.</param>
        /// <remarks>테스트용 의존성을 보관하는 생성자이므로 별도의 반환값은 없습니다.</remarks>
        public CancelThenFailUnitOfWork(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        /// <summary>커밋이 실행된 횟수입니다.</summary>
        public int CommitCallCount { get; private set; }

        /// <summary>토큰을 취소한 뒤 저장 실패 Result를 반환합니다.</summary>
        /// <param name="order">저장을 시도한 유효한 주문입니다.</param>
        /// <param name="message">주문과 함께 저장하려던 Outbox 메시지입니다.</param>
        /// <param name="cancellationToken">커밋 시작 전에 확인할 취소 신호입니다.</param>
        /// <returns>취소와 함께 반환되는 가짜 저장 실패 Result입니다.</returns>
        public Task<Result> CommitAsync(
            Order order,
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(message);
            cancellationToken.ThrowIfCancellationRequested();
            CommitCallCount++;
            _cancellation.Cancel();
            return Task.FromResult(Result.Failure(
                new Error("storage.cancelled_and_failed", "가짜 Unit of Work가 취소와 실패를 동시에 만들었습니다.")));
        }
    }
}
