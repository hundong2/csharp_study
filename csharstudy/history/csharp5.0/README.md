# csharp 5.0

## Caller information

- [Caller](./CallerEx.cs). 

|specific|descript|
|---|---|
|CallerMemberName|호출자 정보가 명시된 매서드를 호출한 측의 메서드 이름|
|CallerFilePath|호출자 정보가 명시된 메서드를 호출한 측의 소스코드 파일 경로 |
|CallerLineNumber|호출자 정보가 명시된 메서드를 호출한 측의 소스코드 라인 번호 |

## Async Caller

- `async` and `await`. : 문맥 예약어 
- `await`는 메서드에 `async`가 지정되지 않으면 예약어로 인식되지 않는다. 

```csharp
async void NormalFunc()
{
    int await = 5; //compile error
    Console.WriteLine(await);
}

```

- [Async Example](./asyncEx.cs). 
- [Async Example2](./asyncEx2.cs). 

### Task, Task<TResult> Type

- [Task Example](./TaskEx.cs). 

```csharp
Task taskSleep = new Task(() => { Thread.Sleep(5000); });
taskSleep.Start();
taskSleep.Wait();
```

- [Task Factory StartNew](./TaskFactoryStartNew.md). 

```csharp
Task.Factory.StartNew(
    ()=>{Console.WriteLine("process Taskitem");}
);
Task.Factory.StartNew(
    (obj) => { Console.WriteLine("process taskitem(obj)" ); }, null
);
```

- Task<object> example
    - [Example for Task](./TaskEx2.cs). 
    - using Function<TResult> delegate 
    - Task.Result를 통해 결과 값을 가져 올 수 있다. 
      - 내부에 Wait가 있기 때문에 Task.Wait() 호출은 생략 가능.  

- TaskFactory 

```csharp
Task<int> taskReturn = Task.Factory.StartNew<int>(()=>1);
taskReturn.Wait();
Console.WriteLine(taskReturn.Result);
```

### Async 메서드가 아닌 비동기 처리 

- [Example](./AsyncEx3.cs). 
- `File.ReadAllText` 메서드는 그에 대응되는 비동기 버전의 메서드를 제공하지 않는다. 

```csharp
string text= File.ReadAllText(...);
Console.WriteLine(text);
```

- Example `AsyncEx3.cs`를 통해 await를 통해 ReadAllText를 비동기처럼 사용 

### parallel 

- [Parallel Example](./ParallelEx.cs). 
- diff thread and task
  - [Diff](./diff_thread_task.md).  


