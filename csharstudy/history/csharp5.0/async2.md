요약된 대화 내용# Socket 비동기 Receive/Send의 await 동작과 시퀀스 다이어그램
단일 메인 스레드에서 async 메서드 호출 후 await 지점에서 어떻게 흐름이 “중단→재개”되는지, 그리고 Socket.ReceiveAsync / SendAsync 가 비동기적으로 작동하는 원리를 시퀀스 다이어그램(PlantUML)으로 설명합니다.

## 예시 파일
[Microsoft 공식 비동기 소켓 샘플](https://learn.microsoft.com/dotnet/fundamentals/networking/sockets/socket-services)

## 답변
아래 원본 코드 핵심(요약):
```csharp
while (true)
{
    var client = listener.Accept();          // 동기 accept (블로킹)
    ProcessTcpClient(client);                // async void (fire & forget)
}

private static async void ProcessTcpClient(Socket client)
{
    byte[] buffer = new byte[1024];
    int received = await client.ReceiveAsync(buffer); // 1) 수신 대기
    string txt = Encoding.UTF8.GetString(buffer, 0, received);
    byte[] sendBuffer = Encoding.UTF8.GetBytes("Hello " + txt);
    await client.SendAsync(sendBuffer);               // 2) 송신 대기
    client.Close();
}
```

### 1. await가 실제로 하는 일 (상태 머신 관점)
C# 컴파일러는 async 메서드를 “비동기 상태 머신”으로 변환합니다.

1. 메서드 진입 → 첫 await 이전까지 동기 실행.
2. `client.ReceiveAsync(buffer)` 호출:
   - 이미 데이터가 도착해 내부적으로 즉시 완료 가능하면(“synchronously completed”) Task/ValueTask가 IsCompleted 상태 → 같은 스레드에서 바로 다음 코드 이어감.
   - 아직 도착 전이라면 “미완료 Task” 반환 → 상태 저장(현재 위치, 지역 변수), 제어권 메서드 호출자에게 반환 → 호출자는 (여기선 async void라) 그냥 리턴.
3. OS 네트워크 스택이 I/O 완료 신호(IOCP / epoll/kqueue) → 런타임이 해당 Task 완료 표시 → 이어서 등록된 continuation(상태 머신의 다음 단계)을 ThreadPool 큐로 스케줄.
4. ThreadPool 워커가 깨어나 await 다음 줄부터 재개.
5. 두 번째 await(SendAsync)도 동일한 패턴 반복.
6. 마지막까지 실행 후 async void 이므로 Task 결과/예외를 호출자에게 전달할 방법 없음 (예외는 SynchronizationContext / Unobserved 로 흘러갈 수 있음).

### 2. 두 await가 “동시에” 실행되지 않는 이유
`await ReceiveAsync`가 끝나야 그 다음 줄(문자열 처리)이 실행되고, 그 뒤에 `await SendAsync`가 시작됩니다. 즉:
- Receive → (데이터 수신 완료 후) → Send 순차 체인.
- “둘 다 요청을 던져놓고 병렬 진행”되는 구조가 아님.
병렬화를 원하면 별도 Task 생성 후 Task.WhenAll 사용 패턴 등 필요.

### 3. Socket.ReceiveAsync / SendAsync 비동기 원리 (간단 OS 레벨 요약)
플랫폼별로:
- Windows: IOCP(Completion Port)에 비동기 I/O 제출 → 즉시 반환 → 완료 시 Completion Packet → 스레드풀 I/O completion 스레드가 Task 완료 처리.
- Linux: epoll / macOS: kqueue 기반 런타임 추상화 → 비차단 소켓 + 이벤트 루프 등록 후 완료 시 콜백 → Task 완료.
핵심: CPU를 점유하며 폴링하지 않고 OS 커널에게 “데이터 준비되면 알려줘”라고 위임.

### 4. 현재 구조의 한계
- `listener.Accept()`가 블로킹이므로 많은 연결이 몰리면 Accept 대기 동안 메인 스레드가 묶임.
- `async void`는 예외 처리/흐름 제어가 어려움.
- 개선: `AcceptAsync` + `async Task` 반환 + CancellationToken.

### 5. 개선된 예 (권장 구조)
```csharp
static async Task Main()
{
    var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    listener.Bind(new IPEndPoint(IPAddress.Any, 11200));
    listener.Listen(100);

    Console.WriteLine("Server started.");

    while (true)
    {
        var client = await listener.AcceptAsync();     // 비동기 Accept
        _ = HandleClientAsync(client);                 // fire & forget (Task 반환)
    }
}

static async Task HandleClientAsync(Socket client)
{
    using (client)
    {
        var buffer = new byte[1024];
        int received = await client.ReceiveAsync(buffer, SocketFlags.None);
        string txt = Encoding.UTF8.GetString(buffer, 0, received);
        byte[] sendBuffer = Encoding.UTF8.GetBytes("Hello " + txt);
        await client.SendAsync(sendBuffer, SocketFlags.None);
    }
}
```
(실제 서비스에서는 예외 로깅, 타임아웃, CancellationToken, 재사용 버퍼(ArrayPool<byte>) 등을 추가.)

### 6. PlantUML 시퀀스 다이어그램 (.puml)
아래 다이어그램은 “수신이 즉시 완료되는 경우” vs “비동기 대기”를 alt 블록으로 표현, Receive 단계와 Send 단계 각각의 await 흐름을 보여줍니다.

```puml
@startuml
title Async Socket Receive/Send Await 흐름

actor Client
participant "Main Thread" as Main
participant "Program" as Prog
participant "Listener Socket" as Listener
participant "Client Socket" as CSock
participant "OS Network Stack" as OS
participant "ThreadPool\n(IO Completion)" as TP

== 서버 시작 ==
Main -> Listener: Bind + Listen
Main -> Listener: Accept() (블로킹 대기)

== 새 연결 ==
Client -> Listener: TCP Connect
Listener --> Main: return Socket (client)
Main -> Prog: ProcessTcpClient(client)  (async void)

== ProcessTcpClient 진입 ==
Prog -> CSock: ReceiveAsync(buffer)
alt 데이터 즉시 도착(동기 완료)
  CSock --> Prog: ValueTask (IsCompleted=true)
  note right of Prog: await 'synchronously completes'\n다음 줄 즉시 계속
else 대기(비동기 I/O)
  CSock --> Prog: 미완료 Task/ValueTask
  Prog --> Main: (메서드 반환 - 상태머신 보관)
  OS <- CSock: I/O 요청 등록
  ...시간 경과...
  OS -> TP: 수신 완료 신호(IOCP/epoll)
  TP -> Prog: continuation 실행 (await 이후 재개)
end

Prog -> Prog: txt = UTF8.GetString(...)
Prog -> CSock: SendAsync("Hello "+txt)
alt 송신 버퍼 즉시 처리
  CSock --> Prog: 완료 Task
else 비동기 송신 진행
  CSock --> Prog: 미완료 Task
  Prog --> (return to caller)
  OS <- CSock: 비동기 전송 요청
  OS -> TP: 전송 완료 통지
  TP -> Prog: continuation (await 이후)
end

Prog -> CSock: Close()

== 메인 루프는 계속 Accept ==
Main -> Listener: Accept() (다음 연결 대기)
@enduml
```

### 7. await가 “스레드를 만든다”는 오해 정리
- await 자체가 스레드를 추가 생성하지 않습니다.
- I/O 대기 동안 스레드는 반환되어 다른 작업 수행 가능.
- 완료 시점에 “다른(ThreadPool) 스레드”가 이어받아 실행할 수 있어 보이지만, 이는 스케줄링 결과이지 필수 규칙은 아님(동기 완료 시 같은 스레드 지속).

### 8. Receive / Send 비동기 흐름 핵심 문장
1. API 호출 → OS 커널에 비동기 I/O 등록 → 즉시 반환.
2. 커널이 데이터 준비/전송 완료 → 런타임에 완료 통지.
3. 런타임이 Task(또는 ValueTask) 상태를 Completed로 바꾸고 continuation 큐잉.
4. 스케줄러(ThreadPool)가 continuation 실행 → await 다음 코드 진행.

### 9. async void 주의
- 예외 전파 어려움 → Task 반환으로 변경 권장.
- 테스트/조합(Task.WhenAll) 불가 → 로깅·모니터링 문제.

### 10. 병렬 처리를 원한다면
- 여러 I/O를 동시에 대기: `var receiveTask = sock.ReceiveAsync(...); var timeoutTask = Task.Delay(...); var done = await Task.WhenAny(receiveTask, timeoutTask);`
- 파이프라인 구조: 수신 → 처리 → 송신을 분리한 전용 Task 체인.

### 추가 자료
- [C# async/await 내부 동작(상태 머신)](https://learn.microsoft.com/dotnet/csharp/language-reference/language-specification/async)
- [비동기 소켓 가이드](https://learn.microsoft.com/dotnet/fundamentals/networking/sockets/asynchronous)
- [I/O Completion Ports(IOCP) 설명](https://learn.microsoft.com/windows/win32/fileio/i-o-completion-ports)
- [ThreadPool / Task 스케줄링](https://learn.microsoft.com/dotnet/standard/parallel-programming/the-managed-thread-pool)
- [ValueTask 사용 지침](https://learn.microsoft.com/dotnet/standard/asynchronous-programming/value-task)
