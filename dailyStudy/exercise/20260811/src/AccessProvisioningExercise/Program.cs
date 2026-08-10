// 오늘 예제는 한 파일에서 위에서 아래로 읽으며 문법과 설계의 연결을 확인하도록 구성했습니다.
// 실제 서비스에서는 Domain, Application, Infrastructure 프로젝트로 나눌 수 있지만, 처음에는 실행 흐름을 한눈에 보는 편이 좋습니다.

var selfTest = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);

// Composition Root: 구체 구현을 조립하는 곳을 한 군데로 모으면 업무 코드는 생성 방법을 몰라도 되어 교체와 테스트가 쉬워집니다.
IEmployeeRepository repository = new InMemoryEmployeeRepository(
[
    // 인터페이스 형식만으로는 생성할 컬렉션을 정할 수 없어 구체 형식 HashSet을 명시합니다.
    new Employee("E-101", "민지", Department.Engineering, new HashSet<string> { "basic" }),
    new Employee("E-102", "준호", Department.Finance, new HashSet<string> { "basic", "finance-reader" }),
    new Employee("E-103", "서연", Department.Engineering, new HashSet<string> { "basic", "developer" })
]);
IAccessPolicy policy = new DepartmentAccessPolicy();
IAccessGateway gateway = new ConsoleAccessGateway();
var service = new ProvisionAccessService(repository, policy, gateway);

if (selfTest)
{
    await BeginnerValidation.RunAsync(service);
    return;
}

Console.WriteLine("=== 직원 접근 권한 프로비저닝 ===");
foreach (var employeeId in new[] { "E-101", "E-102", "E-404" })
{
    var result = await service.ProvisionAsync(employeeId, CancellationToken.None);
    Console.WriteLine(result.IsSuccess
        ? $"{employeeId}: 추가 권한 [{string.Join(", ", result.Value!)}]"
        : $"{employeeId}: 실패 - {result.Error}");
}

// record는 값 중심 데이터를 간결하게 표현합니다. init 전용 속성 효과로 생성 뒤 상태가 바뀌지 않아 추론과 동시성 처리가 쉬워집니다.
public sealed record Employee(string Id, string Name, Department Department, IReadOnlySet<string> CurrentRoles);

// enum은 부서처럼 가능한 값이 정해진 대상을 문자열 오타 없이 표현합니다.
public enum Department { Engineering, Finance, HumanResources }

// Result는 "직원이 없음"처럼 예상 가능한 실패를 반환값으로 드러냅니다. Value의 ?는 실패할 때 null일 수 있다는 nullable 계약입니다.
public sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Repository 인터페이스는 저장 방식(DB, API, 메모리)을 Application Service에서 숨깁니다. 이는 DIP와 테스트 가능성을 높입니다.
public interface IEmployeeRepository
{
    Task<Employee?> FindAsync(string id, CancellationToken cancellationToken);
}

// Strategy 인터페이스는 부서별 권한 정책이라는 변하는 규칙을 격리합니다. 새 정책을 추가해도 서비스 흐름을 고치지 않는 OCP를 돕습니다.
public interface IAccessPolicy
{
    IReadOnlySet<string> RequiredRolesFor(Employee employee);
}

// 외부 IAM 호출도 인터페이스 뒤에 두면 테스트에서 느리거나 위험한 실제 시스템 대신 가짜 구현을 사용할 수 있습니다.
public interface IAccessGateway
{
    Task GrantAsync(string employeeId, IReadOnlySet<string> roles, CancellationToken cancellationToken);
}

// Application Service는 조회 → 정책 계산 → 외부 반영이라는 유스케이스 순서만 책임집니다(SRP).
public sealed class ProvisionAccessService(
    IEmployeeRepository repository,
    IAccessPolicy policy,
    IAccessGateway gateway)
{
    public async Task<Result<IReadOnlySet<string>>> ProvisionAsync(string employeeId, CancellationToken cancellationToken)
    {
        // 공백 ID는 사용자가 고칠 수 있는 입력 실패이므로 예외가 아닌 Result로 돌려줍니다.
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<IReadOnlySet<string>>.Failure("직원 ID가 비어 있습니다.");

        var employee = await repository.FindAsync(employeeId, cancellationToken);
        if (employee is null)
            return Result<IReadOnlySet<string>>.Failure("직원을 찾을 수 없습니다.");

        var required = policy.RequiredRolesFor(employee);

        // LINQ의 Where는 이미 가진 권한을 제외합니다. HashSet은 중복을 막아 같은 요청을 재실행해도 결과가 같아지는 멱등성을 돕습니다.
        IReadOnlySet<string> missing = required
            .Where(role => !employee.CurrentRoles.Contains(role))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (missing.Count == 0)
            return Result<IReadOnlySet<string>>.Success(missing);

        // await는 외부 I/O를 기다리는 동안 스레드를 붙잡지 않습니다. CancellationToken은 종료·시간 초과 요청을 아래 계층까지 전달합니다.
        // 네트워크 장애처럼 예상 밖의 인프라 실패는 여기서 숨기지 않고 예외로 올려 재시도/로깅 정책이 처리하게 합니다.
        await gateway.GrantAsync(employee.Id, missing, cancellationToken);
        return Result<IReadOnlySet<string>>.Success(missing);
    }
}

public sealed class DepartmentAccessPolicy : IAccessPolicy
{
    public IReadOnlySet<string> RequiredRolesFor(Employee employee)
    {
        // switch 식은 enum의 각 경우를 값으로 매핑합니다. 공통 권한과 부서 권한을 합친 뒤 집합으로 중복을 제거합니다.
        string[] departmentRoles = employee.Department switch
        {
            Department.Engineering => ["developer", "source-reader"],
            Department.Finance => ["finance-reader", "expense-approver"],
            Department.HumanResources => ["hr-reader"],
            _ => throw new ArgumentOutOfRangeException(nameof(employee.Department))
        };

        return new[] { "basic", "security-training" }
            .Concat(departmentRoles)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class InMemoryEmployeeRepository(IEnumerable<Employee> employees) : IEmployeeRepository
{
    // Dictionary는 키로 빠르게 조회합니다. OrdinalIgnoreCase를 주어 ID 대소문자 차이를 업무상 같은 값으로 취급합니다.
    private readonly IReadOnlyDictionary<string, Employee> _employees =
        employees.ToDictionary(employee => employee.Id, StringComparer.OrdinalIgnoreCase);

    public Task<Employee?> FindAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _employees.TryGetValue(id, out var employee);
        return Task.FromResult(employee);
    }
}

public sealed class ConsoleAccessGateway : IAccessGateway
{
    public Task GrantAsync(string employeeId, IReadOnlySet<string> roles, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"[IAM] {employeeId}에게 {string.Join(", ", roles.Order())} 부여");
        return Task.CompletedTask;
    }
}

public static class BeginnerValidation
{
    public static async Task RunAsync(ProvisionAccessService service)
    {
        var cases = new (string Name, string Id, bool ExpectedSuccess, int ExpectedCount)[]
        {
            ("신규 권한 계산", "E-101", true, 3),
            ("일부 권한 보유", "E-102", true, 2),
            ("없는 직원", "E-404", false, 0),
            ("빈 ID", " ", false, 0)
        };

        var passed = 0;
        foreach (var test in cases)
        {
            var result = await service.ProvisionAsync(test.Id, CancellationToken.None);
            var actualCount = result.Value?.Count ?? 0; // ??는 왼쪽이 null일 때 안전한 기본값 0을 사용합니다.
            var ok = result.IsSuccess == test.ExpectedSuccess && actualCount == test.ExpectedCount;
            Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {test.Name}");
            if (ok) passed++;
        }

        Console.WriteLine($"검증 결과: {passed}/{cases.Length}");
        if (passed != cases.Length)
            throw new InvalidOperationException("초보자 검증 단계가 실패했습니다.");
    }
}
