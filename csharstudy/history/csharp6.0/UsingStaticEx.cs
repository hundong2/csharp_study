using static MyDay;
using static BitMode;
public enum MyDay
{
    Saturday, Sunday, Monday, Tuesday, Wednesday, Thursday, Friday
}
public class BitMode
{
    public const int Bit32 = 32;
    public const int Bit64 = 64;
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Using Static Example");
        Console.WriteLine("Day: " + Saturday);
        Console.WriteLine("Bit Mode: " + Bit64);
        /**
        Using Static Example
        Day: Saturday
        Bit Mode: 64
        */
    }
}