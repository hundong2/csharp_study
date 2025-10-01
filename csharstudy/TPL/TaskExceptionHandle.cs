using System;
using System.Threading.Tasks;

// 예제 목적:
// 1. Task 내부에서 발생한 예외는 호출 스레드로 바로 전파되지 않고 Task 객체에 저장된다.
// 2. Task.Wait() 또는 task.Result 접근 시 Task가 Faulted 상태면 AggregateException으로 래핑되어 throw 된다.
// 3. AggregateException.InnerExceptions 순회로 개별 실제 예외를 확인/필터링/처리할 수 있다.
// 4. 사용자 정의 예외(CustomException) 분기 처리 후 예상치 못한 다른 예외는 다시 throw 하는 패턴을 보여준다.
// 5. async/await 사용 시에는 보통 try/await/catch 로 단순화되지만, 여기서는 기본 TPL 패턴을 학습하기 위해 Wait 사용.

public static partial class Program
{
    public static void Main()
    {
        // 동기 진입점: 여기서는 데모 목적상 Wait() 패턴을 보여주기 위해
        // HandleThree() 호출. 실제 최신 코드라면 Main 자체를 async Task Main 으로
        // 선언하고 await 패턴을 쓰는 것이 더 자연스럽다.
        HandleThree();
    }
    
    /// <summary>
    /// Task.Run 으로 백그라운드 작업을 시작하고, 해당 작업에서 발생한 사용자 정의 예외를
    /// AggregateException 을 통해 포착 및 처리하는 예제.
    /// </summary>
    public static void HandleThree()
    {
        // Task.Run 내부 람다에서 즉시 CustomException throw.
        // 예외는 지금 바로 메인까지 전파되지 않고 Task 객체에 기록됨.
        // (중요) Task.Run 은 작업을 ThreadPool 큐에 넣고 곧바로 Task(미완료)를 반환.
        // 람다가 실행되는 순간 throw 되지만, 호출 스레드는 try 블록을 계속 진행.
        var task = Task.Run(() => throw new CustomException("This exception is expected!"));

        try
        {
            // task.Wait(): 비동기 Task 를 동기 대기 (블로킹). UI/ASP.NET SynchronizationContext
            // 환경에서는 데드락 위험이 될 수 있으나 여기서는 콘솔이므로 안전.
            // Faulted 상태라면 AggregateException throw.
            task.Wait();
        }
        catch (AggregateException ae)
        {
            // (패턴 1) ae.Handle: 각 InnerException 에 대해 Predicate 실행.
            // true 반환 → 해당 예외 처리됨(무시), false → 처리되지 않은 예외 모아서
            // 모든 처리 후 다시 AggregateException 으로 throw.
            ae.Handle(ex =>
            {
                // 우리가 예상한 사용자 정의 예외만 처리.
                if (ex is CustomException)
                {
                    Console.WriteLine(ex.Message); // 메시지 출력 후 처리 완료 표시(true)
                    return true; // true -> swallow
                }
                // false 반환하면 Handle 처리 끝난 뒤 rethrow 될 것.
                return false;
            });

            // (참고) 패턴 2 - 직접 순회:
            // foreach(var ex in ae.Flatten().InnerExceptions) { ... }
            // 필요 시 throw; 로 다시 던질 수 있음.
        }

    }
}

/// <summary>
/// 사용자 정의 예외. 특정 도메인/의미 있는 오류 의미 전달용.
/// </summary>
public class CustomException : Exception
{
    public CustomException(string message) : base(message) { }
}

// 출력 예:
// This exception is expected!

/*
학습 포인트 정리
-----------------
1. 예외 저장 방식: Task 내부 예외는 즉시 throw 되지 않고 Task.Exception(AggregateException)에 저장.
2. 전파 시점: Wait(), Result, GetAwaiter().GetResult() 호출할 때 래핑되어 전파.
3. AggregateException 처리 패턴: ae.Flatten() 사용해 중첩 AggregateException 펼칠 수 있음.
4. 재throw 권장: 예상 외 예외는 throw; (throw ex; 는 스택 추적 재설정) -> 여기서는 교육용으로 throw; 활용.
5. async/await 전환: HandleThree 를 async Task 로 바꾸고 await task; 하면 CustomException 직접 catch 가능.
6. 병렬 다수 작업: Task.WhenAll 사용 시도 중 하나라도 Faulted 면 AggregateException에 모든 Faulted Task 예외 담김.
7. Best Practice: 새 코드에서는 불필요한 동기 Wait() 지양(데드락 위험/UI 프리즈). 가능한 await 사용.
8. ae.Handle vs 수동 순회: Handle 은 true/false 로 swallow 제어, 수동 순회는 더 유연(로그/분류)하나 직접 rethrow 처리 필요.
9. 동기 Wait 데드락 위험: UI SynchronizationContext (WinForms/WPF) + ConfigureAwait(default) 조합에서 블로킹 대기 금지.
10. 예외 관찰: await 사용 시 첫 Faulted 예외를 그대로 throw → 코드 간결.

추가 실험 아이디어
--------------------
* CustomException 이외 예외 하나 더 발생시키고 Flatten() 동작 관찰.
* Task.WhenAll(new[]{ faultTask1, faultTask2 }) 로 InnerExceptions 개수 확인.
* ConfigureAwait(false) 가 예외 전파 타이밍/컨텍스트에 영향 주지 않는다는 것 확인.
 * Handle 패턴과 직접 foreach 패턴 비교 성능(미미하지만 구조 이해 목적) 측정.
 * async 변환 버전:
     async Task HandleThreeAsync() {
             try { await Task.Run(() => throw new CustomException("expected")); }
             catch(CustomException ce) { Console.WriteLine(ce.Message); }
     }
 * 여러 Fault Task:
     var t1 = Task.Run(() => throw new CustomException("A"));
     var t2 = Task.Run(() => throw new InvalidOperationException("B"));
     try { await Task.WhenAll(t1, t2); } catch(AggregateException ae) { foreach(var e in ae.InnerExceptions) Console.WriteLine(e.GetType().Name); }
*/