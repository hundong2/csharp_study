using System.Reflection;

namespace ConsoleApp;

class Program
{
    private static void ProcessPlugIn(string rootPath)
    {
        Console.WriteLine($"ProcessPlugIn : {rootPath}");
        foreach (var dllPath in Directory.GetFiles(rootPath, "*.dll"))
        {
            Assembly pluginDll = Assembly.LoadFrom(dllPath);
            var entryType = FindEntryType(pluginDll);
            if (entryType == null) continue;

            object instance = Activator.CreateInstance(entryType);
            var entryMethod = FindStartUpMethod(entryType);
            if (entryMethod == null) continue;
            entryMethod.Invoke(instance, null);
        }
    }
    static void Main(string[] args)
    {
        var pluginFolder = @"plugins";
        if (Directory.Exists(pluginFolder) == true)
        {
            Console.WriteLine("Plugin Folder Exist");
            ProcessPlugIn(pluginFolder);
        }
        else
        {
            Console.WriteLine("Plugin Folder Not Exist");
        }
    }
    private static Type? FindEntryType(Assembly pluginDll)
    {
        foreach (var type in pluginDll.GetTypes())
        {
            foreach (var objAttr in type.GetCustomAttributes(false))
            {
                if (objAttr.GetType().Name == "PluginAttribute")
                {
                    return type;
                }
            }
        }
        return default;
    }
    private static MethodInfo? FindStartUpMethod(Type entryType)
    {
        foreach (var methodInfo in entryType.GetMethods())
        {
            foreach (var objAttr in methodInfo.GetCustomAttributes(false))
            {
                if (objAttr.GetType().Name == "StartUpAttribute")
                {
                    return methodInfo;
                }
            }
        }
        return default;
    }

}