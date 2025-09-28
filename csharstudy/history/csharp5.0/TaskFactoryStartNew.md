# Task.Factory.StartNew 두 오버로드 차이  
Action(delegate) vs Action<object> + state 매개변수 차이, 캡처(closure) 비용, 메모리 할당, 사용 의도 구분 방법 설명.  
## 예시 파일  
[예시 코드 (GitHub .NET samples Task 사용 예)](https://github.com/dotnet/samples/blob/main/core/parallel/Tasks/TaskSamples/Program.cs)  
## 답변  
요약  
- 첫 번째: `Task.Factory.StartNew(() => {...});` → Action 사용, 바깥 변수를 캡처하면 숨은 참조형(closure) 객체 할당.  
- 두 번째: `Task.Factory.StartNew(obj => {...}, stateObj);` → Action<object> + 명시적 state 전달. 캡처 줄여 메모리 할당을 줄일 수 있음.  
- 둘 다 기본적으로 Task를 바로 스케줄(이미 Start된 상태). 차이는 delegate 시그니처와 state 전달 방식.  
- 단순 비동기 실행 목적이면 `Task.Run(...)` 권장(간결, await-friendly). `StartNew`는 고급 옵션(커스텀 스케줄러, TaskCreationOptions, LongRunning 등) 필요할 때.  

주요 비교  

| 항목 | `StartNew(() => ...)` | `StartNew(s => ..., state)` |
|------|-----------------------|-----------------------------|
| delegate 시그니처 | Action | Action<object> |
| 추가 state 전달 | 캡처(closure) 필요 | 별도 매개변수(state) |
| 불필요한 할당 회피 | 어려움(캡처 시 발생) | 가능(캡처 줄이기 용도) |
| 메서드 호출 시 인자 | 없음 | state boxing 가능(값 형식이면) |
| 사용 의도 | 간단한 람다 | 고성능/고빈도 + 상태 전달 최적화 |
| 캡처된 변수 변경 반영 | 가능(참조 통해) | 전달된 state 읽기 전용 권장 |

코드 예 (차이 체감)  
````csharp
using System;
using System.Threading;
using System.Threading.Tasks;

class Demo
{
    static void Main()
    {
        int counter = 42;

        // 1) 캡처 사용: counter를 람다가 포획 → closure 객체 할당
        Task t1 = Task.Factory.StartNew(() =>
        {
            Console.WriteLine($"Captured counter = {counter}");
        });

        // 2) state 전달: counter 값 복사(값 형식이면 boxing 없음: int -> object boxing O)
        Task t2 = Task.Factory.StartNew(
            state =>
            {
                int local = (int)state!;
                Console.WriteLine($"State counter = {local}");
            },
            counter);

        Task.WaitAll(t1, t2);
    }
}
````

캡처 비용 줄이기 패턴  
````csharp
var buffer = new byte[1024];
// 캡처 방식 (buffer 캡처 → closure)
Task.Factory.StartNew(() => Use(buffer));

// state 방식 (closure 회피)
Task.Factory.StartNew(o => Use((byte[])o!), buffer);

void Use(byte[] arr) { /* 처리 */ }
````

Task.Run 권장 이유  
````csharp
// 단순: 내부적으로 StartNew + 기본 스케줄러 + Unwrap 처리
var task = Task.Run(() => Compute());
await task;

int Compute() => 123;
````

async delegate 주의  
````csharp
// StartNew + async 람다: Task<Task> (unwrap 안 됨) → 실수 위험
var nested = Task.Factory.StartNew(async () =>
{
    await Task.Delay(100);
    Console.WriteLine("Done");
});
// nested.Wait();  // 내부 await 완료 전 블로킹될 수 있음 (주의)

// 올바른 방법
await Task.Run(async () =>
{
    await Task.Delay(100);
    Console.WriteLine("Done");
});
````

언제 StartNew 선택?  
- TaskScheduler 커스텀 지정 필요  
- TaskCreationOptions(LongRunning, PreferFairness 등) 필요  
- 매우 빈번한 생성 경로에서 closure 할당 줄이고 싶은 경우(state 매개변수 활용)  

TaskCreationOptions 예  
````csharp
var t = Task.Factory.StartNew(
    () => HeavyLoop(),
    CancellationToken.None,
    TaskCreationOptions.LongRunning,
    TaskScheduler.Default);

void HeavyLoop()
{
    for (int i = 0; i < 1_000_000; i++) { /* CPU 바운드 */ }
}
````

성능 메모  
- closure 1개 할당 ≈ 작은 객체 비용 → 고빈도(수십만+) 경로에서 누적됨  
- state 매개변수는 값 형식이면 boxing 발생(피하려면 struct → ref struct 불가)  
- 고성능 시나리오: 캐시된 static 메서드 + state 전달 조합 사용  

요약 문장  
- 두 호출 차이는 “캡처 기반 vs 명시적 state 인자” 구조.  
- 단순 병렬 작업엔 Task.Run, 정교한 스케줄/옵션이 필요할 때 StartNew(state) 사용.  
- async 람다는 Task.Run이 안전(자동 Unwrap).  

### 추가 자료  
- [Task.Factory.StartNew 문서](https://learn.microsoft.com/dotnet/api/system.threading.tasks.taskfactory.startnew)  
- [Task.Run 권장 사항](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.run)  
- [비동기 프로그래밍 개요](https://learn.microsoft.com/dotnet/csharp/async)  
- [TaskCreationOptions 설명](https://learn.microsoft.com/dotnet/api/system.threading.tasks.taskcreationoptions)