using System.Numerics;

namespace ConsoleApp1;

class Program
{
    static void Main(string[] args)
    {
        BigInteger int1 = BigInteger.Parse("123456789012345678901234567890");
        BigInteger int2 = BigInteger.Parse("987654321098765432109876543210");
        BigInteger int3 = 123456789012345678901234567890;
        Console.WriteLine($"int1: {int1}");
        Console.WriteLine($"int2: {int2}");
        Console.WriteLine($"int3: {int3}");
    }
}