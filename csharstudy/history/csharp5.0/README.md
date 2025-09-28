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

