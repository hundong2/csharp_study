class Program
{
    public static void Main(string[] args)
    {
        var a = new { Name = "Alice", Age = 30 };
        var b = new { Name = "Bob", Age = 25 };

        System.Console.WriteLine($"Name: {a.Name}, Age: {a.Age}");
        System.Console.WriteLine($"Name: {b.Name}, Age: {b.Age}");
    }
}

/*
it will be changed in machine code 

internal sealed class Anonymoustype0<T1, T2>
{
private readonly T1 V1Field;
private readonly T2 V2Field;

public Anonymoustype0(T1 V1, T2 V2)
{{
V1Field = v1;
V2Field = v2;
}}
public T1 V1 { get { return V1Field; } }
public T2 V2 { get { return V2Field; } }

class Program
{
    public static void Main(string[] args)
    {
        var a = new Anonymoustype0<string, int>("Alice", 30);
        var b = new Anonymoustype0<string, int>("Bob", 25);

        System.Console.WriteLine($"Name: {a.V1}, Age: {a.V2}");
        System.Console.WriteLine($"Name: {b.V1}, Age: {b.V2}");
    }
}
*/