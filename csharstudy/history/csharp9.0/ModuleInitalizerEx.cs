using System.Runtime.CompilerServices;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Module Initializer Example");
    }

}
class Module
{
    [ModuleInitializer]
    internal static void DllMain()
    {
        Console.WriteLine("Module Initialized");
    }
}

//Output:

//Module Initialized
//Module Initializer Example