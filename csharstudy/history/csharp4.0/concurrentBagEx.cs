using System.Collections.Concurrent;
public class Program
{
    static ConcurrentBag<int> bag = new ConcurrentBag<int>();
    static void Main(string[] args)
    {
        foreach (var item in Enumerable.Range(0, 10))
        {
            bag.Add(item);
        }
        ThreadPool.QueueUserWorkItem(_ =>
        {
            ChangeList();
        });

        ThreadPool.QueueUserWorkItem(_ =>
        {
            EnumerateList();
        });

        Console.ReadLine();
    }
    public static void EnumerateList()
    {
        foreach (var item in bag)
        {
            Console.WriteLine(item);
        }
    }
    private static void ChangeList()
    {
        for (int i = 0; i <= 10; i++)
        {
            bag.Add(100 + i);
            Thread.Sleep(16);
        }
    }
}