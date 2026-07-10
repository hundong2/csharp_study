using System;
using System.Threading.Tasks;

// .NET에는 Java의 Virtual Thread와 같은 이름의 기능이 공식 API로 존재하지 않습니다.
// 이 예제는 "작업 객체를 별도 래퍼 없이 스케줄러에 넘기는 구조"를 실행 가능한 코드로 학습합니다.

public sealed class RawVolatileTask
{
    // get 전용 속성입니다. 외부에서 TaskId를 바꿀 수 없습니다.
    public int TaskId => 90812;
}

public interface IVirtualTaskScheduler
{
    ValueTask DispatchAsync(RawVolatileTask task);
}

public sealed class StaticLaneScheduler : IVirtualTaskScheduler
{
    public ValueTask DispatchAsync(RawVolatileTask task)
    {
        // ValueTask:
        // 결과가 이미 준비된 경우 Task 할당을 줄일 수 있는 비동기 반환 타입입니다.
        Console.WriteLine($"[Scheduler] Task {task.TaskId} dispatched through static lane.");
        return ValueTask.CompletedTask;
    }
}

var raw = new RawVolatileTask();
IVirtualTaskScheduler scheduler = new StaticLaneScheduler();
await scheduler.DispatchAsync(raw);
