using System;

namespace nettest
{
    [TypeAttribute(typeof(string))]
    public string Method() => default;
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}
