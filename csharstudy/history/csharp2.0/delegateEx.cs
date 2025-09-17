class Program
{
    delegate int? MyDivide(int a, int b);

    static void Main(string[] args)
    {
        Thread thread = new Thread(
            delegate (object? obj)
            {
                Console.WriteLine("Thread Start: {0}", obj);
            }
        );
        MyDivide divide = delegate (int a, int b)
        {
            if (b == 0) return null;
            return a / b;
        };
        Console.WriteLine(divide(10, 2));
        Console.WriteLine(divide(10, 0));

        thread.Start("Thread 1");
        thread.Join();
    }
}