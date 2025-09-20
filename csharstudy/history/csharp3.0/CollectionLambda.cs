public class Program
{
    public static void Main()
    {
        List<int> numbers = new List<int> {1,2,3,4,5,6,7,8,9,10};

        numbers.ForEach(n => Console.WriteLine(n));
        numbers.ForEach( (element) => Console.WriteLine($"Element: {element}"));

        //public static void ForEach<T>(T [] array, Action<T> action)
        Array.ForEach(numbers.ToArray(), n => Console.WriteLine(n * n));

        Console.WriteLine("Using anonymous method:");
        numbers.ForEach(delegate(int n) {
            Console.WriteLine(n * n * n);
        });
    }
}