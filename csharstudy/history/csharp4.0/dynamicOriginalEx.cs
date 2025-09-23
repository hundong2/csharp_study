using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;

namespace ConsoleApp;

class Program
{
    public static CallSite<Action<CallSite, object>> p_Site1;
    static void Main(string[] args)
    {
        object d = 5;
        if (p_Site1 == null)
        {
            p_Site1 = CallSite<Action<CallSite, object>>.Create(
                Binder.InvokeMember(
                    CSharpBinderFlags.ResultDiscarded,
                    "CallTest",
                    null,
                    typeof(Program),
                    new[] {
                        CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)
                    }
                )
            );
        }
        p_Site1.Target(p_Site1, d);
    }
}