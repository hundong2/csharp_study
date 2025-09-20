public class Program
{
    public static void Main(string[] args)
    {
        ExampleFindAll();
        ExampleCount();
    }

    public static void ExampleFindAll()
    {
        List<int> numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Find all even numbers using a lambda expression
        List<int> evenNumbers = numbers.FindAll(n => n % 2 == 0);

        Console.WriteLine("Even Numbers:");
        evenNumbers.ForEach(n => Console.WriteLine(n));
    }
    public static void ExampleCount()
    {
        List<int> numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Count how many numbers are greater than 5 using a lambda expression
        int countGreaterThanFive = numbers.Count(n => n > 5);

        Console.WriteLine("Count of numbers greater than 5: " + countGreaterThanFive);
    }
}