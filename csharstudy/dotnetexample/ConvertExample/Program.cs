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
    static void ExampleMemoryStream()
    {
        byte[] shortBytes = BitConverter.GetBytes((short)32000);
        byte[] intBytes = BitConverter.GetBytes(1652300);

        MemoryStream ms = new MemoryStream();
        ms.Write(shortBytes, 0, shortBytes.Length);
        ms.Write(intBytes, 0, intBytes.Length);

        ms.Position = 0;

        //short data read from MemoryStream ms
        byte[] outBytes = new byte[2];
        ms.Read(outBytes, 0,2);
        int shortResult = BitConverter.ToInt16(outBytes, 0);
        Console.WriteLine(shortResult);

        outBytes = new byte[4];
        ms.Read(outBytes, 0, 4);
        int intResult = BitConverter.ToInt32(outBytes, 0);
        Console.WriteLine(intResult);
    }
    static void ExampleMemoryStream2()
    {
        Console.WriteLine("Memory Stream Example : Using ToArray of MemoryStream");
        byte[] shortBytes = BitConverter.GetBytes((short)32000);
        byte[] intBytes = BitConverter.GetBytes(1652300);

        MemoryStream ms = new MemoryStream();
        ms.Write(shortBytes, 0, shortBytes.Length);
        ms.Write(intBytes, 0, intBytes.Length);

        byte[] buf = ms.ToArray();

        int shortResult = BitConverter.ToInt16(buf, 0);
        Console.WriteLine(shortResult);

        int intResult = BitConverter.ToInt32(buf, 2);
        Console.WriteLine(intResult);
    }
    static void ExampleStreamEncoding()
    {
        Console.WriteLine("ExampleStreamEncoding");
        //Using Encoding 
        MemoryStream ms = new MemoryStream();
        byte[] buf = System.Text.Encoding.UTF8.GetBytes("Hello World");
        ms.Write(buf, 0, buf.Length);
        ms.Position = 0;
        StreamReader sr = new StreamReader(ms, System.Text.Encoding.UTF8);
        string txt = sr.ReadToEnd();
        Console.WriteLine(txt); //Hello World
    }
    static MemoryStream ExampleStreamWriter()
    {
        Console.WriteLine("StreamWriter to StreamReader");
        MemoryStream ms = new MemoryStream();
        StreamWriter sw = new StreamWriter(ms, System.Text.Encoding.UTF8);
        sw.WriteLine("Hello world");
        sw.WriteLine("Anderson");
        sw.Write("32000");
        sw.Flush();

        return ms;
    }
    static void ExampleStreamReader(MemoryStream ms)
    {
        ms.Position = 0;

        StreamReader sr = new StreamReader(ms, System.Text.Encoding.UTF8);
        string txt = sr.ReadToEnd();
        Console.WriteLine(txt);
    }
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
        ExampleBitConvert(); //little endian
        ExampleBitConvert2();
        ExampleMemoryStream();
        ExampleMemoryStream2();
        ExampleStreamEncoding();
        ExampleStreamReader(ExampleStreamWriter());
    }
}
