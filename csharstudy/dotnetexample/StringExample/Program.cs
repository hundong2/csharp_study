namespace StringExample;
using System.Text;
using System.Text.RegularExpressions;
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
    static string funcMatch(Match match)
    {
        return "Universe";
    }
    static void ExampleRegex2()
    {
        string txt = "Hello, World! Welcome to my world!";
        Regex regex = new Regex("world", RegexOptions.IgnoreCase);
        string result = regex.Replace(txt, funcMatch);
        Console.WriteLine(result);
    }
    static void ExampleRegex()
    {
        string email = "tester@test.com";
        Regex regex = new Regex(@"^([0-9a-zA-Z]+)@([0-9a-zA-Z]+)(\.[0-9a-zA-Z]+){1,}$");
        Console.WriteLine(regex.IsMatch(email));
    }
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Example();
        ExampleStringFormat();
        ExampleStringFormat2();
        ExampleStringFormat3();
        ExampleStringFormat4();
        ExampleRegex();
        ExampleRegex2();
    }
}
