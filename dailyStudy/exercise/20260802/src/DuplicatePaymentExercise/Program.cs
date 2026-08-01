// 오늘 예제는 한 파일에서 실행 흐름을 쉽게 따라가고, 이후 계층별 파일로 나누기 좋게 구성했습니다.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

// Composition Root: 프로그램 시작점 한 곳에서 구현체를 조립하면 업무 코드는 구체 클래스 생성법을 몰라도 됩니다.
var repository = new InMemoryPaymentRepository(
[
    new Payment("PAY-100", "customer-1", 35_000m, DateTimeOffset.Parse("2026-08-02T08:00:00+09:00")),
]);
IDuplicateRule rule = new SameCustomerAmountRule(TimeSpan.FromMinutes(10));
var service = new PaymentReviewService(repository, rule, new ConsoleAuditLog());

var command = new ReviewPaymentCommand(
    PaymentId: "PAY-101",
    CustomerId: "customer-1",
    Amount: 35_000m,
    PaidAt: DateTimeOffset.Parse("2026-08-02T08:04:00+09:00"),
    Note: null);

var result = await service.ReviewAsync(command, CancellationToken.None);
Console.WriteLine(result.IsSuccess
    ? $"검토 결과: {result.Value!.Status} / 사유: {result.Value.Reason}"
    : $"입력 오류: {result.Error}");

// record는 값 중심 데이터에 적합하고 init 전용 속성처럼 생성 뒤 상태를 바꾸지 않아 추론과 테스트가 쉽습니다.
public sealed record ReviewPaymentCommand(
    string PaymentId,
    string CustomerId,
    decimal Amount,
    DateTimeOffset PaidAt,
    string? Note);

public sealed record Payment(string Id, string CustomerId, decimal Amount, DateTimeOffset PaidAt);
public sealed record PaymentReview(string PaymentId, ReviewStatus Status, string Reason);

public enum ReviewStatus
{
    Approved,
    ManualReview
}

// 예상 가능한 입력 실패는 예외 대신 Result로 반환해 호출자가 성공과 실패를 명시적으로 처리하게 합니다.
public sealed record Result<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(string error) => new(default, error);
}

// Domain Model: 결제 자체의 불변 조건을 한곳에 두어 잘못된 객체가 저장소까지 들어가지 않게 합니다.
public static class PaymentFactory
{
    public static Result<Payment> Create(ReviewPaymentCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.PaymentId))
            return Result<Payment>.Failure("결제 ID는 필수입니다.");
        if (string.IsNullOrWhiteSpace(command.CustomerId))
            return Result<Payment>.Failure("고객 ID는 필수입니다.");
        if (command.Amount <= 0)
            return Result<Payment>.Failure("금액은 0보다 커야 합니다.");

        return Result<Payment>.Success(new Payment(
            command.PaymentId.Trim(), command.CustomerId.Trim(), command.Amount, command.PaidAt));
    }
}

// Strategy: 중복 판정 규칙을 인터페이스 뒤에 두면 카드사·국가별 정책을 기존 서비스 수정 없이 교체할 수 있습니다.
public interface IDuplicateRule
{
    Payment? FindDuplicate(Payment candidate, IReadOnlyCollection<Payment> recentPayments);
}

public sealed class SameCustomerAmountRule(TimeSpan window) : IDuplicateRule
{
    public Payment? FindDuplicate(Payment candidate, IReadOnlyCollection<Payment> recentPayments)
    {
        // LINQ는 '같은 고객·금액이며 시간 창 안인 첫 결제'라는 검색 의도를 반복문보다 직접 표현합니다.
        return recentPayments
            .Where(payment => payment.CustomerId == candidate.CustomerId)
            .Where(payment => payment.Amount == candidate.Amount)
            .Where(payment => Math.Abs((candidate.PaidAt - payment.PaidAt).TotalMinutes) <= window.TotalMinutes)
            .OrderByDescending(payment => payment.PaidAt)
            .FirstOrDefault();
    }
}

// Repository는 저장 기술을 업무 규칙에서 분리하며, 인터페이스 덕분에 테스트에서 메모리 구현을 쓸 수 있습니다.
public interface IPaymentRepository
{
    Task<IReadOnlyCollection<Payment>> GetRecentAsync(DateTimeOffset since, CancellationToken cancellationToken);
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
}

public sealed class InMemoryPaymentRepository(IEnumerable<Payment> seed) : IPaymentRepository
{
    private readonly List<Payment> _payments = [.. seed];

    public Task<IReadOnlyCollection<Payment>> GetRecentAsync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyCollection<Payment> result = _payments.Where(payment => payment.PaidAt >= since).ToArray();
        return Task.FromResult(result);
    }

    public Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _payments.Add(payment);
        return Task.CompletedTask;
    }
}

public interface IAuditLog
{
    void ReviewCompleted(PaymentReview review);
}

public sealed class ConsoleAuditLog : IAuditLog
{
    public void ReviewCompleted(PaymentReview review) =>
        Console.WriteLine($"감사 로그: payment={review.PaymentId}, status={review.Status}");
}

// Application Service는 검증→조회→판정→저장 순서를 조정하고, 세부 규칙은 각 협력 객체에 위임합니다.
public sealed class PaymentReviewService(
    IPaymentRepository repository,
    IDuplicateRule duplicateRule,
    IAuditLog auditLog)
{
    public async Task<Result<PaymentReview>> ReviewAsync(
        ReviewPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var creation = PaymentFactory.Create(command);
        if (!creation.IsSuccess)
            return Result<PaymentReview>.Failure(creation.Error!); // 실패 분기라 Error가 null이 아님을 컴파일러에 알립니다.

        var payment = creation.Value!; // 성공 분기라 Value가 존재하며, 앞의 검사로 nullable 안전성을 보장합니다.

        try
        {
            var recent = await repository.GetRecentAsync(payment.PaidAt.AddHours(-1), cancellationToken);
            var duplicate = duplicateRule.FindDuplicate(payment, recent);
            var review = duplicate is null
                ? new PaymentReview(payment.Id, ReviewStatus.Approved, "중복 후보 없음")
                : new PaymentReview(payment.Id, ReviewStatus.ManualReview, $"유사 결제 {duplicate.Id} 확인 필요");

            await repository.AddAsync(payment, cancellationToken);
            auditLog.ReviewCompleted(review);
            return Result<PaymentReview>.Success(review);
        }
        catch (OperationCanceledException)
        {
            throw; // 취소는 정상적인 제어 신호이므로 일반 장애로 감싸지 않고 호출자에게 전달합니다.
        }
        catch (Exception exception)
        {
            // DB·네트워크 장애 같은 예상 밖 실패는 예외로 다루며, 실무에서는 구조화 로그 후 상위 계층에서 변환합니다.
            throw new InvalidOperationException("결제 검토 중 저장소 오류가 발생했습니다.", exception);
        }
    }
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        passed += Check(!PaymentFactory.Create(new("", "c", 1, DateTimeOffset.UtcNow, null)).IsSuccess,
            "빈 결제 ID 거부");
        passed += Check(!PaymentFactory.Create(new("p", "c", 0, DateTimeOffset.UtcNow, null)).IsSuccess,
            "0원 결제 거부");

        var repository = new InMemoryPaymentRepository(
            [new Payment("old", "c", 10_000m, DateTimeOffset.Parse("2026-08-02T00:00:00Z"))]);
        var service = new PaymentReviewService(repository, new SameCustomerAmountRule(TimeSpan.FromMinutes(10)), new SilentAuditLog());
        var duplicate = await service.ReviewAsync(
            new("new", "c", 10_000m, DateTimeOffset.Parse("2026-08-02T00:05:00Z"), null), CancellationToken.None);
        passed += Check(duplicate.Value?.Status == ReviewStatus.ManualReview, "10분 이내 같은 금액 탐지");

        var normal = await service.ReviewAsync(
            new("other", "c", 20_000m, DateTimeOffset.Parse("2026-08-02T00:06:00Z"), null), CancellationToken.None);
        passed += Check(normal.Value?.Status == ReviewStatus.Approved, "다른 금액 승인");

        Console.WriteLine($"self-test: {passed}/4 통과");
        if (passed != 4) Environment.ExitCode = 1;
    }

    private static int Check(bool condition, string name)
    {
        Console.WriteLine($"{(condition ? "PASS" : "FAIL")}: {name}");
        return condition ? 1 : 0;
    }

    // 테스트에서는 출력 부작용을 제거해 판정 결과만 검증합니다.
    private sealed class SilentAuditLog : IAuditLog
    {
        public void ReviewCompleted(PaymentReview review) { }
    }
}
