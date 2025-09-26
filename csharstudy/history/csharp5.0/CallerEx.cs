using System.Runtime.CompilerServices;

namespace ConsoleApp1;

class Program
{
    static void Main(string[] args)
    {
        LogMessage("Hello, World!");
    }
    static void LogMessage(String text, 
        [CallerMemberName] String memberName = "",
        [CallerFilePath] String sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        {
            Console.WriteLine($"Message: {text}");
            Console.WriteLine($"Member Name: {memberName}");
            Console.WriteLine($"Source File Path: {sourceFilePath}");
            Console.WriteLine($"Source Line Number: {sourceLineNumber}");
        }
}
