using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

class Program
{
    static void Main()
    {
        var results = new ConcurrentBag<int>();
        Parallel.For(0, 100, i =>
        {
            // Simulate some work
            Task.Delay(100).Wait();
            results.Add(i);
        });

        Console.WriteLine($"Processed {results.Count} items in parallel.");
    }
}