class Program
{
    static void Main(string[] args)
    {
        Action<string> logOut = (txt) => Console.WriteLine(txt);
        logOut("Action delegate example");

        Func<double> pi = () => 3.14;
        Console.WriteLine("Value of pi: " + pi());

        Func<int, int, int> multiply = (x, y) => x * y;
        Console.WriteLine("3 * 4 = " + multiply(3, 4));
    }
}