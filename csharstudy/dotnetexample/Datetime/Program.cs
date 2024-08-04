namespace Datetime;

class Program
{
    static void Example1()
    {
        DateTime now =  DateTime.Now;
        Console.WriteLine($"{now} : {now.Kind}");

        DateTime utcNow = DateTime.UtcNow;
        Console.WriteLine($"{utcNow} : {utcNow.Kind}");

        DateTime worldcup2002 = new DateTime(2002, 5, 31);
        Console.WriteLine($"{worldcup2002} : {worldcup2002.Kind}");

        worldcup2002 = new DateTime(2002, 5, 31, 0, 0, 0, DateTimeKind.Local);
        Console.WriteLine($"{worldcup2002} : {worldcup2002.Kind}");
    }
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        Example1();
    }
}
