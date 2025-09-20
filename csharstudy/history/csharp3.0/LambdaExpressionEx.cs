public class Program
{
    delegate int? MyMinus(int a, int b);
    public static void Main()
    {
        // Lambda expression that takes two integers and returns their sum
        Func<int, int, int> add = (x, y) => x + y;
        MyMinus myFunc = (a, b) => a - b;
        // Using the lambda expression
        int result = add(5, 3);
        Console.WriteLine(result); // Output: 8
        Console.WriteLine("10-2==" + myFunc(10, 2));
    }
}