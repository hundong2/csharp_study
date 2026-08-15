// 이 파일은 한 곳에서 실행 흐름을 따라가도록 구성했다. 실무에서는 역할별 파일로 나누는 편이 좋다.

var requests = new[]
{
    new TransferRequest("TR-1001", "SKU-A", 12, "SEOUL", "BUSAN", TransferUrgency.Normal),
    new TransferRequest("TR-1002", "SKU-B", 30, "SEOUL", "JEJU", TransferUrgency.Urgent),
    new TransferRequest("TR-1003", "SKU-C", 0, "BUSAN", "SEOUL", TransferUrgency.Normal),
    new TransferRequest("TR-1004", "SKU-A", 4, "SEOUL", "SEOUL", TransferUrgency.Normal)
};

// Composition Root는 구현 객체를 조립하는 유일한 곳이다. 핵심 로직이 구체 클래스 생성에 묶이지 않아 테스트가 쉬워진다.
ITransferRepository repository = new InMemoryTransferRepository();
IWarehouseStockReader stockReader = new InMemoryWarehouseStockReader(
    new Dictionary<(string Warehouse, string Sku), int>
    {
        [("SEOUL", "SKU-A")] = 20,
        [("SEOUL", "SKU-B")] = 10,
        [("BUSAN", "SKU-C")] = 50
    });
IApprovalStrategy approvalStrategy = new RiskBasedApprovalStrategy();
var service = new PlanTransfersService(repository, stockReader, approvalStrategy);

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(requests, CancellationToken.None);
Console.WriteLine($"승인 {result.Approved.Count}건 / 보류 {result.Rejected.Count}건");
foreach (var plan in result.Approved)
{
    Console.WriteLine($"{plan.RequestId}: {plan.Decision} - {plan.Reason}");
}
foreach (var rejection in result.Rejected)
{
    Console.WriteLine($"{rejection.RequestId}: 보류 - {rejection.Reason}");
}

enum TransferUrgency { Normal, Urgent }
enum ApprovalDecision { AutoApproved, ManualReview }

// record는 값 중심 데이터를 간결하게 표현하고 init 전용 상태로 불변성을 돕는다.
sealed record TransferRequest(
    string Id,
    string Sku,
    int Quantity,
    string SourceWarehouse,
    string DestinationWarehouse,
    TransferUrgency Urgency);

sealed record TransferPlan(string RequestId, ApprovalDecision Decision, string Reason);
sealed record TransferRejection(string RequestId, string Reason);
sealed record TransferBatchResult(
    IReadOnlyList<TransferPlan> Approved,
    IReadOnlyList<TransferRejection> Rejected);

// Result는 예상 가능한 입력 실패를 예외와 구분한다. 호출자는 성공 여부를 명시적으로 처리해야 한다.
sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

interface ITransferRepository
{
    Task<bool> ExistsAsync(string requestId, CancellationToken cancellationToken);
    Task SaveAsync(TransferPlan plan, CancellationToken cancellationToken);
}

interface IWarehouseStockReader
{
    Task<int> GetAvailableAsync(string warehouse, string sku, CancellationToken cancellationToken);
}

interface IApprovalStrategy
{
    Result<TransferPlan> Decide(TransferRequest request, int availableStock);
}

// Strategy는 바뀌기 쉬운 승인 규칙을 분리한다. 새 정책을 추가해도 서비스의 처리 순서를 고치지 않아도 된다.
sealed class RiskBasedApprovalStrategy : IApprovalStrategy
{
    public Result<TransferPlan> Decide(TransferRequest request, int availableStock)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Sku))
            return Result<TransferPlan>.Failure("요청 ID와 SKU는 필수입니다.");
        if (request.Quantity <= 0)
            return Result<TransferPlan>.Failure("수량은 1 이상이어야 합니다.");
        if (request.SourceWarehouse == request.DestinationWarehouse)
            return Result<TransferPlan>.Failure("출발 창고와 도착 창고가 같을 수 없습니다.");
        if (availableStock < request.Quantity)
            return Result<TransferPlan>.Failure($"재고 부족: 가용 {availableStock}, 요청 {request.Quantity}");

        var needsReview = request.Urgency == TransferUrgency.Urgent || request.Quantity >= 20;
        var decision = needsReview ? ApprovalDecision.ManualReview : ApprovalDecision.AutoApproved;
        var reason = needsReview ? "긴급 또는 대량 이동이라 담당자 확인이 필요합니다." : "재고와 기본 위험 규칙을 통과했습니다.";
        return Result<TransferPlan>.Success(new TransferPlan(request.Id, decision, reason));
    }
}

// Application Service는 검증, 조회, 정책 적용, 저장의 순서를 조정하고 세부 규칙은 협력 객체에 맡긴다.
sealed class PlanTransfersService(
    ITransferRepository repository,
    IWarehouseStockReader stockReader,
    IApprovalStrategy approvalStrategy)
{
    public async Task<TransferBatchResult> ExecuteAsync(
        IEnumerable<TransferRequest> requests,
        CancellationToken cancellationToken)
    {
        var approved = new List<TransferPlan>();
        var rejected = new List<TransferRejection>();

        // LINQ의 OrderBy는 입력 순서와 무관하게 결과를 재현 가능하게 만들어 운영 조사와 테스트에 유리하다.
        foreach (var request in requests.OrderBy(request => request.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await repository.ExistsAsync(request.Id, cancellationToken))
            {
                rejected.Add(new(request.Id, "이미 처리한 요청입니다."));
                continue;
            }

            var stock = await stockReader.GetAvailableAsync(request.SourceWarehouse, request.Sku, cancellationToken);
            var decision = approvalStrategy.Decide(request, stock);
            if (!decision.IsSuccess || decision.Value is null)
            {
                rejected.Add(new(request.Id, decision.Error ?? "알 수 없는 검증 실패"));
                continue;
            }

            await repository.SaveAsync(decision.Value, cancellationToken);
            approved.Add(decision.Value);
        }

        return new(approved, rejected);
    }
}

// 메모리 Repository는 학습용 구현이다. 인터페이스 덕분에 실제 DB 구현으로 교체해도 서비스는 유지된다.
sealed class InMemoryTransferRepository : ITransferRepository
{
    private readonly Dictionary<string, TransferPlan> _plans = new(StringComparer.Ordinal);
    public Task<bool> ExistsAsync(string requestId, CancellationToken cancellationToken)
        => Task.FromResult(_plans.ContainsKey(requestId));

    public Task SaveAsync(TransferPlan plan, CancellationToken cancellationToken)
    {
        _plans.Add(plan.RequestId, plan);
        return Task.CompletedTask;
    }
}

sealed class InMemoryWarehouseStockReader(Dictionary<(string Warehouse, string Sku), int> stocks)
    : IWarehouseStockReader
{
    public Task<int> GetAvailableAsync(string warehouse, string sku, CancellationToken cancellationToken)
        => Task.FromResult(stocks.GetValueOrDefault((warehouse, sku), 0));
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var strategy = new RiskBasedApprovalStrategy();
        Check(!strategy.Decide(new("T1", "A", 0, "S", "B", TransferUrgency.Normal), 10).IsSuccess, "0 수량 거부");
        passed++;
        Check(strategy.Decide(new("T2", "A", 5, "S", "B", TransferUrgency.Normal), 10).Value?.Decision == ApprovalDecision.AutoApproved, "소량 자동 승인");
        passed++;
        Check(strategy.Decide(new("T3", "A", 20, "S", "B", TransferUrgency.Normal), 30).Value?.Decision == ApprovalDecision.ManualReview, "대량 수동 검토");
        passed++;

        var repository = new InMemoryTransferRepository();
        var service = new PlanTransfersService(repository, new InMemoryWarehouseStockReader(new() { [("S", "A")] = 10 }), strategy);
        var request = new TransferRequest("T4", "A", 1, "S", "B", TransferUrgency.Normal);
        await service.ExecuteAsync([request], CancellationToken.None);
        var second = await service.ExecuteAsync([request], CancellationToken.None);
        Check(second.Rejected.Single().Reason.Contains("이미 처리", StringComparison.Ordinal), "중복 요청 거부");
        passed++;
        Console.WriteLine($"self-test 통과: {passed}/4");
    }

    private static void Check(bool condition, string name)
    {
        // 테스트 실패는 정상 업무 결과가 아니라 코드 계약 위반이므로 예외로 즉시 드러낸다.
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
    }
}
