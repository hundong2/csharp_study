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

class Program
{
    static void Main(string[] args)
    {
        Type systemInfoType = Type.GetType("ConsoleApp.SystemInfo");
        FieldInfo fieldInfo = systemInfoType.GetField("_is64Bit", BindingFlags.NonPublic | BindingFlags.Instance);
        //private 필드에 접근하기 위해 BindingFlags.NonPublic 플래그 사용, Instance 플래그는 인스턴스 필드를 나타냄
        MethodInfo method = systemInfoType.GetMethod("WriteInfo");
        object objInstance = Activator.CreateInstance(systemInfoType);//instance 생성 
        object oldValue = fieldInfo.GetValue(objInstance);
        Console.WriteLine($"_is64Bit old value : {oldValue}");
        //필드의 값을 변경
        fieldInfo.SetValue(objInstance, !Environment.Is64BitOperatingSystem);
        method.Invoke(objInstance, null);



    }
}