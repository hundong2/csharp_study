namespace StringExample;
using System.Text;
class Program
{
    static void Example()
    {
        string txt = "hellow world";
        Console.WriteLine($"txt : StringComparision.OrdinalIgnoreCase {txt.EndsWith("WORLD", StringComparison.OrdinalIgnoreCase)}");
    }
    static void ExampleStringFormat()
    {
        Console.WriteLine($"{string.Format("Hello {0}:{1}", "World", "Anderson") }");
    }
    static void ExampleStringFormat2()
    {
        string txt = "{0, -10} * {1} == {2,10}";
        Console.WriteLine(txt, 5, 6, 5*6);
    }
    static void ExampleStringFormat3()
    {
        string txt = "date: {0, -20:D}, selling numbers: {1, 15:N}";
        Console.WriteLine(txt, DateTime.Now, 267);
    }
    static void ExampleStringFormat4()
    {
        string txt = "Hello world";
        StringBuilder sb = new StringBuilder();
        sb.Append(txt);
        for(int i = 0; i < 30000; i++ )
        {
            sb.Append("1");
        }

    }
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Example();
        ExampleStringFormat();
        ExampleStringFormat2();
        ExampleStringFormat3();
        ExampleStringFormat4();
    }
}
