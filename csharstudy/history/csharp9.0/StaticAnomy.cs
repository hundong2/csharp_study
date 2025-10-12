namespace ConsoleApp;

public class Program
{
    public static void Main()
    {
        Console.WriteLine("ExampleStaticAnomy");
        ExampleStaticAnomy();
        Console.WriteLine("ExampleStaticAnomy2");
        ExampleStaticAnomy2();
    }

    public static void ExampleStaticAnomy()
    {
        string title = "console: ";

        Func<int, string> func = static i =>
        {
            //return title + i.ToString(); // error, static 람다 안에서는 외부 변수 참조 불가
            return i.ToString();
        };
        Console.WriteLine(func(123));
    }
    public static void ExampleStaticAnomy2()
    {
        const string title = "console: ";

        Func<int, string> func = static delegate (int i)
        {
            return title + i.ToString(); // ok, 일반 람다 안에서는 const 변수 참조 가능, const 자체가 static 유형 
        };
        Console.WriteLine(func(123));
    }

}