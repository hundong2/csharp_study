using System.Collections.ObjectModel;
using OrderIntakeApi.Application.Ports;

namespace OrderIntakeApi.Infrastructure;

/// <summary>
/// 학습용 가격 데이터와 정상·일시 장애·버그 상태를 제공하는 Infrastructure Adapter입니다.
/// </summary>
public sealed class DemoProductCatalog : IProductCatalog
{
    private readonly IReadOnlyDictionary<string, decimal> _prices;
    private readonly TimeSpan _latency;
    private int _behavior = (int)CatalogBehavior.Healthy;
    private int _observedCancellationCount;

    /// <summary>
    /// 고정 상품 가격과 선택적 지연을 가진 학습용 Adapter를 만듭니다.
    /// </summary>
    /// <param name="latency">실제 I/O 대기를 흉내 낼 지연이며 생략하면 25ms입니다.</param>
    /// <returns>생성자는 Adapter를 초기화하므로 별도 반환값은 없습니다.</returns>
    public DemoProductCatalog(TimeSpan? latency = null)
    {
        _latency = latency ?? TimeSpan.FromMilliseconds(25);
        if (_latency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(latency), "지연은 음수일 수 없습니다.");
        }

        var prices = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["BOOK-CS"] = 32_000m,
            ["MUG-DOTNET"] = 18_000m,
            ["STICKER-CLR"] = 2_500m
        };
        _prices = new ReadOnlyDictionary<string, decimal>(prices);
    }

    /// <summary>
    /// 현재 장애 주입 상태를 thread-safe하게 읽습니다.
    /// </summary>
    public CatalogBehavior Behavior => (CatalogBehavior)Volatile.Read(ref _behavior);

    /// <summary>
    /// 취소 토큰이 실제 대기에서 관찰된 횟수를 가져옵니다.
    /// </summary>
    public int ObservedCancellationCount => Volatile.Read(ref _observedCancellationCount);

    /// <summary>
    /// 요청한 SKU 중 존재하는 가격만 반환하거나 설정된 장애를 발생시킵니다.
    /// </summary>
    /// <param name="skus">조회할 정규화 SKU 집합입니다.</param>
    /// <param name="cancellationToken">대기와 조회를 중단할 신호입니다.</param>
    /// <returns>찾은 SKU와 가격의 읽기 전용 사전을 반환합니다.</returns>
    public async Task<IReadOnlyDictionary<string, decimal>> GetUnitPricesAsync(
        IReadOnlySet<string> skus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(_latency, cancellationToken);
        }
        // catch when은 이 Adapter가 받은 토큰이 원인인 취소만 세고 같은 예외를 다시 던집니다.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Interlocked.Increment(ref _observedCancellationCount);
            throw;
        }

        // switch expression은 현재 상태 하나를 결과 또는 예외 동작 하나로 대응시킵니다.
        return Behavior switch
        {
            CatalogBehavior.Healthy => FindExistingPrices(skus),
            CatalogBehavior.Unavailable => throw new ProductCatalogUnavailableException(
                "demo catalog가 일시적으로 응답하지 않습니다."),
            CatalogBehavior.Bug => throw new InvalidOperationException(
                "demo adapter의 의도적인 내부 결함입니다."),
            _ => throw new InvalidOperationException("알 수 없는 catalog 상태입니다.")
        };
    }

    /// <summary>
    /// Development 검증용으로 다음 가격 조회의 동작 상태를 바꿉니다.
    /// </summary>
    /// <param name="behavior">정상, 일시 장애, 내부 버그 중 하나입니다.</param>
    /// <returns>상태만 변경하므로 반환값은 없습니다.</returns>
    public void SetBehavior(CatalogBehavior behavior)
    {
        if (!Enum.IsDefined(behavior))
        {
            throw new ArgumentOutOfRangeException(nameof(behavior), "정의된 catalog 상태가 아닙니다.");
        }

        // Volatile.Write는 동시에 들어오는 요청도 완성된 최신 상태를 보도록 memory barrier를 제공합니다.
        Volatile.Write(ref _behavior, (int)behavior);
    }

    /// <summary>
    /// 요청한 SKU와 일치하는 고정 가격만 새 사전에 복사합니다.
    /// </summary>
    /// <param name="skus">조회할 SKU 집합입니다.</param>
    /// <returns>없는 SKU를 제외한 가격 사전을 반환합니다.</returns>
    private IReadOnlyDictionary<string, decimal> FindExistingPrices(IReadOnlySet<string> skus)
    {
        return _prices
            .Where(pair => skus.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}

/// <summary>
/// 학습용 카탈로그 Adapter가 취할 수 있는 동작입니다.
/// </summary>
public enum CatalogBehavior
{
    Healthy,
    Unavailable,
    Bug
}
