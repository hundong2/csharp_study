using System.Reflection;

class Promgram
{
    static void Main(string[] args)
    {
        AppDomain currentDomain = AppDomain.CurrentDomain;
        Console.WriteLine("Current Domain Name: " + currentDomain.FriendlyName);
        foreach (var asm in currentDomain.GetAssemblies())
        {
            Console.WriteLine(" "+ asm.FullName);
        }
    }
}