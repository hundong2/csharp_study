// 초보자 읽기 순서: 실행 코드 → 기본 타입/record → Result → Strategy/Repository → Service → Composition Root → 검증 코드.
// 한 파일에 모은 이유는 처음에는 파일 이동보다 객체가 협력하는 흐름에 집중하기 위해서입니다.

var scheduler = CompositionRoot.CreateScheduler();

if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    await BeginnerValidation.RunAsync();
    return;
}

// [기초] var는 오른쪽 값으로 컴파일러가 타입을 추론합니다. 타입이 사라지는 동적 문법은 아닙니다.
var jobs = new[]
{
    new ScheduledJob(Guid.NewGuid(), " daily-report ", JobPriority.Normal, DateTimeOffset.UtcNow.AddMinutes(5)),
    new ScheduledJob(Guid.NewGuid(), "database-backup", JobPriority.High, DateTimeOffset.UtcNow.AddMinutes(10))
};

// [기초] foreach는 컬렉션을 한 항목씩 순회하고, await는 I/O 작업을 기다리는 동안 스레드를 붙잡지 않습니다.
foreach (var job in jobs)
{
    var result = await scheduler.ScheduleAsync(job, CancellationToken.None);
    Console.WriteLine(result.IsSuccess ? $"예약 성공: {result.Value}" : $"예약 실패: {result.Error}");
}

// enum은 허용된 선택지를 이름으로 제한해 잘못된 문자열 입력을 줄입니다.
enum JobPriority { Normal, High }

// record는 값 중심 데이터에 적합합니다. init 전용 속성과 with 복사를 사용하면 공유 데이터가 뜻밖에 바뀌는 일을 줄입니다.
sealed record ScheduledJob(Guid Id, string Name, JobPriority Priority, DateTimeOffset RunAt)
{
    public Result<ScheduledJob> Validate(DateTimeOffset now)
    {
        var normalizedName = Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
            return Result<ScheduledJob>.Failure("작업 이름은 비워 둘 수 없습니다.");
        if (RunAt <= now)
            return Result<ScheduledJob>.Failure("실행 시각은 현재보다 미래여야 합니다.");

        // 원본을 수정하지 않고 정규화한 새 값을 만들어 호출자가 변경 전후를 안전하게 구분할 수 있습니다.
        return Result<ScheduledJob>.Success(this with { Name = normalizedName });
    }
}

// 예상 가능한 업무 실패는 Result로 돌려 호출자가 성공과 실패를 명시적으로 처리하게 합니다.
sealed record Result<T>(bool IsSuccess, T? Value, string? Error)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// Strategy는 바뀔 가능성이 큰 지연 계산 규칙을 분리합니다. 새 우선순위 정책을 서비스 수정 없이 교체할 수 있습니다(OCP).
interface IDispatchDelayStrategy
{
    TimeSpan GetDelay(JobPriority priority);
}

sealed class PriorityDispatchDelayStrategy : IDispatchDelayStrategy
{
    public TimeSpan GetDelay(JobPriority priority) => priority switch
    {
        JobPriority.High => TimeSpan.Zero,
        JobPriority.Normal => TimeSpan.FromSeconds(30),
        _ => throw new ArgumentOutOfRangeException(nameof(priority))
    };
}

// Repository는 저장 기술이 아니라 도메인이 필요로 하는 저장 동작을 표현합니다. 테스트에서는 메모리 구현으로 교체할 수 있습니다(DIP).
interface IJobRepository
{
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken);
    Task SaveAsync(ScheduledJob job, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScheduledJob>> ListAsync(CancellationToken cancellationToken);
}

sealed class InMemoryJobRepository : IJobRepository
{
    private readonly List<ScheduledJob> _jobs = [];

    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // LINQ Any는 “조건을 만족하는 항목이 하나라도 있는가”라는 의도를 반복문보다 직접 표현합니다.
        return Task.FromResult(_jobs.Any(job => job.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
    }

    public Task SaveAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _jobs.Add(job);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScheduledJob>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // 배열 복사본을 반환해 외부 코드가 Repository 내부 List를 직접 바꾸지 못하게 합니다.
        return Task.FromResult<IReadOnlyList<ScheduledJob>>(_jobs.ToArray());
    }
}

// 현재 시각도 의존성으로 만들면 테스트에서 시간을 고정할 수 있어 경계값 검증이 흔들리지 않습니다.
interface IClock { DateTimeOffset UtcNow { get; } }
sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
sealed class FixedClock(DateTimeOffset utcNow) : IClock { public DateTimeOffset UtcNow { get; } = utcNow; }

// Application Service는 사용 사례의 순서만 조정합니다. 검증은 Domain Model, 저장은 Repository, 정책은 Strategy가 맡습니다(SRP).
sealed class JobScheduler(IJobRepository repository, IDispatchDelayStrategy delayStrategy, IClock clock)
{
    public async Task<Result<ScheduledJob>> ScheduleAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        // 프로그래머 계약 위반(null)은 예외로, 중복이나 과거 시각 같은 예상 가능한 업무 거절은 Result로 구분합니다.
        ArgumentNullException.ThrowIfNull(job);
        var validation = job.Validate(clock.UtcNow);
        if (!validation.IsSuccess || validation.Value is null)
            return Result<ScheduledJob>.Failure(validation.Error ?? "알 수 없는 검증 오류입니다.");

        var validJob = validation.Value;
        if (await repository.ExistsByNameAsync(validJob.Name, cancellationToken))
            return Result<ScheduledJob>.Failure("같은 이름의 작업이 이미 예약되어 있습니다.");

        var delayedJob = validJob with { RunAt = validJob.RunAt + delayStrategy.GetDelay(validJob.Priority) };
        await repository.SaveAsync(delayedJob, cancellationToken);
        return Result<ScheduledJob>.Success(delayedJob);
    }
}

// Composition Root 한 곳에서 구현체를 선택하고 생성자 주입으로 연결하면 업무 코드가 new와 환경 설정에 오염되지 않습니다.
static class CompositionRoot
{
    public static JobScheduler CreateScheduler() =>
        new(new InMemoryJobRepository(), new PriorityDispatchDelayStrategy(), new SystemClock());
}

static class BeginnerValidation
{
    public static async Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);
        var repository = new InMemoryJobRepository();
        var scheduler = new JobScheduler(repository, new PriorityDispatchDelayStrategy(), new FixedClock(now));
        var first = new ScheduledJob(Guid.NewGuid(), " report ", JobPriority.Normal, now.AddMinutes(1));

        var normalized = await scheduler.ScheduleAsync(first, CancellationToken.None);
        var duplicate = await scheduler.ScheduleAsync(first with { Id = Guid.NewGuid() }, CancellationToken.None);
        var past = await scheduler.ScheduleAsync(first with { Id = Guid.NewGuid(), Name = "past", RunAt = now }, CancellationToken.None);
        var saved = await repository.ListAsync(CancellationToken.None);

        (string Name, bool Passed)[] checks =
        {
            ("공백 제거와 저장", normalized.IsSuccess && normalized.Value?.Name == "report" && saved.Count == 1),
            ("Normal 작업 30초 지연", normalized.Value?.RunAt == now.AddMinutes(1).AddSeconds(30)),
            ("중복 이름 거절", !duplicate.IsSuccess),
            ("과거 시각 거절", !past.IsSuccess)
        };

        foreach (var (name, passed) in checks)
            Console.WriteLine($"[{(passed ? "통과" : "실패")}] {name}");

        var passedCount = checks.Count(check => check.Passed);
        Console.WriteLine($"초보자 검증 {(passedCount == checks.Length ? "통과" : "실패")} ({passedCount}/{checks.Length})");
        if (passedCount != checks.Length) Environment.ExitCode = 1;
    }
}
