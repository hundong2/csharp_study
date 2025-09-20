static class ExtentionMethod
{
    public static int GetWordCount(this string str)
    {
        return str.Split(' ').Length;
    }
}
namespace ConsoleApp
{
class Program
{
    static void Main(string[] args)
    {
        string text = "Hello World from Extension Method";
        Console.WriteLine(text.GetWordCount()); 
    }
}
}

