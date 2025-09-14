using System.Reflection;

namespace ConsoleApp;

public class SystemInfo
{
    bool _is64Bit;
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
        //Tightly Coupling Code 
        SystemInfo sysInfo = new SystemInfo();
        sysInfo.WriteInfo();

        Console.WriteLine("\nReflection Example");
        //리플렉션을 사용해 느슨하게 결합 된 코드(loosely coupling code) 
        Type type = Type.GetType("ConsoleApp.SystemInfo");
        object objInstance = Activator.CreateInstance(type);
        //activator타입의 CreateInstance 메서드를 사용하여 객체 생성, 
        //타입 생성자를 리플렉션으로 구해서 직접 호출하는 것도 가능 

        ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
        //GetConstructor 메서드를 사용하여 매개변수가 없는 생성자 정보를 가져옴
        object objInstance2 = ctor.Invoke(null);
        //Invoke 메서드를 사용하여 생성자를 호출하고 객체를 생성(Invocation- 생성자 호출)

        MethodInfo method = type.GetMethod("WriteInfo");
        method.Invoke(objInstance2, null);
    }
}