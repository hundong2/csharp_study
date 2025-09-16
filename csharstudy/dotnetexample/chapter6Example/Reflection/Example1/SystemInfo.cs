using System.Reflection;

namespace ConsoleApp;

public class SystemInfo
{
    bool _is64Bit;//private field
    public SystemInfo()
    {
        _is64Bit = Environment.Is64BitOperatingSystem;
        Console.WriteLine("SystemInfo created.");
    }
    public void WriteInfo()
    {
        Console.WriteLine($"OS : {Environment.OSVersion}, 64Bit : {_is64Bit}");
        Console.WriteLine($"ProcessorCount : {Environment.ProcessorCount}");
        Console.WriteLine($"SystemDirectory : {Environment.SystemDirectory}");
        Console.WriteLine($"UserDomainName : {Environment.UserDomainName}");
        Console.WriteLine($"UserName : {Environment.UserName}");
        Console.WriteLine($"CLR Version : {Environment.Version}");
        Console.WriteLine($"Current Directory : {Environment.CurrentDirectory}");
        Console.WriteLine($"MachineName : {Environment.MachineName}");
        Console.WriteLine($"TickCount : {Environment.TickCount}");
        Console.WriteLine($"WorkingSet : {Environment.WorkingSet}");
    }
}