using System.Collections.ObjectModel;

if (args.Contains("--self-test"))
{
    BeginnerValidation.Run();
    return;
}

Console.WriteLine("=== 오늘의 작업 보드 ===");
SyntaxTour.Show();

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
        string learner = "C# 입문자";
        int studyMinutes = 30;
        bool hasStarted = studyMinutes > 0;
        string? memo = null;
        int[] scores = [70, 85, 90];

        Console.WriteLine($"학습자: {learner}, 시작: {hasStarted}, 메모: {memo ?? "없음"}");
        Console.WriteLine($"점수 합계: {scores.Sum()}, 등급: {Grade(scores.Average())}");

        static string Grade(double average) => average switch
        {
            >= 90 => "A",
            >= 80 => "B",
            >= 70 => "C",
            _ => "다시 연습"
        };
    }
}

enum Priority { Low, Normal, High }

sealed record WorkItem(int Id, string Title, Priority Priority, bool IsDone = false);

interface IWorkItemRepository
{
    Task<WorkItem> AddAsync(string title, Priority priority);
    Task<WorkItem?> FindAsync(int id);
    Task SaveAsync(WorkItem item);
    Task<IReadOnlyList<WorkItem>> GetAllAsync();
}

sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
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
    public Task<WorkItem> AddAsync(string title, Priority priority)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("작업 제목은 비워 둘 수 없습니다.", nameof(title));
        return repository.AddAsync(title.Trim(), priority);
    }

    public async Task CompleteAsync(int id)
    {
        WorkItem item = await repository.FindAsync(id)
            ?? throw new KeyNotFoundException($"작업 #{id}을 찾지 못했습니다.");
        await repository.SaveAsync(item with { IsDone = true });
    }

    public Task<IReadOnlyList<WorkItem>> GetBoardAsync() => repository.GetAllAsync();
}

static class ProgressCalculator
{
    public static int Percent(IReadOnlyList<WorkItem> items) =>
        items.Count == 0 ? 0 : items.Count(x => x.IsDone) * 100 / items.Count;
}

static class CompositionRoot
{
    public static WorkBoardService Create() => new(new InMemoryWorkItemRepository());
}

static class BeginnerValidation
{
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
