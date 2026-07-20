using System.Diagnostics.CodeAnalysis;
using System.Text;

// 학습 순서: ① BasicSyntaxTour(문법) → ② Demo(사용법) → ③ Service(흐름)
// → ④ Domain/Rules(업무 규칙) → ⑤ Infrastructure(기술 구현) → ⑥ SelfTest입니다.
// [고급 관점] 규칙을 작은 객체로 분리한 Pipeline과 의존성 주입이 핵심입니다.

Console.OutputEncoding = Encoding.UTF8;

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTest.RunAsync();
    return;
}

BasicSyntaxTour.Run();
await ExpenseApprovalDemo.RunAsync();

public static class BasicSyntaxTour
{
    public static void Run()
    {
        Console.WriteLine("== 1. 기본 문법 둘러보기 ==");

        string employee = "민수";
        decimal amount = 85_000m;
        bool hasReceipt = true;
        string? optionalMemo = null;

        string amountLevel = amount switch
        {
            <= 0 => "잘못된 금액",
            <= 100_000 => "소액",
            <= 1_000_000 => "일반",
            _ => "고액"
        };

        string[] requiredFields = ["신청자", "금액", "분류"];
        List<decimal> recentAmounts = [32_000m, 85_000m, 14_500m];
        Dictionary<ExpenseCategory, decimal> limits = new()
        {
            [ExpenseCategory.Meal] = 100_000m,
            [ExpenseCategory.Transport] = 300_000m,
            [ExpenseCategory.Equipment] = 2_000_000m
        };

        decimal total = 0m;
        foreach (decimal recentAmount in recentAmounts)
        {
            total += recentAmount;
        }

        string receiptMessage = hasReceipt ? "영수증 있음" : "영수증 없음";
        Console.WriteLine($"{employee}: {amount:N0}원 ({amountLevel}, {receiptMessage})");
        Console.WriteLine($"최근 합계: {total:N0}원, 식비 한도: {limits[ExpenseCategory.Meal]:N0}원");
        Console.WriteLine($"필수 항목: {string.Join(", ", requiredFields)}");
        Console.WriteLine($"메모 길이: {optionalMemo?.Length ?? 0}");
        Console.WriteLine();
    }
}

public static class ExpenseApprovalDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("== 2. 비용 승인 파이프라인 ==");

        ExpenseApp app = CompositionRoot.Build();
        SubmitExpenseCommand command = new(
            EmployeeId: "E-101",
            Category: ExpenseCategory.Equipment,
            Amount: 650_000m,
            Description: "개발용 모니터 구매",
            HasReceipt: true);

        Result<ExpenseSummary> result = await app.Service.SubmitAsync(command);

        if (result.IsSuccess)
        {
            ExpenseSummary summary = result.Value;
            Console.WriteLine($"신청 번호: {summary.Id}");
            Console.WriteLine($"상태: {summary.Status}");
            Console.WriteLine($"승인 경로: {summary.ApprovalRoute}");
            Console.WriteLine($"신청 시각: {summary.SubmittedAt:yyyy-MM-dd HH:mm} UTC");
        }
        else
        {
            Console.WriteLine($"신청 실패: {result.Error}");
        }

        Console.WriteLine("--self-test 옵션으로 초보자 검증을 실행할 수 있습니다.");
    }
}

// Composition Root: 객체 생성과 연결을 한곳에서 담당합니다.
public static class CompositionRoot
{
    public static ExpenseApp Build()
    {
        IExpenseRepository repository = new InMemoryExpenseRepository();
        IClock clock = new FixedClock(DateTimeOffset.Parse("2026-07-16T00:00:00+00:00"));

        IExpenseRule[] rules =
        [
            new RequiredFieldsRule(),
            new PositiveAmountRule(),
            new ReceiptRule(),
            new CategoryLimitRule(new Dictionary<ExpenseCategory, decimal>
            {
                [ExpenseCategory.Meal] = 100_000m,
                [ExpenseCategory.Transport] = 300_000m,
                [ExpenseCategory.Equipment] = 2_000_000m
            })
        ];

        ExpenseService service = new(rules, new ApprovalRouter(), repository, clock);
        return new ExpenseApp(service, repository);
    }
}

public sealed record ExpenseApp(ExpenseService Service, IExpenseRepository Repository);

// Application: 규칙 실행 순서와 저장 흐름을 조정합니다.
public sealed class ExpenseService(
    IEnumerable<IExpenseRule> rules,
    IApprovalRouter router,
    IExpenseRepository repository,
    IClock clock)
{
    // [실무] IEnumerable<IExpenseRule>을 순회하므로 규칙을 추가해도 조건문이 비대해지지 않습니다.
    // 변경에는 닫고 확장에는 연다는 개방-폐쇄 원칙의 작은 적용입니다.
    public async ValueTask<Result<ExpenseSummary>> SubmitAsync(
        SubmitExpenseCommand command,
        CancellationToken cancellationToken = default)
    {
        foreach (IExpenseRule rule in rules)
        {
            string? error = rule.Validate(command);
            if (error is not null)
            {
                return Result<ExpenseSummary>.Fail(error);
            }
        }

        ApprovalRoute route = router.Decide(command.Amount);
        ExpenseReport report = ExpenseReport.Submit(command, route, clock.UtcNow);
        await repository.SaveAsync(report, cancellationToken);

        return Result<ExpenseSummary>.Ok(new ExpenseSummary(
            report.Id,
            report.Status,
            report.Route,
            report.SubmittedAt));
    }
}

// Domain: 비용 신청이 반드시 지켜야 할 상태를 표현합니다.
public sealed class ExpenseReport
{
    // [도메인] private set으로 상태 변경 통로를 메서드에 제한해 불변식을 보호합니다.
    private ExpenseReport(
        string employeeId,
        ExpenseCategory category,
        decimal amount,
        string description,
        ApprovalRoute route,
        DateTimeOffset submittedAt)
    {
        EmployeeId = employeeId;
        Category = category;
        Amount = amount;
        Description = description;
        Route = route;
        SubmittedAt = submittedAt;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public string EmployeeId { get; }
    public ExpenseCategory Category { get; }
    public decimal Amount { get; }
    public string Description { get; }
    public ApprovalRoute Route { get; }
    public DateTimeOffset SubmittedAt { get; }
    public ExpenseStatus Status { get; private set; } = ExpenseStatus.Submitted;

    public static ExpenseReport Submit(
        SubmitExpenseCommand command,
        ApprovalRoute route,
        DateTimeOffset submittedAt) => new(
            command.EmployeeId.Trim(),
            command.Category,
            command.Amount,
            command.Description.Trim(),
            route,
            submittedAt);
}

// Pipeline rules: 규칙 하나가 책임 하나만 갖도록 분리합니다.
public interface IExpenseRule
{
    string? Validate(SubmitExpenseCommand command);
}

public sealed class RequiredFieldsRule : IExpenseRule
{
    public string? Validate(SubmitExpenseCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.EmployeeId))
        {
            return "신청자 사번은 필수입니다.";
        }

        return string.IsNullOrWhiteSpace(command.Description)
            ? "사용 목적은 필수입니다."
            : null;
    }
}

public sealed class PositiveAmountRule : IExpenseRule
{
    public string? Validate(SubmitExpenseCommand command) => command.Amount <= 0
        ? "금액은 0보다 커야 합니다."
        : null;
}

public sealed class ReceiptRule : IExpenseRule
{
    public string? Validate(SubmitExpenseCommand command) =>
        command.Amount >= 50_000m && !command.HasReceipt
            ? "5만원 이상 비용에는 영수증이 필요합니다."
            : null;
}

public sealed class CategoryLimitRule(IReadOnlyDictionary<ExpenseCategory, decimal> limits) : IExpenseRule
{
    public string? Validate(SubmitExpenseCommand command)
    {
        decimal limit = limits[command.Category];
        return command.Amount > limit
            ? $"{command.Category} 한도 {limit:N0}원을 초과했습니다."
            : null;
    }
}

public interface IApprovalRouter
{
    ApprovalRoute Decide(decimal amount);
}

public sealed class ApprovalRouter : IApprovalRouter
{
    public ApprovalRoute Decide(decimal amount) => amount switch
    {
        <= 100_000m => ApprovalRoute.AutoApproved,
        <= 1_000_000m => ApprovalRoute.TeamLead,
        _ => ApprovalRoute.DepartmentHead
    };
}

public sealed record SubmitExpenseCommand(
    string EmployeeId,
    ExpenseCategory Category,
    decimal Amount,
    string Description,
    bool HasReceipt);

public sealed record ExpenseSummary(
    Guid Id,
    ExpenseStatus Status,
    ApprovalRoute ApprovalRoute,
    DateTimeOffset SubmittedAt);

public sealed record Result<T>(T? Value, string? Error)
{
    // [고급] 예상 가능한 검증 실패를 예외가 아닌 명시적인 성공/실패 값으로 표현합니다.
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);
    public static Result<T> Fail(string error) => new(default, error);
}

public enum ExpenseCategory { Meal, Transport, Equipment }
public enum ExpenseStatus { Submitted, Approved, Rejected }
public enum ApprovalRoute { AutoApproved, TeamLead, DepartmentHead }

// Infrastructure: 메모리 저장소와 시간을 교체 가능한 구현으로 둡니다.
public interface IExpenseRepository
{
    ValueTask SaveAsync(ExpenseReport report, CancellationToken cancellationToken);
    ValueTask<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class InMemoryExpenseRepository : IExpenseRepository
{
    private readonly Dictionary<Guid, ExpenseReport> _reports = [];

    public ValueTask SaveAsync(ExpenseReport report, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _reports[report.Id] = report;
        return ValueTask.CompletedTask;
    }

    public ValueTask<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _reports.TryGetValue(id, out ExpenseReport? report);
        return ValueTask.FromResult(report);
    }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; } = utcNow;
}

public static class SelfTest
{
    // [검증] 정상값뿐 아니라 경계값과 실패 후 저장 상태까지 확인해야 부작용을 잡을 수 있습니다.
    public static async Task RunAsync()
    {
        ExpenseApp app = CompositionRoot.Build();

        Result<ExpenseSummary> small = await SubmitAsync(app, 45_000m, true, ExpenseCategory.Meal);
        Assert(small.IsSuccess, "정상 소액 신청은 성공해야 합니다.");
        Assert(small.Value.ApprovalRoute == ApprovalRoute.AutoApproved,
            "10만원 이하 신청은 자동 승인 경로여야 합니다.");
        Assert(await app.Repository.FindAsync(small.Value.Id, CancellationToken.None) is not null,
            "성공한 신청은 저장소에 있어야 합니다.");

        Result<ExpenseSummary> noReceipt = await SubmitAsync(app, 80_000m, false, ExpenseCategory.Meal);
        Assert(!noReceipt.IsSuccess, "5만원 이상인데 영수증이 없으면 실패해야 합니다.");
        Assert(noReceipt.Error.Contains("영수증", StringComparison.Ordinal),
            "실패 메시지가 영수증 문제를 설명해야 합니다.");

        Result<ExpenseSummary> overLimit = await SubmitAsync(app, 150_000m, true, ExpenseCategory.Meal);
        Assert(!overLimit.IsSuccess, "분류별 한도를 넘으면 실패해야 합니다.");

        Result<ExpenseSummary> manager = await SubmitAsync(app, 650_000m, true, ExpenseCategory.Equipment);
        Assert(manager.IsSuccess, "한도 안의 장비 신청은 성공해야 합니다.");
        Assert(manager.Value.ApprovalRoute == ApprovalRoute.TeamLead,
            "65만원 신청은 팀장 승인 경로여야 합니다.");

        Console.WriteLine("초보자 검증 통과: 입력 검사, 규칙 순서, 승인 경로, 저장을 모두 확인했습니다.");
    }

    private static ValueTask<Result<ExpenseSummary>> SubmitAsync(
        ExpenseApp app,
        decimal amount,
        bool hasReceipt,
        ExpenseCategory category) => app.Service.SubmitAsync(new SubmitExpenseCommand(
            "E-TEST",
            category,
            amount,
            "업무 수행에 필요한 비용",
            hasReceipt));

    private static void Assert([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"검증 실패: {message}");
        }
    }
}
