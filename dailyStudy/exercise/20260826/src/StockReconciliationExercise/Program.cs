// 읽는 순서: 실행 예제 → Domain Model → 계약 → Application Service → 구현 → 자체 테스트입니다.
var countedAt = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.FromHours(9));
var repository = new InMemoryStockRepository([
    new("COUNT-101", "SKU-A", 10, 8, countedAt.AddMinutes(-20), 1),
    new("COUNT-102", "SKU-B", 20, 20, countedAt.AddMinutes(-15), 1),
    new("COUNT-103", "SKU-C", 100, 70, countedAt.AddMinutes(-10), 3),
    new("COUNT-104", "", 5, 4, countedAt.AddMinutes(-5), 1)
]);

// Composition Root 한 곳에서 구현을 조립하면 업무 로직이 구체 저장 기술과 분리되어 테스트하기 쉽습니다(DI/DIP).
IReconciliationPolicy policy = new ThresholdReconciliationPolicy(autoAdjustLimit: 5);
var service = new ReconcileStockService(repository, policy, new ConsoleAuditLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(countedAt, CancellationToken.None);
if (!result.IsSuccess)
{
    Console.WriteLine($"처리 실패: {result.Error}");
    return;
}

// 성공 여부를 먼저 확인했으므로 Value가 null이 아님을 null-forgiving 연산자(!)로 알려 줍니다.
var summary = result.Value!;
Console.WriteLine($"자동 조정 {summary.AutoAdjustCount}건, 유지 {summary.NoChangeCount}건, 수동 검토 {summary.ReviewCount}건, 거절 {summary.RejectCount}건");
foreach (var item in summary.Items)
    Console.WriteLine($"- {item.CountId}: {item.Decision}, 차이 {item.Difference} ({item.Reason})");

// enum은 허용된 결정을 이름으로 제한해 문자열 오타와 잘못된 상태를 줄입니다.
enum ReconciliationDecision { AutoAdjust, NoChange, ManualReview, Reject }

// record는 값 중심 불변 데이터에 적합하며, int와 DateTimeOffset은 각각 정수 수량과 시간대가 있는 시각을 표현합니다.
sealed record StockCount(string Id, string Sku, int SystemQuantity, int CountedQuantity, DateTimeOffset CountedAt, int Version);
sealed record Reconciliation(string CountId, string Sku, int Difference, ReconciliationDecision Decision, string Reason, int ExpectedVersion);
sealed record ReconciliationSummary(IReadOnlyList<Reconciliation> Items)
{
    // LINQ Count는 조건별 개수를 센다는 의도를 반복문보다 간결하게 보여 줍니다.
    public int AutoAdjustCount => Items.Count(x => x.Decision == ReconciliationDecision.AutoAdjust);
    public int NoChangeCount => Items.Count(x => x.Decision == ReconciliationDecision.NoChange);
    public int ReviewCount => Items.Count(x => x.Decision == ReconciliationDecision.ManualReview);
    public int RejectCount => Items.Count(x => x.Decision == ReconciliationDecision.Reject);
}

// 예상 가능한 업무 실패는 Result로 반환하고, DB 단절이나 코드 결함 같은 예기치 못한 장애는 예외로 남깁니다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface IStockRepository
{
    Task<IReadOnlyList<StockCount>> GetPendingAsync(CancellationToken cancellationToken);
    Task<Result<bool>> SaveAsync(Reconciliation reconciliation, CancellationToken cancellationToken);
}

// Strategy 계약 뒤에 기준을 숨겨 창고별 조정 정책을 서비스 변경 없이 추가할 수 있습니다(OCP).
interface IReconciliationPolicy { Reconciliation Decide(StockCount count, DateTimeOffset now); }
interface IAuditLog { void Saved(Reconciliation reconciliation); }

// Application Service는 조회→판단→저장의 사용 사례 순서만 담당하고 각 규칙은 협력 객체에 맡깁니다(SRP).
sealed class ReconcileStockService(IStockRepository repository, IReconciliationPolicy policy, IAuditLog auditLog)
{
    public async Task<Result<ReconciliationSummary>> ExecuteAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var counts = await repository.GetPendingAsync(cancellationToken);
        if (counts.Count == 0)
            return Result<ReconciliationSummary>.Failure("처리할 실사 결과가 없습니다.");

        var reconciliations = new List<Reconciliation>();
        // 명시적 정렬은 저장소의 반환 순서가 달라도 실행과 테스트 결과를 일정하게 만듭니다.
        foreach (var count in counts.OrderBy(x => x.CountedAt).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reconciliation = policy.Decide(count, now);
            var saved = await repository.SaveAsync(reconciliation, cancellationToken);
            if (!saved.IsSuccess)
                return Result<ReconciliationSummary>.Failure($"{count.Id} 저장 실패: {saved.Error}");

            reconciliations.Add(reconciliation);
            auditLog.Saved(reconciliation);
        }

        return Result<ReconciliationSummary>.Success(new(reconciliations.AsReadOnly()));
    }
}

sealed class ThresholdReconciliationPolicy(int autoAdjustLimit) : IReconciliationPolicy
{
    public Reconciliation Decide(StockCount count, DateTimeOffset now)
    {
        // 경계에서 잘못된 입력을 거절하면 유효하지 않은 수량이나 식별자가 핵심 로직 안으로 퍼지지 않습니다.
        if (string.IsNullOrWhiteSpace(count.Id) || string.IsNullOrWhiteSpace(count.Sku))
            return new(count.Id, count.Sku, 0, ReconciliationDecision.Reject, "실사 ID와 SKU가 필요합니다.", count.Version);
        if (count.SystemQuantity < 0 || count.CountedQuantity < 0 || count.Version < 1)
            return new(count.Id, count.Sku, 0, ReconciliationDecision.Reject, "수량은 0 이상이고 버전은 1 이상이어야 합니다.", count.Version);
        if (count.CountedAt > now.AddMinutes(1))
            return new(count.Id, count.Sku, 0, ReconciliationDecision.Reject, "미래 시각의 실사입니다.", count.Version);

        var difference = count.CountedQuantity - count.SystemQuantity;
        if (difference == 0)
            return new(count.Id, count.Sku, difference, ReconciliationDecision.NoChange, "장부와 실사 수량이 같습니다.", count.Version);
        if (Math.Abs(difference) <= autoAdjustLimit)
            return new(count.Id, count.Sku, difference, ReconciliationDecision.AutoAdjust, "허용 범위 안이라 자동 조정합니다.", count.Version);
        return new(count.Id, count.Sku, difference, ReconciliationDecision.ManualReview, "차이가 커서 원인 확인이 필요합니다.", count.Version);
    }
}

sealed class InMemoryStockRepository(IEnumerable<StockCount> seed) : IStockRepository
{
    private readonly List<StockCount> _pending = [.. seed];
    private readonly Dictionary<string, Reconciliation> _saved = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<StockCount>> GetPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본은 호출자가 저장소 내부 목록을 우연히 변경하지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<StockCount>>(_pending.ToArray());
    }

    public Task<Result<bool>> SaveAsync(Reconciliation reconciliation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 같은 결과의 재저장은 성공으로 처리해 재시도 때 재고가 두 번 조정되지 않게 합니다(멱등성).
        if (_saved.TryGetValue(reconciliation.CountId, out var existing))
            return Task.FromResult(existing == reconciliation ? Result<bool>.Success(true) : Result<bool>.Failure("이미 다른 조정 결과가 저장되었습니다."));
        _saved.Add(reconciliation.CountId, reconciliation);
        return Task.FromResult(Result<bool>.Success(true));
    }
}

sealed class ConsoleAuditLog : IAuditLog
{
    public void Saved(Reconciliation reconciliation)
    {
        // 감사 로그에는 추적에 필요한 ID·결정·버전만 남기고 담당자 개인정보는 넣지 않습니다.
        Console.WriteLine($"[audit] count={reconciliation.CountId} decision={reconciliation.Decision} version={reconciliation.ExpectedVersion}");
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.FromHours(9));
        var policy = new ThresholdReconciliationPolicy(5);
        var tests = new (string Name, Func<Task> Run)[]
        {
            ("같은 수량은 유지", () => AssertDecisionAsync(policy, new("T-1", "A", 10, 10, now, 1), now, ReconciliationDecision.NoChange)),
            ("작은 차이는 자동 조정", () => AssertDecisionAsync(policy, new("T-2", "B", 10, 7, now, 1), now, ReconciliationDecision.AutoAdjust)),
            ("큰 차이는 수동 검토", () => AssertDecisionAsync(policy, new("T-3", "C", 100, 70, now, 1), now, ReconciliationDecision.ManualReview)),
            ("서비스는 전체 항목 처리", ServiceProcessesAllAsync)
        };

        var passed = 0;
        foreach (var test in tests)
        {
            await test.Run();
            passed++;
            Console.WriteLine($"PASS: {test.Name}");
        }
        Console.WriteLine($"self-test {passed}/{tests.Length} 통과");
    }

    private static Task AssertDecisionAsync(IReconciliationPolicy policy, StockCount count, DateTimeOffset now, ReconciliationDecision expected)
    {
        var actual = policy.Decide(count, now).Decision;
        if (actual != expected) throw new InvalidOperationException($"예상 {expected}, 실제 {actual}");
        return Task.CompletedTask;
    }

    private static async Task ServiceProcessesAllAsync()
    {
        var now = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.FromHours(9));
        var repository = new InMemoryStockRepository([new("T-4", "D", 1, 2, now, 1)]);
        var result = await new ReconcileStockService(repository, new ThresholdReconciliationPolicy(5), new SilentLog()).ExecuteAsync(now, CancellationToken.None);
        if (!result.IsSuccess || result.Value!.Items.Count != 1) throw new InvalidOperationException("모든 항목이 처리되어야 합니다.");
    }

    private sealed class SilentLog : IAuditLog
    {
        // 테스트 대역은 콘솔 출력 부작용을 없애 반환값 검증에 집중하게 합니다.
        public void Saved(Reconciliation reconciliation) { }
    }
}
