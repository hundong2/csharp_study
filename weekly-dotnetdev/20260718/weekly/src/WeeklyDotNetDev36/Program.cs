using System.Diagnostics;
using System.Runtime.InteropServices;

return await WeeklyDemos.RunAsync(args);

internal static class WeeklyDemos
{
    public static async Task<int> RunAsync(string[] args)
    {
        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "all";

        if (command == "child")
        {
            RunChildWriter();
            return 0;
        }

        return command switch
        {
            "all" => await RunAllAsync(),
            "process" => await RunProcessOutputDemoAsync(),
            "false-sharing" => RunFalseSharingDemo(),
            "query-budget" => RunQueryBudgetDemo(),
            "help" => PrintHelp(),
            _ => UnknownCommand(command)
        };
    }

    private static async Task<int> RunAllAsync()
    {
        await RunProcessOutputDemoAsync();
        Console.WriteLine();
        RunFalseSharingDemo();
        Console.WriteLine();
        RunQueryBudgetDemo();
        return 0;
    }

    private static async Task<int> RunProcessOutputDemoAsync()
    {
        Console.WriteLine("== Process output demo ==");

        using var process = StartChildWriter();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var exitTask = process.WaitForExitAsync();

        await Task.WhenAll(stdoutTask, stderrTask, exitTask);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Console.WriteLine($"Exit code   : {process.ExitCode}");
        Console.WriteLine($"stdout lines: {CountLines(stdout)}");
        Console.WriteLine($"stderr lines: {CountLines(stderr)}");
        Console.WriteLine("Pattern     : start both reads before waiting for process exit.");

        return process.ExitCode;
    }

    private static Process StartChildWriter()
    {
        var currentProcessPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot locate current process path.");

        var psi = new ProcessStartInfo
        {
            FileName = currentProcessPath,
            Arguments = "child",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start child process.");
    }

    private static void RunChildWriter()
    {
        for (var i = 0; i < 5_000; i++)
        {
            Console.Out.WriteLine($"stdout:{i:D4}: process api practice payload");
            Console.Error.WriteLine($"stderr:{i:D4}: process api practice payload");
        }
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var count = 0;
        using var reader = new StringReader(text);

        while (reader.ReadLine() is not null)
        {
            count++;
        }

        return count;
    }

    private static int RunFalseSharingDemo()
    {
        Console.WriteLine("== False sharing demo ==");

        var workers = Math.Clamp(Environment.ProcessorCount, 2, 8);
        const int operationsPerWorker = 8_000_000;

        var adjacent = Measure("adjacent long[]", () => RunAdjacentCounters(workers, operationsPerWorker));
        var padded = Measure("padded struct[]", () => RunPaddedCounters(workers, operationsPerWorker));

        Console.WriteLine($"Workers     : {workers}");
        Console.WriteLine($"Operations  : {operationsPerWorker:N0} per worker");
        Console.WriteLine($"Adjacent    : {adjacent.ElapsedMilliseconds} ms, sum={adjacent.Sum:N0}");
        Console.WriteLine($"Padded      : {padded.ElapsedMilliseconds} ms, sum={padded.Sum:N0}");
        Console.WriteLine("Note        : hardware, power mode, and background load can change the ratio.");

        return 0;
    }

    private static Measurement Measure(string name, Func<long> run)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var stopwatch = Stopwatch.StartNew();
        var sum = run();
        stopwatch.Stop();

        return new Measurement(name, stopwatch.ElapsedMilliseconds, sum);
    }

    private static long RunAdjacentCounters(int workers, int operationsPerWorker)
    {
        var counters = new long[workers];
        var tasks = new Task[workers];

        for (var worker = 0; worker < workers; worker++)
        {
            var slot = worker;
            tasks[slot] = Task.Run(() =>
            {
                for (var i = 0; i < operationsPerWorker; i++)
                {
                    counters[slot]++;
                }
            });
        }

        Task.WaitAll(tasks);
        return counters.Sum();
    }

    private static long RunPaddedCounters(int workers, int operationsPerWorker)
    {
        var counters = new PaddedCounter[workers];
        var tasks = new Task[workers];

        for (var worker = 0; worker < workers; worker++)
        {
            var slot = worker;
            tasks[slot] = Task.Run(() =>
            {
                for (var i = 0; i < operationsPerWorker; i++)
                {
                    counters[slot].Value++;
                }
            });
        }

        Task.WaitAll(tasks);
        return counters.Sum(counter => counter.Value);
    }

    private static int RunQueryBudgetDemo()
    {
        Console.WriteLine("== Query budget demo ==");

        var nPlusOneCounter = new QueryCounter();
        var nPlusOneStore = new FakeOrderStore(nPlusOneCounter);
        var nPlusOne = BuildSummariesWithNPlusOne(nPlusOneStore);
        PrintSummaries("N+1 path", nPlusOne, nPlusOneCounter.Count, budget: 2);

        Console.WriteLine();

        var projectionCounter = new QueryCounter();
        var projectionStore = new FakeOrderStore(projectionCounter);
        var projection = projectionStore.LoadOrderSummaries();
        PrintSummaries("Projection path", projection, projectionCounter.Count, budget: 2);

        return 0;
    }

    private static IReadOnlyList<OrderSummary> BuildSummariesWithNPlusOne(FakeOrderStore store)
    {
        var orders = store.LoadOrders();
        var summaries = new List<OrderSummary>();

        foreach (var order in orders)
        {
            var lines = store.LoadLinesForOrder(order.Id);
            summaries.Add(new OrderSummary(order.Id, order.CustomerName, lines.Count, lines.Sum(line => line.Quantity)));
        }

        return summaries;
    }

    private static void PrintSummaries(string label, IReadOnlyList<OrderSummary> summaries, int queryCount, int budget)
    {
        Console.WriteLine(label);

        foreach (var summary in summaries)
        {
            Console.WriteLine($"  order #{summary.OrderId}: {summary.CustomerName}, lines={summary.LineCount}, quantity={summary.TotalQuantity}");
        }

        Console.WriteLine($"  queries: {queryCount}, budget: {budget}, status: {(queryCount <= budget ? "PASS" : "FAIL")}");
    }

    private static int PrintHelp()
    {
        Console.WriteLine("Usage: dotnet run -- [all|process|false-sharing|query-budget|help]");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintHelp();
        return 1;
    }

    private sealed record Measurement(string Name, long ElapsedMilliseconds, long Sum);

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct PaddedCounter
    {
        [FieldOffset(0)]
        public long Value;
    }
}

internal sealed class QueryCounter
{
    public int Count { get; private set; }

    public void Record(string queryName)
    {
        Count++;
        Console.WriteLine($"  query {Count:00}: {queryName}");
    }
}

internal sealed class FakeOrderStore
{
    private readonly QueryCounter _counter;

    private readonly List<Order> _orders =
    [
        new(101, "Contoso"),
        new(102, "Fabrikam"),
        new(103, "Northwind"),
        new(104, "Adventure Works"),
        new(105, "Tailspin")
    ];

    private readonly List<OrderLine> _lines =
    [
        new(101, "SDK", 2),
        new(101, "Book", 1),
        new(102, "License", 5),
        new(103, "Workshop", 3),
        new(103, "Support", 1),
        new(103, "Addon", 4),
        new(104, "Hosting", 2),
        new(105, "Consulting", 6)
    ];

    public FakeOrderStore(QueryCounter counter)
    {
        _counter = counter;
    }

    public IReadOnlyList<Order> LoadOrders()
    {
        _counter.Record("select Orders");
        return _orders;
    }

    public IReadOnlyList<OrderLine> LoadLinesForOrder(int orderId)
    {
        _counter.Record($"select OrderLines where OrderId = {orderId}");
        return _lines.Where(line => line.OrderId == orderId).ToList();
    }

    public IReadOnlyList<OrderSummary> LoadOrderSummaries()
    {
        _counter.Record("select Orders left join OrderLines group by Orders.Id");

        return _orders
            .GroupJoin(
                _lines,
                order => order.Id,
                line => line.OrderId,
                (order, lines) =>
                {
                    var materializedLines = lines.ToList();
                    return new OrderSummary(
                        order.Id,
                        order.CustomerName,
                        materializedLines.Count,
                        materializedLines.Sum(line => line.Quantity));
                })
            .ToList();
    }
}

internal sealed record Order(int Id, string CustomerName);
internal sealed record OrderLine(int OrderId, string Sku, int Quantity);
internal sealed record OrderSummary(int OrderId, string CustomerName, int LineCount, int TotalQuantity);
