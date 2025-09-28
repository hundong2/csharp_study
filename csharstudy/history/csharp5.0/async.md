# 비동기 메서드(await) 호출 구조 상세 해부  
`ProcessTcpClient` 내부의 두 개 `await`가 어떻게 동작하고 언제 “분기”처럼 보이는지, 실제 실행 흐름을 상태머신 관점과 여러 시나리오로 깊이 있게 설명합니다.

## 예시 파일
[예시 파일 링크 (asyncEx2.puml)](https://raw.githubusercontent.com/hundong2/csharp_study/main/csharstudy/history/csharp5.0/asyncEx2.puml)

## 답변

### 1. 핵심 결론 먼저
1. `ProcessTcpClient` 안의 두 await(ReceiveAsync, SendAsync)는 “동시에 분기”하지 않습니다. 코드는 순차적이며 두 번째 await는 첫 번째가 끝난 뒤 실행 시작됩니다.  
2. “분기”처럼 느껴지는 부분은 각 await 시점에서 “(A) 이미 완료되어 동기로 즉시 진행” vs “(B) 아직 미완료라서 메서드가 한 번 반환(중단)되고, 나중에 재개”라는 ‘런타임 선택지’가 있는 것입니다.  
3. 따라서 “전체 구조가 네 갈래로 나뉜다”기보다 “각 await 마다 최대 한 번 suspend(중단)될 수 있는 지점이 두 개 있다”고 이해하면 됩니다.  
4. 두 await 사이에 병렬성은 없습니다(현재 코드 기준). 첫 번째 I/O가 끝난 후에야 두 번째 I/O 호출이 시작됩니다.  

### 2. 컴파일러가 async/await를 바꾸는 방식 (간단 상태머신 개념)
`async void ProcessTcpClient(Socket client)` 는 컴파일 시 대략 다음과 같은 상태머신(구조체/클래스)로 변환됩니다:

(개념적 의사코드)
```
state = -1 (초기)

MoveNext():
  try {
    if (state == -1) {
       task1 = client.ReceiveAsync(...)
       if (!task1.IsCompleted) {
          state = 0
          등록(완료시 continuation = MoveNext)
          return; // 첫 번째 suspension
       }
       result1 = task1.GetResult()
    }
    if (state == 0) {
       result1 = task1.GetResult() // 재개 후 이어짐
    }

    // 데이터 처리 ...

    task2 = client.SendAsync(...)
    if (!task2.IsCompleted) {
       state = 1
       등록(완료시 continuation = MoveNext)
       return; // 두 번째 suspension
    }
    result2 = task2.GetResult()

    if (state == 1) {
       result2 = task2.GetResult() // 재개
    }

    // 닫기 & 종료
  } catch (...) { 예외 처리 }
```
즉 각 await는 “IsCompleted?” 체크 → 아니면 상태 저장(state 전환) → continuation 등록 → 즉시 메서드 반환 → 완료 후 재진입 패턴을 따릅니다.

### 3. 첫 번째 await와 두 번째 await의 관계
- 첫 번째 await(ReceiveAsync)가 ‘완료/미완료’를 결정한 뒤에야 그 아래 코드(디코딩 → SendAsync 호출)로 내려갈 수 있습니다.
- 두 번째 await(SendAsync)는 첫 번째 await가 끝난 “후”에만 호출됩니다.  
- 따라서 “두 await가 동시에 분기 트리를 만든다”기보다는 “연속된 두 개의 ‘잠재적 중단점’이 있다”는 구조입니다.

### 4. 가능한 실행 4가지 시나리오
| 시나리오 | ReceiveAsync | SendAsync | 외형적 느낌 | 실제 의미 |
|----------|--------------|-----------|-------------|-----------|
| A | 동기 완료 | 동기 완료 | 그냥 일반 함수처럼 직진 | 어떤 suspension도 없음 |
| B | 비동기(중단) | 동기 완료 | 한 번 끊겼다 재개 후 끝 | 1번째 await에서만 suspension |
| C | 동기 완료 | 비동기(중단) | 거의 끝에 가서 한 번 끊김 | 2번째 await에서만 suspension |
| D | 비동기(중단) | 비동기(중단) | 두 번에 걸쳐 재개 | 두 await 모두 suspension 발생 |

시나리오 D가 가장 일반적인(네트워크 I/O) 패턴입니다.

### 5. “분기”라는 표현이 헷갈리는 이유
- if/else 같은 논리 분기가 아니라 “런타임에서 ‘이미 완료되었나?’ 체크 결과에 따라 ‘중단(suspend)할지 말지’ 결정”입니다.
- 컴파일된 상태머신 관점에서는 “같은 코드 경로 + 재진입 위치(state)” 조합으로 표현되므로, 실제로 코드 복제가 이루어지는 분기가 아님.

### 6. async void 특성 때문에 보이는 현상
`async void`는 호출자가 기다릴 Task가 없으므로, 첫 번째 await에서 비동기로 전환되면 “호출자는 이미 메서드가 끝난 것처럼” 보입니다.  
따라서:
- 첫 await가 동기 완료 → 호출자는 메서드가 실제로 시간이 좀 더 걸리는 느낌  
- 첫 await가 비동기 → 호출자는 즉시 리턴(진입 후 바로 복귀)  

실무에서는 `async Task` 를 반환하도록 바꾸는 것이 예외 전파, 테스트, 흐름 제어 측면에서 더 낫습니다.

### 7. 두 await 사이에 ‘동기’ 흐름과 ‘비동기’ 흐름
코드 순서:
1. await ReceiveAsync … (잠재 중단점 #1)
2. UTF8.GetString(...) — 이 부분은 I/O 완료 후 “항상 동기” 코드
3. await SendAsync … (잠재 중단점 #2)
4. Close()

즉, “두 await 사이”는 항상 “첫 번째가 완료된 뒤에 실행되는 순차 코드”입니다.

### 8. 추가 그림 (간단 타임라인)

(1) 둘 다 비동기 (일반적)
```
Caller -> [Enter ProcessTcpClient]
  ReceiveAsync 시작 -> 미완료 -> suspend (return)
    (나중) 수신 완료 -> continuation -> 문자열 처리 -> SendAsync 시작 -> 미완료 -> suspend
        (나중) 송신 완료 -> Close -> 종료
```

(2) 첫 번째만 비동기
```
Enter -> ReceiveAsync 미완료 -> suspend -> (재개) -> 디코딩 -> SendAsync 즉시 완료 -> Close -> 종료
```

(3) 두 번째만 비동기
```
Enter -> ReceiveAsync 즉시 완료 -> 디코딩 -> SendAsync 미완료 -> suspend -> (재개) -> Close
```

(4) 둘 다 동기(드물지만 가능: OS 캐시/loopback)
```
Enter -> ReceiveAsync 즉시 완료 -> 디코딩 -> SendAsync 즉시 완료 -> Close -> 종료 (suspend 없음)
```

### 9. 왜 ReceiveAsync/SendAsync 가 “동기적으로 완료될 수도” 있는가?
소켓 비동기 API라도 내부적으로:
- 커널 버퍼에 이미 데이터가 들어와 있으면 즉시 읽기 완료
- 송신 버퍼에 여유가 충분하면 즉시 송신 완료
→ Task/ValueTask가 이미 완료 상태로 반환 → await는 “스택 풀지 않고” 바로 다음 줄 진행.

### 10. ValueTask 관련 주의
`ReceiveAsync` 가 `ValueTask<int>` 등일 경우:
- 여러 번 await 하면 안 됨 (한 번 소비)
- 결과가 동기라면 할당 줄이는 이점
- 복잡한 조합(저장/재사용) 필요 시 `AsTask()` 고려

### 11. 개선된 구조 (권장 형태: async Task + AcceptAsync)
(설명용 코드 — 현재 리포 파일과 분리된 개념 예시)
```
while (!cts.IsCancellationRequested)
{
    var client = await listener.AcceptAsync(); // 여기서도 await 잠재 suspension
    _ = HandleClientAsync(client); // fire-and-forget (예외 로깅 포함)
}

static async Task HandleClientAsync(Socket client)
{
    var buffer = new byte[4096];
    int received = await client.ReceiveAsync(buffer, SocketFlags.None);
    if (received == 0) { client.Close(); return; }

    var txt = Encoding.UTF8.GetString(buffer, 0, received);
    var payload = Encoding.UTF8.GetBytes("Hello " + txt);
    await client.SendAsync(payload, SocketFlags.None);

    client.Close();
}
```
이제 호출자는 Task를 추적할 수 있고, 예외는 UnobservedTaskException 위험 대신 적절히 처리 가능.

### 12. await 마다 “순차성” 유지 여부
현재 코드 로직에서는 “첫 번째 I/O 끝나야 두 번째 시작”.  
병렬(동시) 처리 원하면:
```
var receiveTask = sock.ReceiveAsync(buf);
var sendTask = SomeOtherSendAsync(...);
await Task.WhenAll(receiveTask, sendTask);
```
처럼 별도의 Task들을 독립적으로 생성해야 합니다. (지금 구조는 그렇지 않음)

### 13. 예외 & 상태 전이
- ReceiveAsync 예외 → 첫 await에서 MoveNext 안에 try/catch로 포착 (async void라면 전역 핸들러로 튈 수도 있음)
- SendAsync 예외 → 두 번째 재개 부분에서 포착
- async void 단점: 호출자가 예외를 직접 잡지 못함

### 14. 흐름 오해를 줄이는 팁
- “await = 논리적 ‘잠재적 중단점’ (checkpoint)”  
- 복잡하면 “각 await는 (동기 빠른 길 / 비동기 느린 길) 둘 중 하나를 선택” 이라고 시각화  
- 두 checkpoint 사이 코드는 항상 직렬(동기)  
- 병렬성이 필요하면 await를 나란히 배치하지 말고 Task 생성 후 결합 (`WhenAll`, `WhenAny`)  

### 15. 한눈에 정리
- 두 await는 “서로 독립적인 잠재 중단점”  
- 두 번째 await는 첫 번째 완료 뒤에만 실행  
- 각각 “동기 or 비동기” 실행 경로가 있을 뿐, 전체 메서드가 트리처럼 복잡하게 갈라지는 것이 아님  
- 네 가지 조합(AA/AB/BA/BB) 이해하면 전체 흐름이 모두 커버됨  
- async void 대신 async Task로 리팩토링 권장  

원하시면:  
1) AcceptAsync 포함 완전 리팩토링 버전 파일 추가  
2) 네 가지 시나리오 각각에 대한 별도 puml 생성  
3) 상태머신 실제 IL/디컴파일 비교  
도 해드릴 수 있어요. 필요하면 말해주세요.

### 추가 자료
- [공식 문서: Async/Await 동작](https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/async/)
- [ValueTask 사용 가이드](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)
- [소켓 비동기 패턴](https://learn.microsoft.com/dotnet/api/system.net.sockets.socket.receiveasync)

원하는 다음 확장 작업(예: 리팩토링 코드 실제 추가, 병렬 처리 예시, puml 시나리오 분리 등)이 있다면 말씀 주세요. 어떤 부분을 더 보고 싶으세요?