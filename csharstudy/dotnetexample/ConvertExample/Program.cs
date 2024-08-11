namespace ConvertExample;
using System;
class Program
{
    static void ExampleBitConvert()
    {
        byte[] boolBytes = BitConverter.GetBytes(true);
        byte[] shortBytes = BitConverter.GetBytes((short)32000);
        byte[] intBytes = BitConverter.GetBytes(1652300);

        bool boolResult = BitConverter.ToBoolean(boolBytes,0);
        short shortResult = BitConverter.ToInt16(shortBytes,0);
        int intResult = BitConverter.ToInt32(intBytes, 0);
        
        Console.WriteLine(BitConverter.ToString(boolBytes));
        Console.WriteLine(BitConverter.ToString(shortBytes));
        Console.WriteLine(BitConverter.ToString(intBytes));

    }
    static void ExampleBitConvert2()
    {
        byte[] buf = new byte[4];
        buf[0] = 0x4c;
        buf[1] = 0x36;
        buf[2] = 0x19;
        buf[3] = 0x00;

        int result = BitConverter.ToInt32(buf, 0);
        string resultStr = BitConverter.ToString(buf);
        Console.WriteLine(result); 
        Console.WriteLine($"string result {resultStr}");
    }
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        ExampleBitConvert(); //little endian
        ExampleBitConvert2();
    }
}
