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
        var task = Task.Run(
            () => throw new CustomException("This exception is expected!"));

        try
        {
            // task.Wait() 시점에 Task 상태 확인 -> Faulted 이면 AggregateException throw.
            task.Wait();
        }
        catch (AggregateException ae)
        {
            // 여러 개 예외가 있을 수 있으므로 InnerExceptions 열거.
            foreach (var ex in ae.InnerExceptions)
            {
                // 우리가 기대한 CustomException 인지 검사.
                if (ex is CustomException)
                {
                    Console.WriteLine(ex.Message); // "This exception is expected!"
                }
                else
                {
                    // 예상 외 예외는 재throw (원래 스택 추적 보존 원하면 throw; 사용)
                    throw;
                }
            }
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

추가 실험 아이디어
--------------------
* CustomException 이외 예외 하나 더 발생시키고 Flatten() 동작 관찰.
* Task.WhenAll(new[]{ faultTask1, faultTask2 }) 로 InnerExceptions 개수 확인.
* ConfigureAwait(false) 가 예외 전파 타이밍/컨텍스트에 영향 주지 않는다는 것 확인.
*/