# 2026-08-12: 지원 티켓 자동 배정

## 처음 읽는 순서

1. 아래 **처음 만나는 기본 문법**을 읽습니다.
2. [`Program.cs`](./src/TicketRoutingExercise/Program.cs)를 위에서 아래로 읽고 실행 흐름을 따라갑니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 작은 변경을 직접 구현합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md) 질문에 말로 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260812/src/TicketRoutingExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 stable SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고를 오류로 처리해 null 위험을 초기에 발견합니다.

## 처음 만나는 기본 문법

- `var service = ...;`: 오른쪽 값에서 변수 형식을 추론하며 문장은 `;`로 끝납니다.
- `string?`: 문자열에 `null`이 올 수 있음을 표시합니다. `IsNullOrWhiteSpace`로 null·빈 값·공백을 함께 검사합니다.
- `enum`: 우선순위처럼 가능한 값을 제한해 문자열 오타를 방지합니다.
- `record`: 값 중심 데이터를 간결하고 불변에 가깝게 표현합니다. `with`로 원본 대신 변경된 복사본을 만듭니다.
- `if`, `?:`: 조건에 따라 실행 경로나 값을 선택합니다.
- 배열 `[]`, `foreach`: 여러 값을 담고 하나씩 반복합니다.
- `interface`: 필요한 행동의 계약입니다. 구현을 교체할 수 있어 DI와 테스트에 유리합니다.
- `Task<T>`, `async`, `await`: DB나 네트워크 I/O를 기다리는 동안 스레드를 불필요하게 붙잡지 않습니다.
- LINQ의 `Where`, `OrderByDescending`, `ToArray`: 필터링, 정렬, 결과 확정을 선언적으로 표현합니다.

## 설계를 읽는 방법

`Program`(Composition Root) → `RouteOpenTicketsService`(Application Service) → `ITicketRepository`(저장) + `ITicketRoutingStrategy`(Domain Model의 정책/Strategy) + `IRoutingNotifier`(외부 알림) 순서입니다. 생성자 매개변수로 의존성을 넣는 DI와 구체 구현보다 인터페이스에 의존하는 DIP 덕분에 가짜 구현으로 빠르게 테스트할 수 있습니다.

Application Service는 유스케이스 순서만 담당하고 각 구성 요소는 한 책임만 맡아 SRP를 지킵니다. 새 배정 정책은 기존 서비스를 고치지 않고 Strategy 구현으로 추가하므로 OCP에도 맞습니다. `record`와 `with`는 공유 데이터의 우발적 변경을 줄입니다. 예상 가능한 “이미 배정됨”은 `Result<T>`로 표현하고, 취소·저장 장애 같은 비정상 상황은 예외로 호출자에게 전파합니다.

실서비스에서는 티켓 ID를 추적 ID와 함께 구조화 로그로 남기고, 처리량·실패율·지연 시간 메트릭과 경보를 둡니다. 저장 뒤 알림이 실패하면 Outbox 같은 신뢰성 패턴을 고려하며, 재시도에는 시간 제한·지수 백오프·멱등성이 필요합니다. 입력 데이터에 개인정보가 있을 수 있으므로 로그에 원문을 무심코 남기지 않습니다.

## 버전 업데이트 (2026-08-12 확인)

- 최신 stable은 **.NET 10.0.10(LTS), SDK 10.0.302, C# 14**입니다. 이 예제는 로컬 stable SDK 10.0.301에서 컴파일되는 기능만 사용합니다.
- 최신 preview는 **.NET 11 Preview 6 / C# 15**입니다. C# 15의 collection-expression arguments, union types, closed hierarchies, extension indexers, memory safety 변경은 preview SDK가 필요하므로 실행 코드에는 넣지 않았습니다.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 다운로드](https://dotnet.microsoft.com/en-us/download), [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/), [C# 15 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 완료 기준

빌드 경고 0개, self-test 4/4, 그리고 Repository·Strategy·Application Service를 각자 한 문장으로 설명할 수 있으면 완료입니다.
