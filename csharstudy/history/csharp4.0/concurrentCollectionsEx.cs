internal class Program
{
    static List<int> list = new List<int>();
    static void Main(string[] args)
    {
        list.AddRange(Enumerable.Range(1, 100));
        ChangeList();
        EnumerateList();

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
