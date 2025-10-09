namespace ConsoleApp;

public class Program
{
    public static void Main()
    {
        string txt = "Hello, World!";
        Console.WriteLine(txt[^1]); // '!' 출력
        Console.WriteLine(txt[0..5]); // 'Hello' 출력
        Console.WriteLine(txt[..5]); // 'Hello' 출력
        Console.WriteLine(txt[7..]); // 'World!' 출력

        Console.WriteLine(txt[..]); // 'Hello, World!' 출력
        Console.WriteLine(txt[0..^0]); // 'Hello, World!' 출력, System.Range.All
                                       //Console.WriteLine(txt[^0]); //error 

        System.Index idx = new Index(0, false);// fromStart, fromEnd
        System.Index idx2 = new Index(1, true);
        Console.WriteLine(txt[idx]); // 'H' 출력
        Console.WriteLine(txt[idx2]); // '!' 출력
    }
}