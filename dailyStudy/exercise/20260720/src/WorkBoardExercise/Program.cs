using System.Collections.ObjectModel;

// 학습 순서: ① SyntaxTour(변수·nullable·배열·LINQ·switch) → ② WorkItem 모델
// → ③ Repository 계약/구현 → ④ Application Service → ⑤ Composition Root/검증입니다.
// [고급 관점] 불변 record, 의존성 역전, 읽기 전용 컬렉션, 비동기 계약을 함께 살펴보세요.

if (args.Contains("--self-test"))
{
    BeginnerValidation.Run();
    return;
}

Console.WriteLine("=== 오늘의 작업 보드 ===");
SyntaxTour.Show();

// [기초] var는 오른쪽 식으로 타입을 추론합니다. 타입 자체가 사라지는 것은 아닙니다.
// [비동기] await는 Task 완료를 기다리는 동안 스레드를 불필요하게 점유하지 않습니다.
var service = CompositionRoot.Create();
var first = await service.AddAsync("Nullable 복습", Priority.High);
var second = await service.AddAsync("Repository 패턴 연습", Priority.Normal);
await service.CompleteAsync(first.Id);

foreach (var item in await service.GetBoardAsync())
    Console.WriteLine($"#{item.Id} [{item.Priority}] {item.Title} - {(item.IsDone ? "완료" : "진행 중")}");

Console.WriteLine($"완료율: {ProgressCalculator.Percent(await service.GetBoardAsync())}%");

static class SyntaxTour
{
    public static void Show()
    {
        // [기초] 명시적 타입, nullable(?), collection expression([...])의 예입니다.
        string learner = "C# 입문자";
        int studyMinutes = 30;
        bool hasStarted = studyMinutes > 0;
        string? memo = null;
        int[] scores = [70, 85, 90];

        Console.WriteLine($"학습자: {learner}, 시작: {hasStarted}, 메모: {memo ?? "없음"}");
        Console.WriteLine($"점수 합계: {scores.Sum()}, 등급: {Grade(scores.Average())}");

        // [중급] 식 본문 메서드(=>)와 switch 식을 결합해 입력을 결과로 매핑합니다.
        static string Grade(double average) => average switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            _ => "다시 연습"
        };
    }
}

// [모델링] enum은 허용 가능한 선택지를 컴파일 타임에 제한합니다.
enum Priority { Low, Normal, High }

// [중급] record는 값 기반 동등성과 with를 이용한 불변 복사를 기본 지원합니다.
sealed record WorkItem(int Id, string Title, Priority Priority, bool IsDone = false);

// [설계] Repository 인터페이스는 저장 기술이 아닌 서비스가 필요한 계약만 노출합니다.
interface IWorkItemRepository
{
    Task<WorkItem> AddAsync(string title, Priority priority);
    Task<WorkItem?> FindAsync(int id);
    Task SaveAsync(WorkItem item);
    Task<IReadOnlyList<WorkItem>> GetAllAsync();
}

sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    // [캡슐화] 변경 가능한 List는 private으로 감추고 외부에는 읽기 전용 계약을 줍니다.
    private readonly List<WorkItem> _items = [];

    public Task<WorkItem> AddAsync(string title, Priority priority)
    {
        var item = new WorkItem(_items.Count + 1, title, priority);
        _items.Add(item);
        return Task.FromResult(item);
    }

    public Task<WorkItem?> FindAsync(int id) => Task.FromResult(_items.SingleOrDefault(x => x.Id == id));

    public Task SaveAsync(WorkItem item)
    {
        int index = _items.FindIndex(x => x.Id == item.Id);
        if (index >= 0) _items[index] = item;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkItem>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<WorkItem>>(new ReadOnlyCollection<WorkItem>(_items));
}

sealed class WorkBoardService(IWorkItemRepository repository)
{
    // [실무] Primary constructor로 계약을 주입받아 메모리/DB 구현과 서비스를 분리합니다.
    public Task<WorkItem> AddAsync(string title, Priority priority)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("작업 제목은 비워 둘 수 없습니다.", nameof(title));
        return repository.AddAsync(title.Trim(), priority);
    }

    public async Task CompleteAsync(int id)
    {
        // [중급] ?? throw는 null이면 즉시 의미 있는 예외를 내는 간결한 표현입니다.
        WorkItem item = await repository.FindAsync(id)
            ?? throw new KeyNotFoundException($"작업 #{id}을 찾지 못했습니다.");
        await repository.SaveAsync(item with { IsDone = true });
    }

    public Task<IReadOnlyList<WorkItem>> GetBoardAsync() => repository.GetAllAsync();
}

static class ProgressCalculator
{
    // [기초→중급] 삼항 연산자로 0 나누기를 막고 LINQ Count로 완료 항목을 집계합니다.
    public static int Percent(IReadOnlyList<WorkItem> items) =>
        items.Count == 0 ? 0 : items.Count(x => x.IsDone) * 100 / items.Count;
}

static class CompositionRoot
{
    // [실무] 구체 구현을 선택하고 객체 그래프를 만드는 한 장소입니다.
    public static WorkBoardService Create() => new(new InMemoryWorkItemRepository());
}

static class BeginnerValidation
{
    // [검증] 외부 테스트 패키지 없이 핵심 규칙의 입력과 기대 결과를 확인합니다.
    public static void Run()
    {
        var checks = new (string Name, bool Passed)[]
        {
            ("빈 목록의 완료율은 0", ProgressCalculator.Percent([]) == 0),
            ("두 작업 중 하나 완료 시 50%", ProgressCalculator.Percent([
                new(1, "완료", Priority.Normal, true), new(2, "미완료", Priority.Normal)]) == 50),
            ("record with는 원본을 바꾸지 않음", CheckRecordCopy()),
            ("빈 제목은 친절한 오류로 거절", CheckEmptyTitle())
        };

        foreach (var (name, passed) in checks)
            Console.WriteLine($"{(passed ? "[통과]" : "[실패]")} {name}");

        int passedCount = checks.Count(x => x.Passed);
        Console.WriteLine($"초보자 검증 통과 ({passedCount}/{checks.Length})");
        if (passedCount != checks.Length) Environment.ExitCode = 1;
    }

    private static bool CheckRecordCopy()
    {
        var before = new WorkItem(1, "연습", Priority.Low);
        var after = before with { IsDone = true };
        return !before.IsDone && after.IsDone;
    }

    private static bool CheckEmptyTitle()
    {
        try
        {
            CompositionRoot.Create().AddAsync("  ", Priority.Low).GetAwaiter().GetResult();
            return false;
        }
        catch (ArgumentException ex)
        {
            return ex.Message.Contains("비워 둘 수 없습니다");
        }
    }
}
