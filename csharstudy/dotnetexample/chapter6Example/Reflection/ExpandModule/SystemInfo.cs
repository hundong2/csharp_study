using System;

namespace ExpandModule;

[PluginAttribute]
public class SystemInfo
{
    private readonly bool _is64Bit; // readonly로 고정
    public SystemInfo()
    {
        _is64Bit = Environment.Is64BitOperatingSystem;
        Console.WriteLine("SystemInfo created.");
    }

    [StartUpAttribute]
    public void WriteInfo()
    {
        // 문자열 보간으로 간결화
        Console.WriteLine($"OS=={(_is64Bit ? 64 : 32)}bits");
    }
}

// 부착 대상/정책 명시
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PluginAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class StartUpAttribute : Attribute { }