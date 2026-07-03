using System;
using System.Runtime.CompilerServices;

public interface IEngineTask<T>
{
    string Execute(T data);
}

public sealed class FastPipelineTask : IEngineTask<string>
{
    public string Execute(string data) => data.Trim();
}

public sealed class ServiceInfrastructure
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public string RunTask<TTask, TData>(TTask task, TData data)
        where TTask : IEngineTask<TData>
    {
        return task.Execute(data);
    }
}

var infra = new ServiceInfrastructure();
var coreTask = new FastPipelineTask();

string result = infra.RunTask(coreTask, "  NET10_PGO_SPEED  ");

Console.WriteLine($"[PGO] Result: {result}");
Console.WriteLine("JIT Dynamic PGO Generics Devirtualized Inlining hot path sample executed.");

/*
실행 결과:
[PGO] Result: NET10_PGO_SPEED
JIT Dynamic PGO Generics Devirtualized Inlining hot path sample executed.
*/
