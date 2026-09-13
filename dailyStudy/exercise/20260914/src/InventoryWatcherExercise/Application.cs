using InventoryWatcherExercise.Domain;

namespace InventoryWatcherExercise.Application;

/// <summary>
/// 재고 저장 기술을 애플리케이션 계층에서 분리하는 Repository 포트입니다.
/// 구현이 메모리, 파일, 데이터베이스로 바뀌어도 감시 규칙은 바뀌지 않습니다.
/// </summary>
public interface IInventoryRepository
{
    /// <summary>
    /// 감시할 모든 재고의 한 시점 스냅샷을 비동기로 읽습니다.
    /// </summary>
    /// <param name="cancellationToken">호스트 종료 요청을 저장소 작업까지 전달하는 취소 신호입니다.</param>
    /// <returns>SKU가 중복되지 않고 null 항목이 없는 읽기 전용 재고 목록을 반환합니다.</returns>
    Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 재고 상태 판정 알고리즘을 교체할 수 있게 만드는 Strategy 포트입니다.
/// 계절이나 상품군마다 다른 정책이 생겨도 Application Service를 수정하지 않을 수 있습니다.
/// </summary>
public interface IStockThresholdStrategy
{
    /// <summary>
    /// 재고 한 건을 보고 정상, 부족, 품절 중 하나로 분류합니다.
    /// </summary>
    /// <param name="item">검증을 마친 재고 항목입니다.</param>
    /// <returns>정책에 따라 판정한 재고 상태를 반환합니다.</returns>
    StockLevel Classify(InventoryItem item);
}

/// <summary>
/// 경고 전달 기술을 감시 유스케이스에서 분리하는 출력 포트입니다.
/// 콘솔 대신 이메일이나 메시지 큐를 붙여도 핵심 로직은 그대로 유지됩니다.
/// </summary>
public interface ILowStockAlertSink
{
    /// <summary>
    /// 한 감시 회차에서 만들어진 저재고 경고를 외부로 전달합니다.
    /// </summary>
    /// <param name="alerts">긴급도와 SKU 순서로 정렬된 하나 이상의 경고입니다.</param>
    /// <param name="cancellationToken">호스트 종료 요청을 외부 전송 작업까지 전달하는 취소 신호입니다.</param>
    /// <returns>전달 작업의 완료를 나타내는 Task를 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
    Task PublishAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken);
}

/// <summary>
/// 백그라운드 Worker가 매 주기 실행할 한 번의 재고 감시 유스케이스입니다.
/// </summary>
public interface IInventoryWatchCycle
{
    /// <summary>
    /// 저장소를 한 번 읽고 저재고를 판정한 뒤 필요한 경고를 전송합니다.
    /// </summary>
    /// <param name="cancellationToken">이번 회차 전체를 중단할 수 있는 취소 신호입니다.</param>
    /// <returns>검사 건수와 전송한 경고를 담은 회차 보고서를 반환합니다.</returns>
    Task<WatchCycleReport> RunAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 한 번의 감시 실행 결과를 호출자에게 전달하는 불변 보고서입니다.
/// 받은 컬렉션을 복사하고 중복을 거부하며 결정적 순서로 정렬하므로 이미 끝난 회차의 기록은 바뀌지 않습니다.
/// </summary>
public sealed class WatchCycleReport
{
    /// <summary>
    /// 검사 건수와 경고 스냅샷을 모순 없는 보고서로 만듭니다.
    /// 생성자는 새 보고서만 초기화하며 별도 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="inspectedCount">저장소에서 검사한 0 이상의 전체 재고 건수입니다.</param>
    /// <param name="alerts">호출자가 이번 회차 보고서에 담으려는 경고 열거입니다.</param>
    public WatchCycleReport(int inspectedCount, IEnumerable<StockAlert> alerts)
    {
        if (inspectedCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inspectedCount),
                inspectedCount,
                "검사 건수는 0 이상이어야 합니다.");
        }

        ArgumentNullException.ThrowIfNull(alerts);
        var snapshot = alerts.ToArray();
        if (snapshot.Any(alert => alert is null))
        {
            throw new ArgumentException("경고 목록에는 null 항목이 있을 수 없습니다.", nameof(alerts));
        }

        if (snapshot.Any(alert => alert.Level == StockLevel.Healthy))
        {
            throw new ArgumentException("보고서에는 실제 저재고 경고만 담을 수 있습니다.", nameof(alerts));
        }

        if (snapshot.Length > inspectedCount)
        {
            throw new ArgumentException("경고 건수는 검사 건수보다 많을 수 없습니다.", nameof(alerts));
        }

        var uniqueSkus = new HashSet<string>(StringComparer.Ordinal);
        foreach (var alert in snapshot)
        {
            if (!uniqueSkus.Add(alert.Item.Sku))
            {
                throw new ArgumentException(
                    $"보고서에는 같은 SKU '{alert.Item.Sku}'를 두 번 담을 수 없습니다.",
                    nameof(alerts));
            }
        }

        InspectedCount = inspectedCount;
        // Array.AsReadOnly는 복사한 배열을 수정 메서드가 없는 읽기 전용 뷰로 감싼다.
        Alerts = Array.AsReadOnly(
            snapshot
                .OrderByDescending(alert => alert.Level)
                .ThenBy(alert => alert.Item.Sku, StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>저장소에서 검사한 전체 재고 건수입니다.</summary>
    public int InspectedCount { get; }

    /// <summary>호출자가 제공한 경고를 생성 시점에 복사하고 긴급도·SKU 순으로 정렬한 읽기 전용 목록입니다.</summary>
    public IReadOnlyList<StockAlert> Alerts { get; }
}

/// <summary>
/// 기본 저재고 판정 정책입니다. 수량이 0이면 품절, 재주문 기준 이하면 부족으로 분류합니다.
/// </summary>
public sealed class DefaultStockThresholdStrategy : IStockThresholdStrategy
{
    /// <summary>
    /// 기본 임계값 규칙으로 재고 상태를 판정합니다.
    /// </summary>
    /// <param name="item">상태를 판정할 null이 아닌 재고 항목입니다.</param>
    /// <returns>품절이면 Critical, 기준 이하면 Low, 그 밖에는 Healthy를 반환합니다.</returns>
    public StockLevel Classify(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Quantity == 0)
        {
            return StockLevel.Critical;
        }

        if (item.Quantity <= item.ReorderPoint)
        {
            return StockLevel.Low;
        }

        return StockLevel.Healthy;
    }
}

/// <summary>
/// Repository에서 읽고 Strategy로 판정하고 Sink로 보내는 순서를 조정하는 Application Service입니다.
/// 업무 흐름만 책임지고 저장·출력의 구체 기술은 인터페이스 뒤로 숨깁니다.
/// </summary>
public sealed class InventoryWatchCycle : IInventoryWatchCycle
{
    private readonly IInventoryRepository _repository;
    private readonly IStockThresholdStrategy _strategy;
    private readonly ILowStockAlertSink _alertSink;

    /// <summary>
    /// 한 회차에 필요한 포트들을 주입받아 Application Service를 구성합니다. 생성자는 의존성만 저장하며 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="repository">재고 스냅샷을 제공하는 Repository 포트입니다.</param>
    /// <param name="strategy">각 재고를 위험 단계로 분류하는 Strategy 포트입니다.</param>
    /// <param name="alertSink">정렬된 저재고 경고를 외부로 전달하는 출력 포트입니다.</param>
    public InventoryWatchCycle(
        IInventoryRepository repository,
        IStockThresholdStrategy strategy,
        ILowStockAlertSink alertSink)
    {
        // `?? throw`는 왼쪽 값이 null일 때 즉시 오른쪽 예외를 던지는 null 병합 문법이다.
        // DI 컨테이너나 수동 조립의 null은 사용자 실패가 아닌 프로그램 계약 버그이므로 생성 시점에 막는다.
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _alertSink = alertSink ?? throw new ArgumentNullException(nameof(alertSink));
    }

    // async/await는 I/O가 끝날 때까지 스레드를 붙잡지 않고 기다리는 비동기 문법이다.
    // 저장소와 경고 전송이 느려져도 호스트의 다른 작업을 막지 않기 위해 사용한다.
    /// <summary>
    /// 재고 스냅샷을 검증하고 저재고만 결정적인 순서로 골라 경고를 전송합니다.
    /// </summary>
    /// <param name="cancellationToken">저장소 조회, 판정, 전송 사이마다 확인할 취소 신호입니다.</param>
    /// <returns>전체 검사 수와 실제 전송한 경고 목록을 담은 보고서를 비동기로 반환합니다.</returns>
    public async Task<WatchCycleReport> RunAsync(CancellationToken cancellationToken)
    {
        // 취소는 실패 Result나 일반 예외로 바꾸지 않는다. 호출자가 '오류'와 '정상 종료 요청'을 구분할 수 있어야 한다.
        cancellationToken.ThrowIfCancellationRequested();
        var items = await _repository.GetAllAsync(cancellationToken);

        // `is null`은 패턴 매칭 문법이다. 인터페이스의 non-null 계약을 어긴 어댑터 버그를 경계에서 명확히 잡는다.
        if (items is null)
        {
            throw new InvalidOperationException("Repository 계약 위반: 재고 목록은 null일 수 없습니다.");
        }

        var seenSkus = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            // 계약 검증 자체가 큰 목록에서 오래 걸릴 수 있으므로 이 반복에서도 항목마다 취소를 확인한다.
            cancellationToken.ThrowIfCancellationRequested();
            if (item is null)
            {
                throw new InvalidOperationException("Repository 계약 위반: 재고 항목은 null일 수 없습니다.");
            }

            if (!seenSkus.Add(item.Sku))
            {
                // `$"...{값}..."`은 값과 글을 합치는 문자열 보간 문법으로, 어떤 SKU가 문제인지 메시지에 넣는다.
                throw new InvalidOperationException($"Repository 계약 위반: SKU '{item.Sku}'가 중복되었습니다.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // LINQ는 컬렉션을 '변환 → 필터 → 정렬' 순서로 읽기 쉽게 표현하는 C#/.NET 도구다.
        // 람다식 `=>`는 각 항목에 적용할 짧은 함수를 뜻하며, 결과를 배열로 확정해 이후 원본 변경의 영향을 막는다.
        var alerts = items
            .Select(item =>
            {
                // 큰 스냅샷에서도 종료 요청에 빨리 반응하도록 각 항목의 판정 전에 같은 토큰을 다시 확인한다.
                cancellationToken.ThrowIfCancellationRequested();
                var level = _strategy.Classify(item);
                if (!Enum.IsDefined(level))
                {
                    // 정의되지 않은 enum 값은 사용자 재고 문제가 아니라 Strategy 구현이 Port 계약을 어긴 버그다.
                    throw new InvalidOperationException(
                        $"Strategy 계약 위반: 정의되지 않은 재고 단계 '{level}'입니다.");
                }

                // `(Item: ..., Level: ...)`은 관련된 두 값을 이름 붙인 tuple 한 쌍으로 잠시 묶는 문법이다.
                return (Item: item, Level: level);
            })
            .Where(result => result.Level != StockLevel.Healthy)
            .Select(result => new StockAlert(result.Item, result.Level))
            .OrderByDescending(alert => alert.Level)
            .ThenBy(alert => alert.Item.Sku, StringComparer.Ordinal)
            .ToArray();

        // 마지막 Strategy 호출에서 토큰이 취소된 뒤 Healthy를 반환해도 성공으로 오인하지 않도록 다시 확인한다.
        cancellationToken.ThrowIfCancellationRequested();

        if (alerts.Length > 0)
        {
            await _alertSink.PublishAsync(alerts, cancellationToken);
        }

        // Sink가 성공한 뒤에는 다시 취소를 확인하지 않는다. 이미 발생한 외부 전송을 취소로 보고 재시도하면 중복 경고가 생길 수 있다.
        return new WatchCycleReport(items.Count, alerts);
    }
}
