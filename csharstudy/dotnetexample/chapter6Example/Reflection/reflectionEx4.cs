using System.Reflection;
using System.Security.Cryptography;

namespace ConsoleApp;

class Program
{
    static void Main(string[] args)
    {
        Assembly asm = Assembly.LoadFrom("./Example1/bin/Debug/net10.0/SystemInfo.dll");
        Type systemInfoType = asm.GetType("ConsoleApp.SystemInfo");
        FieldInfo fieldInfo = systemInfoType.GetField("_is64Bit", BindingFlags.NonPublic | BindingFlags.Instance);
        //private 필드에 접근하기 위해 BindingFlags.NonPublic 플래그 사용, Instance 플래그는 인스턴스 필드를 나타냄
        MethodInfo method = systemInfoType.GetMethod("WriteInfo");
        object objInstance = Activator.CreateInstance(systemInfoType);//instance 생성 
        object oldValue = fieldInfo.GetValue(objInstance);
        Console.WriteLine($"_is64Bit old value : {oldValue}");
        MethodInfo methodInfo = systemInfoType.GetMethod("WriteInfo");
        fieldInfo.SetValue(objInstance, !Environment.Is64BitOperatingSystem);
        methodInfo.Invoke(objInstance, null);
    }
}