using InventoryWatcherExercise.Application;
using InventoryWatcherExercise.Domain;
using Microsoft.Extensions.Logging;

namespace InventoryWatcherExercise.Infrastructure;

/// <summary>
/// 학습용 재고 스냅샷을 메모리에서 제공하는 Repository 어댑터입니다.
/// 실제 서비스에서는 같은 포트를 구현하는 데이터베이스 어댑터로 교체할 수 있습니다.
/// </summary>
public sealed class DemoInventoryRepository : IInventoryRepository
{
    /// <summary>
    /// 정상, 부족, 품절 상태를 모두 관찰할 수 있는 데모 재고를 비동기 계약에 맞춰 반환합니다.
    /// </summary>
    /// <param name="cancellationToken">읽기를 시작하기 전에 확인할 호스트 종료 신호입니다.</param>
    /// <returns>SKU가 유일한 세 개의 검증된 재고 항목을 읽기 전용 목록으로 반환합니다.</returns>
    public Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 대괄호 컬렉션 식은 여러 항목으로 배열이나 목록을 간결하게 만드는 C# 12+ 문법이다.
        IReadOnlyList<InventoryItem> items =
        [
            RequireValid("CAB-100", "USB-C 케이블", 0, 5),
            RequireValid("MOU-200", "무선 마우스", 3, 5),
            RequireValid("KEY-300", "기계식 키보드", 12, 4),
        ];

        // Task.FromResult는 이미 메모리에 있는 값을 완료된 Task로 감싸 비동기 Repository 계약을 지킨다.
        // 데모는 I/O가 없으므로 불필요하게 별도 스레드를 만들지 않는다.
        return Task.FromResult(items);
    }

    /// <summary>
    /// 코드에 고정한 데모 시드가 유효한지 확인하고 성공 값을 꺼냅니다.
    /// 시드 오류는 사용자가 고칠 입력이 아니라 어댑터 개발자의 계약 버그이므로 예외로 바꿉니다.
    /// </summary>
    /// <param name="sku">데모 상품 식별자입니다.</param>
    /// <param name="name">데모 상품 이름입니다.</param>
    /// <param name="quantity">데모 현재 수량입니다.</param>
    /// <param name="reorderPoint">데모 재주문 기준입니다.</param>
    /// <returns>도메인 검증을 통과한 재고 항목을 반환합니다.</returns>
    private static InventoryItem RequireValid(
        string sku,
        string name,
        int quantity,
        int reorderPoint)
    {
        var result = InventoryItem.Create(sku, name, quantity, reorderPoint);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"데모 재고 설정 오류: {result.Error}");
        }

        return result.Value;
    }
}

/// <summary>
/// 저재고 경고를 구조화된 콘솔 로그로 출력하는 학습용 출력 어댑터입니다.
/// </summary>
public sealed class ConsoleLowStockAlertSink : ILowStockAlertSink
{
    private readonly ILogger<ConsoleLowStockAlertSink> _logger;

    /// <summary>
    /// Generic Host가 제공한 로거를 저장해 콘솔 출력 어댑터를 준비합니다. 생성자는 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="logger">로그 수준과 출력 형식을 호스트 설정에 따라 적용하는 로거입니다.</param>
    public ConsoleLowStockAlertSink(ILogger<ConsoleLowStockAlertSink> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 정렬된 저재고 경고를 한 줄씩 콘솔 로그로 출력합니다.
    /// </summary>
    /// <param name="alerts">Application Service가 긴급도와 SKU 순서로 정렬한 경고입니다.</param>
    /// <param name="cancellationToken">각 출력 전에 확인할 호스트 종료 신호입니다.</param>
    /// <returns>모든 동기 로그 출력이 끝난 상태의 Task를 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
    public Task PublishAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alerts);

        foreach (var alert in alerts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogWarning(
                "재고 경고 | 단계={Level} | SKU={Sku} | 상품={Name} | 현재={Quantity} | 기본기준부족={BaseReorderShortfall}",
                alert.Level,
                alert.Item.Sku,
                alert.Item.Name,
                alert.Item.Quantity,
                alert.BaseReorderShortfallQuantity);
        }

        return Task.CompletedTask;
    }
}
