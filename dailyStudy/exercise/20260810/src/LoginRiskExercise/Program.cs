// 오늘 예제는 로그인 기록을 읽어 위험도를 계산합니다. 작은 콘솔 앱이지만 실무의 계층 분리와 테스트 가능한 조립법을 함께 보여 줍니다.
var repository = new InMemoryLoginRepository([
    new LoginAttempt("L-101", "alice", "KR", false, 1),
    new LoginAttempt("L-102", "bob", "US", true, 4),
    new LoginAttempt("L-103", "carol", null, false, 0)
]);

// Composition Root는 구현 객체를 한곳에서 조립합니다. 서비스가 new로 의존성을 만들지 않아 테스트용 구현으로 쉽게 교체할 수 있습니다.
IRiskStrategy strategy = new DefaultRiskStrategy();
var service = new ReviewLoginService(repository, strategy);
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase)) { await SelfTests.RunAsync(); return; }

var results = await service.ExecuteAsync(CancellationToken.None);
foreach (var result in results)
    Console.WriteLine($"{result.LoginId}: {result.Level} ({result.Score}점) - {result.Reason}");

// record는 값 중심 데이터에 값 동등성과 간결한 생성 문법을 제공합니다. CountryCode의 ?는 값이 없을 수 있음을 컴파일러에도 알립니다.
public sealed record LoginAttempt(string Id, string UserId, string? CountryCode, bool IsNewDevice, int FailedAttempts);
public sealed record RiskAssessment(string LoginId, RiskLevel Level, int Score, string Reason);
public enum RiskLevel { Low, Medium, High }

// 예상 가능한 업무 실패는 Result로 표현하면 호출자가 성공과 실패를 빠뜨리지 않고 처리하기 쉽습니다.
public sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

public interface ILoginRepository { Task<IReadOnlyList<LoginAttempt>> GetRecentAsync(CancellationToken cancellationToken); }
public interface IRiskStrategy { Result<RiskAssessment> Assess(LoginAttempt attempt); }

// Strategy는 변하기 쉬운 점수 규칙을 분리합니다. 새 정책을 추가해도 Application Service를 수정하지 않아 OCP와 DIP에 유리합니다.
public sealed class DefaultRiskStrategy : IRiskStrategy
{
    public Result<RiskAssessment> Assess(LoginAttempt attempt)
    {
        if (string.IsNullOrWhiteSpace(attempt.UserId) || attempt.FailedAttempts < 0)
            return Result<RiskAssessment>.Failure("사용자와 실패 횟수를 확인하세요.");

        var score = (attempt.IsNewDevice ? 40 : 0) + Math.Min(attempt.FailedAttempts * 15, 60);
        var level = score >= 70 ? RiskLevel.High : score >= 30 ? RiskLevel.Medium : RiskLevel.Low;
        var country = attempt.CountryCode ?? "미확인"; // null 병합 연산자는 안전한 기본값을 선택해 NullReferenceException을 피합니다.
        return Result<RiskAssessment>.Success(new(attempt.Id, level, score, $"국가 {country}, 실패 {attempt.FailedAttempts}회"));
    }
}

// Application Service는 저장소 조회와 도메인 정책 실행 순서를 조정하고, 세부 구현은 인터페이스 뒤에 둡니다.
public sealed class ReviewLoginService(ILoginRepository repository, IRiskStrategy strategy)
{
    public async Task<IReadOnlyList<RiskAssessment>> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var attempts = await repository.GetRecentAsync(cancellationToken);
            // LINQ는 '평가 성공 결과만 위험도순으로 선택'한다는 의도를 반복문보다 짧고 선언적으로 드러냅니다.
            return attempts.Select(strategy.Assess)
                .Where(x => x.IsSuccess)
                .Select(x => x.Value!)
                .OrderByDescending(x => x.Score)
                .ToArray();
        }
        catch (OperationCanceledException) { throw; } // 취소는 오류로 숨기지 않고 호출자에게 전달해야 종료와 타임아웃 제어가 가능합니다.
        catch (Exception ex)
        {
            // 저장소 장애 같은 예외는 경계에서 기록해야 합니다. 학습용이라 콘솔을 쓰지만 운영에서는 구조화 로그와 추적 ID를 사용합니다.
            Console.Error.WriteLine($"로그인 검토 실패: {ex.Message}");
            throw;
        }
    }
}

public sealed class InMemoryLoginRepository(IEnumerable<LoginAttempt> seed) : ILoginRepository
{
    private readonly IReadOnlyList<LoginAttempt> _items = seed.ToArray(); // 복사본을 보관해 외부 컬렉션 변경이 내부 상태를 흔들지 않게 합니다.
    public Task<IReadOnlyList<LoginAttempt>> GetRecentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_items);
    }
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var policy = new DefaultRiskStrategy();
        Check(policy.Assess(new("A", "u", null, false, 0)).Value?.Level == RiskLevel.Low, "낮은 위험", ref passed);
        Check(policy.Assess(new("B", "u", "KR", true, 4)).Value?.Level == RiskLevel.High, "높은 위험", ref passed);
        Check(!policy.Assess(new("C", "", "KR", false, -1)).IsSuccess, "입력 검증", ref passed);
        var service = new ReviewLoginService(new InMemoryLoginRepository([new("D", "u", "KR", true, 2)]), policy);
        Check((await service.ExecuteAsync(CancellationToken.None)).Count == 1, "서비스 조립", ref passed);
        Console.WriteLine($"self-test: {passed}/4 통과");
    }

    private static void Check(bool condition, string name, ref int passed)
    {
        // 실패를 즉시 예외로 만들어 자동 검증이 잘못된 결과를 조용히 통과시키지 않게 합니다.
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
        passed++;
    }
}
