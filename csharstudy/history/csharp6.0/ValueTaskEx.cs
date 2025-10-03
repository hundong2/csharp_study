using System.Threading.Tasks;

namespace ConsoleApp;

public class Program
{
    static void Main(string[] args)
    {
        ExampleNormalAsync();
        Console.ReadLine();
    }
    static async Task ExampleNormalAsync()
    {
        //Task<(string, int tid)> result =
        ValueTask<(string, int tid)> result =
            FileReadAsync(@"README.md");
        int tid = Thread.CurrentThread.ManagedThreadId;
        Console.WriteLine($"MainThreadID: {tid}, AsyncTaskID: {result.Result.tid}");
    }

    //private static async Task<(string, int)> FileReadAsync(string filePath)
    private static async ValueTask<(string, int)> FileReadAsync(string filePath)
    {
        string fileText = await File.ReadAllTextAsync(filePath);
        return (fileText, Thread.CurrentThread.ManagedThreadId);
    }
    static Task<string> ReadAllTextAsync(string filePath)
    {
        return Task.Factory.StartNew(() =>
        {
            return File.ReadAllText(filePath);
        });
    }
}