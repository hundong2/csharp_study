using OrderPaymentReconciliation.Application.Ports;
using OrderPaymentReconciliation.Domain;

namespace OrderPaymentReconciliation.Application;

/// <summary>
/// 주문·결제 조회, 입력 집합 검증, LeftJoin, 고아 결제 탐지를 순서대로 조율하는 Application Service입니다.
/// 저장 기술과 판정 규칙은 Port 뒤로 숨겨 이 use case 자체를 작고 테스트하기 쉽게 유지합니다.
/// </summary>
public sealed class ReconcileOrdersService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IReconciliationPolicy _policy;

    /// <summary>
    /// use case에 필요한 Repository Port와 Strategy를 생성자 주입으로 받습니다.
    /// </summary>
    /// <param name="orderRepository">주문을 읽는 저장소 추상화입니다.</param>
    /// <param name="paymentRepository">결제를 읽는 저장소 추상화입니다.</param>
    /// <param name="policy">주문과 결제의 업무 상태를 판정하는 전략입니다.</param>
    public ReconcileOrdersService(
        IOrderRepository orderRepository,
        IPaymentRepository paymentRepository,
        IReconciliationPolicy policy)
    {
        // ?? throw는 왼쪽 의존성이 null일 때만 즉시 예외를 던져 잘못 조립된 객체를 막는 문법입니다.
        _orderRepository = orderRepository
            ?? throw new ArgumentNullException(nameof(orderRepository));
        _paymentRepository = paymentRepository
            ?? throw new ArgumentNullException(nameof(paymentRepository));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <summary>
    /// 모든 주문과 결제를 읽어 중복을 거절하고 완전한 대사 보고서를 만듭니다.
    /// </summary>
    /// <param name="cancellationToken">Repository와 각 CPU 처리 단계 사이에서 작업 중단을 확인할 신호입니다.</param>
    /// <returns>정상 보고서 또는 신뢰할 수 없는 중복 입력을 설명하는 실패 Result를 반환합니다.</returns>
    // async/await는 thread를 붙잡아 두지 않고 Repository의 비동기 완료를 기다리는 C# 문법입니다.
    public async Task<Result<ReconciliationReport>> ReconcileAsync(
        CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.ListAsync(cancellationToken);
        var payments = await _paymentRepository.ListAsync(cancellationToken);
        // Repository가 반환하는 바로 그 순간 취소될 수도 있으므로 CPU 작업을 시작하기 전에 다시 확인합니다.
        cancellationToken.ThrowIfCancellationRequested();

        var duplicateOrderId = FindDuplicateKey(orders, order => order.OrderId);
        if (duplicateOrderId is not null)
        {
            return Result<ReconciliationReport>.Failure(
                new Error(
                    "data.duplicate_order_id",
                    $"주문 ID '{duplicateOrderId}'가 두 번 이상 나타났습니다.",
                    "orderId"));
        }

        var duplicatePaymentId = FindDuplicateKey(payments, payment => payment.PaymentId);
        if (duplicatePaymentId is not null)
        {
            return Result<ReconciliationReport>.Failure(
                new Error(
                    "data.duplicate_payment_id",
                    $"결제 ID '{duplicatePaymentId}'가 두 번 이상 나타났습니다.",
                    "paymentId"));
        }

        var duplicatePaidOrderId = FindDuplicateKey(payments, payment => payment.OrderId);
        if (duplicatePaidOrderId is not null)
        {
            return Result<ReconciliationReport>.Failure(
                new Error(
                    "data.multiple_payments_for_order",
                    $"주문 ID '{duplicatePaidOrderId}'에 결제가 두 건 이상 연결되었습니다.",
                    "orderId"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // .NET 10 LeftJoin은 왼쪽 orders의 모든 원소를 반드시 한 번 이상 남깁니다.
        // 연결된 payment가 없을 때 resultSelector의 payment는 null이므로 누락을 숨기지 않습니다.
        var orderLines = orders.LeftJoin(
                payments,
                order => order.OrderId,
                payment => payment.OrderId,
                (order, payment) => _policy.Compare(order, payment),
                StringComparer.Ordinal)
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        // HashSet은 "이 ID가 존재하는가?"를 반복 확인할 때 선형 List 검색보다 의도가 분명하고 효율적입니다.
        var orderIds = orders
            .Select(order => order.OrderId)
            .ToHashSet(StringComparer.Ordinal);
        var orphanLines = payments
            .Where(payment => !orderIds.Contains(payment.OrderId))
            .Select(_policy.MarkOrphan);

        // LINQ는 기본적으로 지연 실행되므로 Create가 ToImmutableArray로 한 번 materialize해 snapshot을 고정합니다.
        var combinedLines = orderLines.Concat(orphanLines);
        cancellationToken.ThrowIfCancellationRequested();

        // Report factory가 정렬과 snapshot 생성을 보장하므로 모든 public 생성 경로가 같은 불변식을 지킵니다.
        var report = ReconciliationReport.Create(combinedLines);
        // 정렬·복사가 진행되는 동안 들어온 취소도 성공 결과를 반환하기 전에 마지막으로 관찰합니다.
        cancellationToken.ThrowIfCancellationRequested();
        return Result<ReconciliationReport>.Success(report);
    }

    /// <summary>
    /// sequence에서 같은 문자열 key가 두 번 이상 등장하면 첫 중복 key를 찾습니다.
    /// </summary>
    /// <typeparam name="TItem">주문이나 결제처럼 검사할 원소 형식입니다.</typeparam>
    /// <param name="items">중복 key를 검사할 원소 sequence입니다.</param>
    /// <param name="keySelector">각 원소에서 비교할 문자열 key를 꺼내는 함수입니다.</param>
    /// <returns>첫 중복 key를 반환하고, 모든 key가 유일하면 null을 반환합니다.</returns>
    private static string? FindDuplicateKey<TItem>(
        IEnumerable<TItem> items,
        Func<TItem, string> keySelector)
    {
        // GroupBy의 comparer를 명시해 식별자는 문화권이나 대소문자 추측 없이 정확히 비교합니다.
        return items
            .GroupBy(keySelector, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Skip(1).Any())
            ?.Key;
    }
}
