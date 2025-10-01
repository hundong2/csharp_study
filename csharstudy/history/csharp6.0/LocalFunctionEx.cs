public class Program
{
    delegate (bool, int) MyDivide(int x, int y);
    static void Main(string[] args)
    {
        LocalFunctionDelegate();
        LocalFunctionDelegate2();
        LocalFunctionDelegate3();
        LocalFunctionDelegate4();
    }
    public static void LocalFunctionDelegate()
    {
        Console.WriteLine("1. function delegate");
        MyDivide divide = delegate (int x, int y)
        {
            if (y == 0) return (false, 0);
            return (true, x / y);
        };
        var (success, result) = divide(10, 2);
        if (success) Console.WriteLine(result);
    }
    public static void LocalFunctionDelegate2()
    {
        Console.WriteLine("1. function delegate");
        MyDivide divide = (x, y) =>
        {
            if (y == 0) return (false, 0);
            return (true, x / y);
        };
        var (success, result) = divide(10, 2);
        if (success) Console.WriteLine(result);
    }
    public static void LocalFunctionDelegate3()
    {
        Console.WriteLine("2. local function");
        (bool, int) Divide(int x, int y)
        {
            if (y == 0) return (false, 0);
            return (true, x / y);
        }
        var (success, result) = Divide(10, 2);
        if (success) Console.WriteLine(result);
    }
    public static void LocalFunctionDelegate4()
    {
        Console.WriteLine("2. local function with lambda");
        (bool, int) Divide(int x, int y) => (y == 0) ? (false, 0) : (true, x / y);
        var (success, result) = Divide(10, 2);
        if (success) Console.WriteLine(result);
    }
}