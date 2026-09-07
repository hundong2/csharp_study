namespace DailyStudy.TransactionalOutbox;

/// <summary>
/// 콘솔 데모의 Composition Root입니다.
/// 객체를 한곳에서 조립하여 도메인과 Application 코드가 구체 구현 생성 책임을 갖지 않게 합니다.
/// </summary>
internal static class Program
{
    private static readonly DateTimeOffset OrderTime =
        new(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 일반 데모 또는 <c>--self-test</c> 검증 모드를 실행합니다.
    /// </summary>
    /// <param name="args">명령줄 인수이며 --self-test가 있으면 자체 테스트를 선택합니다.</param>
    /// <returns>성공하면 0, 처리하지 못한 실패나 예외가 있으면 1을 비동기로 반환합니다.</returns>
    private static async Task<int> Main(string[] args)
    {
        // try는 정상 경로를 실행하고, catch는 그 안에서 던져진 예상 밖 예외만 최상단 경계에서 처리합니다.
        // 입력 오류처럼 예상 가능한 실패는 아래 계층의 Result로 다루므로 여기까지 예외로 올리지 않습니다.
        try
        {
            // Contains의 두 번째 인수는 대소문자를 무시하여 사용자가 옵션을 편하게 입력하게 합니다.
            if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
            {
                return await SelfTests.RunAsync().ConfigureAwait(false);
            }

            return await RunDemoAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // 최상단에서만 예상 밖 예외를 사용자에게 보여 주고 실패 종료 코드로 바꿉니다.
            Console.Error.WriteLine($"예상하지 못한 오류: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 주문 원자 저장, 첫 발행 실패, 재시도 성공, 완료 메시지 건너뛰기를 차례대로 보여 줍니다.
    /// </summary>
    /// <returns>모든 예상 단계가 끝나면 0, 주문 생성이 실패하면 1을 반환합니다.</returns>
    private static async Task<int> RunDemoAsync()
    {
        var database = new InMemoryOutboxDatabase();

        // [true]는 C# collection expression이며 bool 한 개가 든 배열을 간결하게 만듭니다.
        // 첫 호출만 실패시키므로 실제 시간 지연 없이 재시도 흐름을 재현할 수 있습니다.
        var publisher = new ScriptedEventPublisher([true]);

        // 인터페이스에 구체 구현을 넘기는 수동 DI입니다. 이곳이 애플리케이션의 Composition Root입니다.
        var orderService = new OrderApplicationService(database, new JsonOrderPlacedSerializer());
        var dispatcher = new OutboxDispatcher(database, publisher);
        // 숫자 안의 _는 값에 영향을 주지 않고 59,900을 읽기 쉽게 묶는 자릿수 구분자입니다.
        var command = new CreateOrderCommand("ORDER-20260908-001", "CUSTOMER-104", 59_900m);

        var createResult = await orderService
            .CreateAsync(command, OrderTime, CancellationToken.None)
            .ConfigureAwait(false);

        if (createResult.IsFailure)
        {
            Console.Error.WriteLine($"[주문 실패] {createResult.Error.Code}: {createResult.Error.Message}");
            return 1;
        }

        var pendingAfterCommit = await database
            .GetPendingAsync(CancellationToken.None)
            .ConfigureAwait(false);
        Console.WriteLine(
            $"[저장] 주문 {database.OrderCount}건, Outbox {database.OutboxCount}건, " +
            $"Pending {pendingAfterCommit.Count}건, 외부 전달 {publisher.DeliveredMessageIds.Count}건");

        var firstDispatch = await dispatcher
            .DispatchPendingAsync(OrderTime.AddMinutes(1), CancellationToken.None)
            .ConfigureAwait(false);
        var pendingAfterFailure = await database
            .GetPendingAsync(CancellationToken.None)
            .ConfigureAwait(false);
        Console.WriteLine(
            $"[1차 발행] 성공 {firstDispatch.Published}건, 실패 {firstDispatch.Failed}건, " +
            $"Pending {pendingAfterFailure.Count}건");

        var secondDispatch = await dispatcher
            .DispatchPendingAsync(OrderTime.AddMinutes(2), CancellationToken.None)
            .ConfigureAwait(false);
        var pendingAfterRetry = await database
            .GetPendingAsync(CancellationToken.None)
            .ConfigureAwait(false);
        Console.WriteLine(
            $"[2차 발행] 성공 {secondDispatch.Published}건, 실패 {secondDispatch.Failed}건, " +
            $"Pending {pendingAfterRetry.Count}건");

        var finalDispatch = await dispatcher
            .DispatchPendingAsync(OrderTime.AddMinutes(3), CancellationToken.None)
            .ConfigureAwait(false);
        Console.WriteLine(
            $"[재실행] 조회 {finalDispatch.Scanned}건, 외부 누적 전달 {publisher.DeliveredMessageIds.Count}건");

        Console.WriteLine("학습 모델: 프로세스 내 원자 저장을 확인했습니다. 영속 DB와 Dispatcher 재실행을 전제로 전달은 at-least-once입니다.");
        return 0;
    }
}
