// 오늘의 읽기 순서: 실행부 → 기본 타입/record/Result → Domain Model → Strategy/Repository → Application Service 순서입니다.
// 한 파일에 모아 두어 초보자가 파일을 오가며 길을 잃지 않고, 역할이 나뉜 실제 서비스 구조를 따라갈 수 있게 합니다.

var repository = new InMemoryExpenseRepository(
[
    new Expense("EXP-100", "팀 점심", 45_000m, ExpenseStatus.Submitted),
    new Expense("EXP-101", "노트북", 2_100_000m, ExpenseStatus.Submitted),
    new Expense("EXP-103", "택시", 18_000m, ExpenseStatus.Submitted),
    new Expense("EXP-102", "완료된 요청", 10_000m, ExpenseStatus.Approved)
]);

IApprovalPolicy policy = new AmountBasedApprovalPolicy(autoApprovalLimit: 100_000m);
var service = new ExpenseApprovalApplicationService(repository, policy, new ConsoleAuditLog());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTest.RunAsync();
    return;
}

var commands = new[]
{
    new ApproveExpenseCommand("EXP-100", "mina"),
    new ApproveExpenseCommand("EXP-101", "mina"),
    new ApproveExpenseCommand("EXP-404", "mina")
};

foreach (var command in commands)
{
    var result = await service.ApproveAsync(command, CancellationToken.None);
    Console.WriteLine(result.IsSuccess
        ? $"성공: {result.Value!.ExpenseId} / {result.Value.Decision}"
        : $"실패: {result.Error}");
}

// LINQ는 목록을 '무엇을 원하는지' 순서대로 표현합니다. Where(거르기), OrderBy(정렬), Select(모양 바꾸기)를 차례로 읽어 보세요.
var pendingIds = (await repository.GetAllAsync(CancellationToken.None))
    .Where(expense => expense.Status == ExpenseStatus.Submitted)
    .OrderBy(expense => expense.Amount)
    .Select(expense => expense.Id);
Console.WriteLine($"아직 제출 상태인 요청: {string.Join(", ", pendingIds)}");

// enum은 상태를 정해진 값으로 제한해 오타가 있는 문자열 상태가 시스템 전체로 퍼지는 일을 막습니다.
enum ExpenseStatus { Submitted, Approved, NeedsManagerApproval }

// record는 명령과 결과처럼 '값 자체'가 중요한 데이터를 간결하게 표현합니다. 생성 뒤 바뀌지 않아 전달 중 실수를 줄입니다.
sealed record ApproveExpenseCommand(string ExpenseId, string Approver);
sealed record ApprovalReceipt(string ExpenseId, string Decision, DateTimeOffset DecidedAtUtc);

// Result는 '찾지 못함', '규칙 위반'처럼 호출자가 예상하고 안내할 수 있는 실패를 값으로 돌려줍니다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Domain Model은 비용 요청의 상태 전이 규칙을 자기 안에 둡니다. 서비스가 상태를 직접 바꾸지 않아 규칙의 주인이 분명해집니다.
sealed class Expense
{
    public string Id { get; }
    public string Description { get; }
    public decimal Amount { get; }
    public ExpenseStatus Status { get; private set; }

    public Expense(string id, string description, decimal amount, ExpenseStatus status)
    {
        // 생성 시 검증하면 잘못된 금액이나 빈 ID가 저장소와 다른 기능으로 흘러가는 것을 초기에 막습니다.
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        Id = id;
        Description = description;
        Amount = amount;
        Status = status;
    }

    public Result<ExpenseStatus> Decide(ExpenseStatus nextStatus)
    {
        if (Status != ExpenseStatus.Submitted)
            return Result<ExpenseStatus>.Failure("제출 상태의 요청만 결정할 수 있습니다.");

        Status = nextStatus;
        return Result<ExpenseStatus>.Success(Status);
    }
}

// Strategy는 금액별 승인 규칙을 교체 가능한 인터페이스로 분리합니다. 정책이 바뀌어도 Application Service는 수정하지 않아 OCP에 가깝습니다.
interface IApprovalPolicy
{
    ExpenseStatus DecideFor(Expense expense);
}

sealed class AmountBasedApprovalPolicy(decimal autoApprovalLimit) : IApprovalPolicy
{
    public ExpenseStatus DecideFor(Expense expense) =>
        expense.Amount <= autoApprovalLimit ? ExpenseStatus.Approved : ExpenseStatus.NeedsManagerApproval;
}

// Repository는 데이터가 메모리, SQL, HTTP 어디에 있는지 감춥니다. 서비스는 인터페이스만 알아 테스트에서 가짜 구현으로 쉽게 바꿉니다(DIP).
interface IExpenseRepository
{
    Task<Expense?> FindByIdAsync(string id, CancellationToken cancellationToken);
    Task SaveAsync(Expense expense, CancellationToken cancellationToken);
    Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken cancellationToken);
}

sealed class InMemoryExpenseRepository(IEnumerable<Expense> seed) : IExpenseRepository
{
    private readonly Dictionary<string, Expense> _items =
        seed.ToDictionary(expense => expense.Id, StringComparer.OrdinalIgnoreCase);

    public Task<Expense?> FindByIdAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.TryGetValue(id, out var expense);
        // Expense?는 '없음'이 정상 조회 결과일 수 있음을 타입으로 알려 줍니다. ! 연산자로 억지로 숨기지 않습니다.
        return Task.FromResult(expense);
    }

    public Task SaveAsync(Expense expense, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items[expense.Id] = expense;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Expense>>(_items.Values.ToArray());
    }
}

// 운영 로그는 업무 로직과 분리합니다. 실제 환경에서는 구조화 로그, correlation ID, 권한 있는 감사 저장소로 구현을 교체합니다.
interface IAuditLog
{
    void DecisionMade(ApprovalReceipt receipt, string approver);
}

sealed class ConsoleAuditLog : IAuditLog
{
    public void DecisionMade(ApprovalReceipt receipt, string approver) =>
        Console.WriteLine($"감사 로그: expense={receipt.ExpenseId}, approver={approver}, utc={receipt.DecidedAtUtc:O}");
}

// Application Service는 조회 → 규칙 적용 → 저장 → 기록이라는 유스케이스 순서만 조정합니다. 이것이 SRP와 테스트 가능성의 핵심입니다.
sealed class ExpenseApprovalApplicationService(
    IExpenseRepository repository,
    IApprovalPolicy policy,
    IAuditLog auditLog)
{
    public async Task<Result<ApprovalReceipt>> ApproveAsync(
        ApproveExpenseCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ExpenseId) || string.IsNullOrWhiteSpace(command.Approver))
            return Result<ApprovalReceipt>.Failure("비용 요청 ID와 승인자는 필수입니다.");

        var expense = await repository.FindByIdAsync(command.ExpenseId, cancellationToken);
        if (expense is null)
            return Result<ApprovalReceipt>.Failure("비용 요청을 찾을 수 없습니다.");

        var decision = expense.Decide(policy.DecideFor(expense));
        if (!decision.IsSuccess)
            return Result<ApprovalReceipt>.Failure(decision.Error!);

        // await는 I/O가 끝날 때까지 스레드를 붙잡지 않습니다. 취소는 예외로 전파해 호출자가 작업 중단을 명확히 처리하게 합니다.
        await repository.SaveAsync(expense, cancellationToken);
        var receipt = new ApprovalReceipt(expense.Id, decision.Value!.ToString(), DateTimeOffset.UtcNow);
        auditLog.DecisionMade(receipt, command.Approver);
        return Result<ApprovalReceipt>.Success(receipt);
    }
}

static class SelfTest
{
    public static async Task RunAsync()
    {
        var repository = new InMemoryExpenseRepository(
        [
            new("AUTO", "식비", 30_000m, ExpenseStatus.Submitted),
            new("MANAGER", "장비", 300_000m, ExpenseStatus.Submitted),
            new("DONE", "이미 처리됨", 1_000m, ExpenseStatus.Approved)
        ]);
        var service = new ExpenseApprovalApplicationService(
            repository, new AmountBasedApprovalPolicy(100_000m), new ConsoleAuditLog());

        var cases = new[]
        {
            ("자동 승인", await service.ApproveAsync(new("AUTO", "lee"), default), true),
            ("관리자 검토", await service.ApproveAsync(new("MANAGER", "lee"), default), true),
            ("없는 요청", await service.ApproveAsync(new("MISSING", "lee"), default), false),
            ("이미 처리됨", await service.ApproveAsync(new("DONE", "lee"), default), false)
        };

        foreach (var (name, result, expectedSuccess) in cases)
        {
            if (result.IsSuccess != expectedSuccess)
                throw new InvalidOperationException($"{name} 검증 실패");
            Console.WriteLine($"PASS: {name}");
        }
    }
}
