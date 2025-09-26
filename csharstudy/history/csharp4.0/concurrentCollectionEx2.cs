internal class Program
{
    private static List<int> list = new List<int>();
    private static void Main(string[] args)
    {
        list.AddRange(Enumerable.Range(0, 100));
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
        foreach (var item in list)
        {
            Console.WriteLine(item);
        }
    }
    private static void ChangeList()
    {
        for (int i = 0; i <= 10; i++)
        {
            list.Add(100 + i);
            Thread.Sleep(16);
        }
    }
}