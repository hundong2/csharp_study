// 오늘 예제는 실패한 배치 작업을 재시도하거나 수동 검토 대상으로 격리합니다.
// 한 파일 안에서도 데이터, 업무 규칙, 작업 순서, 외부 연결을 분리해 실무 구조를 연습합니다.

var repository = new InMemoryJobRepository(
[
    new FailedJob("JOB-101", "invoice-export", 1, 3, FailureKind.Temporary, DateTimeOffset.Parse("2026-08-07T08:00:00+09:00")),
    new FailedJob("JOB-102", "customer-sync", 3, 3, FailureKind.Temporary, DateTimeOffset.Parse("2026-08-07T08:05:00+09:00")),
    new FailedJob("JOB-103", null, 0, 2, FailureKind.Permanent, DateTimeOffset.Parse("2026-08-07T08:10:00+09:00"))
]);

// Composition Root는 시작점 한 곳에서 구체 구현을 조립합니다.
// 서비스가 생성 방법을 몰라 테스트용 저장소와 실행기로 쉽게 교체할 수 있습니다.
IRetryPolicy policy = new ExponentialRetryPolicy(TimeSpan.FromSeconds(10));
var service = new ProcessFailedJobsService(repository, policy, new ConsoleJobDispatcher());

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await SelfTests.RunAsync();
    return;
}

var result = await service.ExecuteAsync(CancellationToken.None);
Console.WriteLine(result.IsSuccess
    ? $"처리 완료: 재시도 {result.Value.RetryCount}건, 격리 {result.Value.QuarantineCount}건"
    : $"처리 실패: {result.Error}");

// record는 값 중심 데이터를 표현하고 생성 뒤 변경을 줄여 흐름 추적을 쉽게 합니다.
// string?은 외부 입력에서 작업 이름이 없을 가능성을 숨기지 않고 검사하도록 만듭니다.
public sealed record FailedJob(
    string Id,
    string? Name,
    int AttemptCount,
    int MaxAttempts,
    FailureKind FailureKind,
    DateTimeOffset FailedAt);

public enum FailureKind { Temporary, Permanent }
public sealed record RetryDecision(bool ShouldRetry, TimeSpan Delay, string Reason);
public sealed record ProcessingSummary(int RetryCount, int QuarantineCount);

// 예상 가능한 검증 실패는 Result로 표현해 호출자가 성공 여부를 빠뜨리지 않게 합니다.
// DB 연결 단절 같은 기술 장애는 예외로 전파한 뒤 Application Service 경계에서 번역합니다.
public sealed record Result<T>(bool IsSuccess, T Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default!, error);
}

public interface IJobRepository
{
    Task<IReadOnlyList<FailedJob>> GetFailedAsync(CancellationToken cancellationToken);
}

public interface IRetryPolicy
{
    Result<RetryDecision> Decide(FailedJob job);
}

public interface IJobDispatcher
{
    Task RetryAsync(FailedJob job, TimeSpan delay, CancellationToken cancellationToken);
    Task QuarantineAsync(FailedJob job, string reason, CancellationToken cancellationToken);
}

// Strategy는 자주 바뀌는 재시도 규칙을 별도 계약 뒤에 둡니다.
// 정책을 추가해도 서비스 흐름을 고치지 않으므로 SOLID의 개방-폐쇄 원칙을 돕습니다.
public sealed class ExponentialRetryPolicy(TimeSpan baseDelay) : IRetryPolicy
{
    public Result<RetryDecision> Decide(FailedJob job)
    {
        if (string.IsNullOrWhiteSpace(job.Name))
            return Result<RetryDecision>.Failure("작업 이름이 필요합니다.");

        if (job.AttemptCount < 0 || job.MaxAttempts <= 0)
            return Result<RetryDecision>.Failure("시도 횟수 설정이 올바르지 않습니다.");

        if (job.FailureKind == FailureKind.Permanent)
            return Result<RetryDecision>.Success(new(false, TimeSpan.Zero, "영구 오류"));

        if (job.AttemptCount >= job.MaxAttempts)
            return Result<RetryDecision>.Success(new(false, TimeSpan.Zero, "최대 시도 횟수 초과"));

        // Math.Pow는 시도할수록 대기 시간을 늘려 장애 시스템에 요청이 몰리는 것을 줄입니다.
        var multiplier = Math.Pow(2, job.AttemptCount);
        return Result<RetryDecision>.Success(new(true, baseDelay * multiplier, "일시 오류"));
    }
}

// Application Service는 조회, 판단, 재시도 또는 격리의 순서만 조정합니다.
// 생성자 주입으로 세부 구현을 숨겨 단일 책임 원칙과 테스트 가능성을 높입니다.
public sealed class ProcessFailedJobsService(
    IJobRepository repository,
    IRetryPolicy policy,
    IJobDispatcher dispatcher)
{
    public async Task<Result<ProcessingSummary>> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            // await는 I/O 대기 중 스레드를 붙잡지 않으며, 토큰은 종료 요청을 아래 계층까지 전달합니다.
            var jobs = await repository.GetFailedAsync(cancellationToken);

            // LINQ로 오래 실패한 작업부터 처리할 새 배열을 만들며 원본 컬렉션은 바꾸지 않습니다.
            var orderedJobs = jobs.OrderBy(job => job.FailedAt).ToArray();
            var retryCount = 0;
            var quarantineCount = 0;

            foreach (var job in orderedJobs)
            {
                var decision = policy.Decide(job);
                if (!decision.IsSuccess)
                {
                    await dispatcher.QuarantineAsync(job, decision.Error!, cancellationToken);
                    quarantineCount++;
                    continue;
                }

                if (decision.Value.ShouldRetry)
                {
                    await dispatcher.RetryAsync(job, decision.Value.Delay, cancellationToken);
                    retryCount++;
                }
                else
                {
                    await dispatcher.QuarantineAsync(job, decision.Value.Reason, cancellationToken);
                    quarantineCount++;
                }
            }

            return Result<ProcessingSummary>.Success(new(retryCount, quarantineCount));
        }
        catch (OperationCanceledException)
        {
            // 취소는 장애가 아니라 상위 호출자의 제어 신호이므로 실패 결과로 감추지 않습니다.
            throw;
        }
        catch (Exception ex)
        {
            // 운영에서는 원본 예외를 구조화 로그에 남기고 외부에는 안전한 메시지만 반환합니다.
            return Result<ProcessingSummary>.Failure($"실패 작업 처리 중 기술 오류: {ex.Message}");
        }
    }
}

public sealed class InMemoryJobRepository(IEnumerable<FailedJob> seed) : IJobRepository
{
    private readonly IReadOnlyList<FailedJob> _jobs = seed.ToArray();

    public Task<IReadOnlyList<FailedJob>> GetFailedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_jobs);
    }
}

public sealed class ConsoleJobDispatcher : IJobDispatcher
{
    public Task RetryAsync(FailedJob job, TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"{job.Id} {job.Name}: {delay.TotalSeconds:0}초 후 재시도");
        return Task.CompletedTask;
    }

    public Task QuarantineAsync(FailedJob job, string reason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine($"{job.Id}: 격리 ({reason})");
        return Task.CompletedTask;
    }
}

public static class SelfTests
{
    public static async Task RunAsync()
    {
        var passed = 0;
        var policy = new ExponentialRetryPolicy(TimeSpan.FromSeconds(10));

        Check(policy.Decide(Job("A", 1, 3, FailureKind.Temporary)).Value.Delay == TimeSpan.FromSeconds(20), "지수 백오프", ref passed);
        Check(!policy.Decide(Job("B", 3, 3, FailureKind.Temporary)).Value.ShouldRetry, "최대 횟수", ref passed);
        Check(!policy.Decide(Job("C", 0, 3, FailureKind.Permanent)).Value.ShouldRetry, "영구 오류", ref passed);

        var dispatcher = new CollectingDispatcher();
        var repository = new InMemoryJobRepository([Job("D", 0, 2, FailureKind.Temporary)]);
        var result = await new ProcessFailedJobsService(repository, policy, dispatcher).ExecuteAsync(CancellationToken.None);
        Check(result.Value.RetryCount == 1 && dispatcher.RetryCount == 1, "서비스 흐름", ref passed);

        Console.WriteLine($"self-test: {passed}/4 통과");
    }

    private static FailedJob Job(string id, int attempts, int maxAttempts, FailureKind kind) =>
        new(id, "test-job", attempts, maxAttempts, kind, DateTimeOffset.UnixEpoch);

    private static void Check(bool condition, string name, ref int passed)
    {
        if (!condition) throw new InvalidOperationException($"테스트 실패: {name}");
        passed++;
    }

    private sealed class CollectingDispatcher : IJobDispatcher
    {
        public int RetryCount { get; private set; }

        public Task RetryAsync(FailedJob job, TimeSpan delay, CancellationToken cancellationToken)
        {
            RetryCount++;
            return Task.CompletedTask;
        }

        public Task QuarantineAsync(FailedJob job, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
