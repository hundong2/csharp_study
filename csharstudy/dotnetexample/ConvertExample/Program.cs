namespace ConvertExample;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;


public class Program
{
    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public Person()
        {
        }
        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }
        public override string ToString()
        {
            return $"Name : {this.Name}, Age : {this.Age}";
        }
    }
    static void ExampleJsonSerializer()
    {
        Console.WriteLine("ExampleJsonSerializer");
        Person p = new Person("Anderson", 30);
        System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true, WriteIndented = true};
        string text = System.Text.Json.JsonSerializer.Serialize(p, options);
        Console.WriteLine(text);

        Person p2 = JsonSerializer.Deserialize<Person>(text);
        Console.WriteLine(p2);

    }
    static void ExampleXmlSerializer()
    {
        Console.WriteLine("ExampleXmlSerializer");
        Person p = new Person("Anderson", 30);
        System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(Person));
        System.IO.MemoryStream ms = new System.IO.MemoryStream();
        serializer.Serialize(ms, p);
        ms.Position = 0;
        Person p2 = (Person)serializer.Deserialize(ms);
        Console.WriteLine(p2);

        byte[] buf = ms.ToArray();
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(buf));
    }
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
    static void ExampleBinaryWriter()
    {
        Console.WriteLine("Example Binary Writer, Reader");
        MemoryStream ms = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(ms);
        bw.Write("Hello World" + Environment.NewLine);
        bw.Write("Anderson" + Environment.NewLine);
        bw.Write(32000);
        bw.Flush();

        ms.Position = 0;

        BinaryReader br = new BinaryReader(ms);
        string first = br.ReadString();
        string second = br.ReadString();
        int result = br.ReadInt32();

        Console.Write("{0}, {1}, {2}", first, second, result);
    }
    static public void ExampleXmlSerializer()
    {
        MemoryStream ms = new MemoryStream();
        XmlSerializer xs = new XmlSerializer(typeof(Person));

        Person person = new Person(36, "Anderson");
        //MemoryStream string 
        xs.Serialize(ms, person);
        ms.Position = 0;

        Person clone = xs.Deserialize(ms) as Person;
        Console.WriteLine(clone);
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
        ExampleXmlSerializer();
        ExampleJsonSerializer();
    }
}
