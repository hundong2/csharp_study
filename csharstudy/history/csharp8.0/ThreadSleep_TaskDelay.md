# Task.Delay와 Thread.Sleep의 차이
Task.Sleep는 존재하지 않고, 비동기 지연은 Task.Delay를 사용합니다. Thread.Sleep은 현재 스레드를 “블로킹”하고, Task.Delay는 스레드를 “반납하는 비동기 대기”입니다.

## 예시 파일
[Async streams 예제 파일 (dotnet/samples)](https://github.com/dotnet/samples/blob/main/snippets/csharp/keywords/AsyncStreams/Program.cs)

## 답변
- Thread.Sleep(ms)
  - 현재 스레드를 지정 시간 동안 블로킹.
  - 스레드풀이 줄어 확장성 저하, UI/ASP.NET 등에서는 응답 멈춤 위험.
  - 취소 지원 없음, await 불가.
- Task.Delay(ms, token)
  - 타이머 기반 비동기 대기. 스레드 점유 없음 → 확장성 우수.
  - await로 자연스럽게 사용, CancellationToken 지원.
  - UI/서버 코드, 비동기 메서드에서 권장.

핵심 선택 기준
- 비동기 메서드/서버/UI: await Task.Delay 사용.
- 동기 콘솔 데모/테스트에서 “의도적 블로킹”이 필요할 때만 Thread.Sleep.

예시: 사용 중인 코드 수정(AsyncStream2.cs)
````csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp;

public class Program
{
    public static async Task Main()
    {
        await foreach (var number in GetNumbersAsync(10))
        {
            Console.WriteLine(number);
        }
    }

    // 비동기 스트림은 async IAsyncEnumerable<T>로 선언하고, Thread.Sleep 대신 Task.Delay 사용
    public static async IAsyncEnumerable<int> GetNumbersAsync(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(100); // 비동기 지연(스레드 비점유)
            yield return i;
        }
    }
}
````

추가 팁
- await Task.Delay는 동기 컨텍스트를 캡처합니다. 라이브러리 코드에서는 ConfigureAwait(false) 고려.
- 정확한 타이밍이 필요한 경우(고정밀 타이머)는 별도 타이머/스케줄러 사용을 검토.

### 추가 자료
- Task.Delay 문서: https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay
- Thread.Sleep 문서: https://learn.microsoft.com/dotnet/api/system.threading.thread.sleep
- C# 비동기 프로그래밍 개요: https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/
- Async streams: https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-streams