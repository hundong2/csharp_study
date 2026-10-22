using System.Globalization;
using OrderPaymentReconciliation.Application;
using OrderPaymentReconciliation.Domain;
using OrderPaymentReconciliation.Infrastructure;
using OrderPaymentReconciliation.SelfTesting;

namespace OrderPaymentReconciliation;

/// <summary>
/// 구체 Repository Adapter와 Strategy를 조립하고 demo 또는 self-test 실행을 선택하는 Composition Root입니다.
/// 객체 생성 책임을 진입점 한곳에 모으면 Domain과 Application은 구체 기술을 알 필요가 없습니다.
/// </summary>
public sealed class Program
{
    /// <summary>
    /// static 진입점만 가진 Composition Root가 실수로 객체화되지 않게 막습니다.
    /// </summary>
    private Program()
    {
    }

    /// <summary>
    /// 명령줄 인수에 따라 학습용 demo 또는 자동 self-test를 실행합니다.
    /// </summary>
    /// <param name="args">`--demo` 또는 `--self-test` 실행 mode를 담은 명령줄 인수입니다.</param>
    /// <returns>성공은 0, demo 업무 실패는 1, 알 수 없는 인수는 2를 담은 Task를 반환합니다.</returns>
    // async/await는 thread를 붙잡지 않고 demo 또는 self-test의 비동기 완료를 기다리는 C# 문법입니다.
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.Ordinal))
        {
            return await SelfTestRunner.RunAsync();
        }

        if (args.Length == 0 || args.Contains("--demo", StringComparer.Ordinal))
        {
            return await RunDemoAsync();
        }

        Console.Error.WriteLine("사용법: --demo 또는 --self-test");
        return 2;
    }

    /// <summary>
    /// 다섯 가지 대사 상태가 나타나는 고정 입력으로 전체 use case를 실행하고 표 형태로 출력합니다.
    /// </summary>
    /// <returns>보고서 생성 성공은 0, 예상 가능한 데이터 실패는 1을 반환합니다.</returns>
    private static async Task<int> RunDemoAsync()
    {
        // var는 오른쪽 반환값이 ReconcileOrdersService임이 분명할 때 정적 형식을 compiler가 추론하게 합니다.
        var service = BuildDemoService();
        var result = await service.ReconcileAsync(CancellationToken.None);

        if (!result.IsSuccess)
        {
            Console.Error.WriteLine($"[DEMO:FAILURE] {result.Error.Code}: {result.Error.Message}");
            return 1;
        }

        var report = result.Value;
        Console.WriteLine("주문-결제 대사 결과");
        Console.WriteLine("OrderId | PaymentId | Expected | Actual | Difference | Status");

        // foreach는 sequence의 각 행을 순서대로 한 번씩 처리하는 기본 반복 문법입니다.
        foreach (var line in report.Lines)
        {
            // ?.는 값이 있을 때만 ToString을 호출하고, ??는 null일 때 표시용 대시를 선택합니다.
            var expected = line.ExpectedAmount?.ToString() ?? "-";
            var actual = line.ActualAmount?.ToString() ?? "-";
            var difference = line.Difference?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-";
            Console.WriteLine(
                $"{line.OrderId} | {line.PaymentId ?? "-"} | {expected} | "
                + $"{actual} | {difference} | {line.Status}");
        }

        Console.WriteLine(
            $"[DEMO:SUCCESS] total={report.Lines.Length}, "
            + $"matched={report.Count(ReconciliationStatus.Matched)}, "
            + $"review={report.ReviewCount}");
        return 0;
    }

    /// <summary>
    /// demo용 Port 구현과 Strategy를 생성자 주입으로 연결해 Application Service를 만듭니다.
    /// </summary>
    /// <returns>메모리 주문·결제 snapshot을 사용하는 실행 가능한 service를 반환합니다.</returns>
    private static ReconcileOrdersService BuildDemoService()
    {
        // new로 구체 구현을 고르는 곳을 Composition Root에 제한하면 나중에 DB Adapter로 교체하기 쉽습니다.
        return new ReconcileOrdersService(
            new InMemoryOrderRepository(CreateDemoOrders()),
            new InMemoryPaymentRepository(CreateDemoPayments()),
            new AmountAndCurrencyPolicy());
    }

    /// <summary>
    /// 일치, 결제 누락, 금액 불일치, 통화 불일치를 각각 보여 줄 demo 주문을 만듭니다.
    /// </summary>
    /// <returns>고정된 네 건의 유효한 주문 목록을 반환합니다.</returns>
    private static IReadOnlyList<Order> CreateDemoOrders()
    {
        var baseTime = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);

        // [ ... ]는 C# collection expression입니다. 목표 형식에 맞는 collection을 간결하게 만듭니다.
        return
        [
            Must(Order.Create("ORD-100", "CUS-1", Must(Money.Create(35_000m, "KRW")), baseTime)),
            Must(Order.Create("ORD-101", "CUS-2", Must(Money.Create(12_000m, "KRW")), baseTime.AddMinutes(1))),
            Must(Order.Create("ORD-102", "CUS-3", Must(Money.Create(50_000m, "KRW")), baseTime.AddMinutes(2))),
            Must(Order.Create("ORD-103", "CUS-4", Must(Money.Create(29m, "USD")), baseTime.AddMinutes(3)))
        ];
    }

    /// <summary>
    /// 일치, 금액 불일치, 통화 불일치와 주문 없는 고아 결제를 보여 줄 demo 결제를 만듭니다.
    /// </summary>
    /// <returns>고정된 네 건의 유효한 결제 목록을 반환합니다.</returns>
    private static IReadOnlyList<Payment> CreateDemoPayments()
    {
        var baseTime = new DateTimeOffset(2026, 10, 23, 0, 5, 0, TimeSpan.Zero);

        return
        [
            Must(Payment.Create("PAY-100", "ORD-100", Must(Money.Create(35_000m, "KRW")), baseTime)),
            Must(Payment.Create("PAY-102", "ORD-102", Must(Money.Create(45_000m, "KRW")), baseTime.AddMinutes(1))),
            Must(Payment.Create("PAY-103", "ORD-103", Must(Money.Create(29m, "EUR")), baseTime.AddMinutes(2))),
            Must(Payment.Create("PAY-999", "ORD-999", Must(Money.Create(8_000m, "KRW")), baseTime.AddMinutes(3)))
        ];
    }

    /// <summary>
    /// 소스 코드에 고정한 demo 값이 잘못되었다면 사용자 오류가 아니라 개발자의 구성 실수로 바꿔 즉시 중단합니다.
    /// </summary>
    /// <typeparam name="T">성공 시 꺼낼 null이 아닌 값의 형식입니다.</typeparam>
    /// <param name="result">항상 성공해야 하는 demo 데이터 생성 결과입니다.</param>
    /// <returns>성공 값을 반환합니다.</returns>
    /// <exception cref="InvalidOperationException">고정 demo 데이터가 도메인 규칙을 어겼을 때 발생합니다.</exception>
    private static T Must<T>(Result<T> result)
        // where T : notnull은 성공 값으로 null 허용 형식을 받지 않겠다는 generic 제약입니다.
        where T : notnull
    {
        // ?:는 조건에 따라 값 또는 throw 식 중 하나를 고르는 삼항 조건 연산자입니다.
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(
                $"고정 demo 데이터 오류: {result.Error.Code} - {result.Error.Message}");
    }
}
