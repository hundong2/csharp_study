namespace ConsoleApp;

public class Utility
{
    public static T Max<T>(T a, T b) where T : System.IComparable<T>
    {
        return a.CompareTo(b) > 0 ? a : b;
    }
}
public class Program
{
    public static void Main(string[] args)
    {
        System.Console.WriteLine(Utility.Max<int>(10, 20));
        System.Console.WriteLine(Utility.Max<string>("A", "B"));
        System.Console.WriteLine(Utility.Max<double>(10.5, 20.5));
    }
}