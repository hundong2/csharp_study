// 학습 흐름을 한 파일에서 따라갈 수 있게 구성했습니다. 실무에서는 역할별 파일과 프로젝트로 나누는 편이 좋습니다.

var requests = new[]
{
    new RefundRequest("RF-1001", "ORD-101", 12_000m, 2, RefundReason.Defective, "CARD"),
    new RefundRequest("RF-1002", "ORD-102", 80_000m, 20, RefundReason.ChangeOfMind, "BANK"),
    new RefundRequest("RF-1003", "ORD-103", -1_000m, 1, RefundReason.Defective, "CARD"),
    new RefundRequest("RF-1001", "ORD-104", 5_000m, 1, RefundReason.DeliveryDelay, null)
};

// Composition Root는 구현 객체를 한곳에서 조립합니다. 핵심 로직이 구체 클래스 생성에 묶이지 않아 교체와 테스트가 쉬워집니다.
IRefundRepository repository = new InMemoryRefundRepository();
IRefundPolicy policy = new StandardRefundPolicy();
IRefundMethodStrategy methodStrategy = new RefundMethodStrategy();
var service = new ReviewRefundsService(repository, policy, methodStrategy);

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var batch = await service.ExecuteAsync(requests, CancellationToken.None);
Console.WriteLine($"승인 {batch.Approved.Count}건 / 거절 {batch.Rejected.Count}건");
foreach (var decision in batch.Approved)
    Console.WriteLine($"{decision.RequestId}: {decision.Method}, {decision.Amount:N0}원 - {decision.Note}");
foreach (var rejection in batch.Rejected)
    Console.WriteLine($"{rejection.RequestId}: 거절 - {rejection.Reason}");

enum RefundReason { Defective, DeliveryDelay, ChangeOfMind }
enum RefundMethod { OriginalPayment, BankTransfer, ManualReview }

// record는 값 중심 데이터를 간결하게 나타냅니다. init 전용 상태라 처리 중 입력이 몰래 바뀌는 오류도 줄입니다.
sealed record RefundRequest(
    string Id,
    string OrderId,
    decimal Amount,
    int DaysSincePurchase,
    RefundReason Reason,
    string? OriginalPaymentCode);

sealed record RefundDecision(string RequestId, decimal Amount, RefundMethod Method, string Note);
sealed record RefundRejection(string RequestId, string Reason);
sealed record RefundBatchResult(
    IReadOnlyList<RefundDecision> Approved,
    IReadOnlyList<RefundRejection> Rejected);

// 예상 가능한 업무 실패는 Result로 돌려줍니다. 호출자가 실패를 정상 흐름으로 읽고 처리하게 만드는 설계입니다.
sealed record Result<T>(bool IsSuccess, T Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);

    // 실패 때 Value는 읽지 않는다는 계약입니다. default!는 컴파일러에 그 계약을 알리며, 실제 코드는 IsSuccess를 먼저 확인해야 합니다.
    public static Result<T> Failure(string error) => new(false, default!, error);
}

interface IRefundRepository
{
    Task<bool> ExistsAsync(string requestId, CancellationToken cancellationToken);
    Task SaveAsync(RefundDecision decision, CancellationToken cancellationToken);
}

interface IRefundPolicy
{
    Result<decimal> Validate(RefundRequest request);
}

interface IRefundMethodStrategy
{
    RefundMethod Select(RefundRequest request, decimal approvedAmount);
}

// 정책을 별도 클래스로 둬 단일 책임 원칙(SRP)을 지킵니다. 규칙 변경이 처리 순서 코드에 번지지 않습니다.
sealed class StandardRefundPolicy : IRefundPolicy
{
    public Result<decimal> Validate(RefundRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.OrderId))
            return Result<decimal>.Failure("환불 ID와 주문 ID는 필수입니다.");
        if (request.Amount <= 0)
            return Result<decimal>.Failure("환불 금액은 0보다 커야 합니다.");
        if (request.DaysSincePurchase < 0)
            return Result<decimal>.Failure("구매 후 날짜는 음수일 수 없습니다.");
        if (request.Reason == RefundReason.ChangeOfMind && request.DaysSincePurchase > 14)
            return Result<decimal>.Failure("단순 변심 환불 기한 14일이 지났습니다.");

        return Result<decimal>.Success(request.Amount);
    }
}

// Strategy는 환불 수단 선택 규칙을 캡슐화합니다. 새 결제 수단이 생겨도 서비스의 흐름은 유지할 수 있습니다.
sealed class RefundMethodStrategy : IRefundMethodStrategy
{
    public RefundMethod Select(RefundRequest request, decimal approvedAmount)
    {
        if (approvedAmount >= 50_000m)
            return RefundMethod.ManualReview;

        return request.OriginalPaymentCode?.ToUpperInvariant() switch
        {
            "CARD" => RefundMethod.OriginalPayment,
            "BANK" => RefundMethod.BankTransfer,
            _ => RefundMethod.ManualReview
        };
    }
}

// Application Service는 조회, 정책 적용, 저장의 순서만 조정합니다. 업무 규칙 자체는 Domain 객체와 Strategy에 맡깁니다.
sealed class ReviewRefundsService(
    IRefundRepository repository,
    IRefundPolicy policy,
    IRefundMethodStrategy methodStrategy)
{
    public async Task<RefundBatchResult> ExecuteAsync(
        IEnumerable<RefundRequest> requests,
        CancellationToken cancellationToken)
    {
        var approved = new List<RefundDecision>();
        var rejected = new List<RefundRejection>();

        // LINQ OrderBy로 처리 순서를 결정적으로 만들어 로그 비교와 재현 가능한 테스트에 도움을 줍니다.
        foreach (var request in requests.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await repository.ExistsAsync(request.Id, cancellationToken))
            {
                rejected.Add(new(request.Id, "이미 처리된 환불 요청입니다."));
                continue;
            }

            var validation = policy.Validate(request);
            if (!validation.IsSuccess)
            {
                rejected.Add(new(request.Id, validation.Error ?? "알 수 없는 검증 실패"));
                continue;
            }

            var method = methodStrategy.Select(request, validation.Value);
            var note = method == RefundMethod.ManualReview ? "고액 또는 결제 정보 누락으로 담당자 확인이 필요합니다." : "자동 환불 조건을 통과했습니다.";
            var decision = new RefundDecision(request.Id, validation.Value, method, note);
            await repository.SaveAsync(decision, cancellationToken);
            approved.Add(decision);
        }

        return new(approved, rejected);
    }
}

// 메모리 Repository는 학습용 대역입니다. 인터페이스 덕분에 서비스 변경 없이 DB 구현으로 교체할 수 있습니다.
sealed class InMemoryRefundRepository : IRefundRepository
{
    private readonly Dictionary<string, RefundDecision> _decisions = new(StringComparer.Ordinal);

    public Task<bool> ExistsAsync(string requestId, CancellationToken cancellationToken)
        => Task.FromResult(_decisions.ContainsKey(requestId));

    public Task SaveAsync(RefundDecision decision, CancellationToken cancellationToken)
    {
        _decisions.Add(decision.RequestId, decision);
        return Task.CompletedTask;
    }
}

static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var policy = new StandardRefundPolicy();
        Check(!policy.Validate(new("T1", "O1", 0m, 1, RefundReason.Defective, "CARD")).IsSuccess, "0원 거절");
        passed++;
        Check(!policy.Validate(new("T2", "O2", 1_000m, 15, RefundReason.ChangeOfMind, "CARD")).IsSuccess, "변심 기한 거절");
        passed++;

        var strategy = new RefundMethodStrategy();
        Check(strategy.Select(new("T3", "O3", 10_000m, 1, RefundReason.Defective, "CARD"), 10_000m) == RefundMethod.OriginalPayment, "카드 원결제 환불");
        passed++;

        var repository = new InMemoryRefundRepository();
        var service = new ReviewRefundsService(repository, policy, strategy);
        var request = new RefundRequest("T4", "O4", 1_000m, 1, RefundReason.Defective, "CARD");
        await service.ExecuteAsync([request], CancellationToken.None);
        var repeated = await service.ExecuteAsync([request], CancellationToken.None);
        Check(repeated.Rejected.Single().Reason.Contains("이미 처리", StringComparison.Ordinal), "중복 요청 거절");
        passed++;
        Console.WriteLine($"self-test 통과: {passed}/4");
    }

    private static void Check(bool condition, string name)
    {
        // 테스트 실패는 정상 업무 결과가 아니라 코드 계약 위반이므로 예외로 즉시 드러냅니다.
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
    }
}
