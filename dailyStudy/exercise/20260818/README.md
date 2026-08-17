# 2026-08-18: 회의실 예약 충돌 해결과 배정 전략

## 처음 읽는 순서

1. 아래 **처음 만나는 기본 문법**을 읽습니다.
2. [`Program.cs`](./src/MeetingRoomExercise/Program.cs)를 위에서 아래로 읽으며 실행 흐름을 따라갑니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 작은 변경을 한 번에 하나씩 구현합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 말로 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260818/src/MeetingRoomExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고를 오류로 처리해 null 위험을 컴파일 단계에서 발견합니다.

## 처음 만나는 기본 문법

- `var requests = new[] { ... };`: 오른쪽 값에서 배열 형식을 추론합니다. C# 문장은 보통 `;`로 끝납니다.
- `string`과 `string?`: `string`은 null이 아니어야 하고 `string?`은 값이 없을 수 있습니다. `is null`로 안전하게 검사합니다.
- `int`, `bool`, `DateTime`, `TimeSpan`: 각각 정수, 참/거짓, 날짜·시각, 기간을 나타냅니다.
- `if`, `foreach`, `continue`, `return`: 조건 분기, 반복, 다음 반복으로 이동, 현재 메서드 종료를 담당합니다.
- `record`: 값 중심 데이터를 간결하고 불변에 가깝게 표현합니다. 예약 요청이 처리 도중 몰래 바뀌는 오류를 줄입니다.
- `interface`: 필요한 동작의 계약입니다. Repository와 Strategy 구현을 테스트용 객체로 교체할 수 있습니다.
- `Task<T>`, `async`, `await`: DB 같은 I/O를 기다릴 때 스레드를 붙잡지 않는 비동기 문법입니다.
- LINQ의 `Where`, `OrderBy`, `ThenBy`, `Any`: 컬렉션 필터, 정렬, 존재 여부를 선언적으로 표현합니다.
- 제네릭 `Result<T>`: `T`를 실제 성공 값 형식으로 정합니다. 성공 여부를 먼저 보고 `Value`를 읽어야 합니다.

## 설계를 읽는 방법

실행 흐름은 `Program`(Composition Root) → `ReserveMeetingsService`(Application Service) → `IRoomSelectionStrategy`(Strategy)와 Repository 순서입니다. 생성자 주입은 구체 구현이 아니라 인터페이스에 의존하게 하는 DI/DIP 방식이어서 단위 테스트가 쉬워집니다.

`MeetingRequest`, `MeetingRoom`, `Reservation` record는 Domain Model의 핵심 용어와 데이터를 드러냅니다. 방 선택과 처리 순서를 분리해 SRP를 지키고, 새 배정 정책은 Strategy 구현 추가로 확장해 OCP를 따릅니다. LINQ 정렬은 결과를 결정적으로 만들며 불변 데이터는 공유 상태 변경을 줄입니다.

입력 오류, 빈 회의실처럼 예상 가능한 실패는 `Result<T>`로 처리합니다. DB 연결 실패, 취소, 코드 계약 위반처럼 정상 업무 분기가 아닌 문제는 예외가 적합합니다. 모든 실패를 예외로 만들면 거절과 장애가 로그에서 섞이고, 모든 예외를 Result로 바꾸면 장애 스택 정보가 흐려집니다.

메모리 Repository는 교육용입니다. 실제 운영에서는 요청 ID와 예약 시간에 DB 제약·트랜잭션·낙관적 또는 비관적 잠금을 적용해야 동시에 들어온 예약의 이중 배정을 막을 수 있습니다. `CancellationToken`을 끝까지 전달하고, 일시 장애 재시도에는 지수 백오프와 횟수 제한을 둡니다. 로그·추적에는 요청 ID, 회의실 ID, 결과, 소요 시간은 남기되 회의 제목 같은 민감 정보는 최소화합니다. 알림 발송까지 원자성이 필요하면 Outbox와 멱등 소비자를 고려하고, 충돌률·예약 실패율·처리 지연을 지표와 경보로 관찰합니다.

## 버전 업데이트 (2026-08-18 확인)

- 최신 안정판은 **.NET 10.0.11(LTS), SDK 10.0.400, C# 14**입니다. 이 예제는 로컬 안정 SDK 10.0.301에서 컴파일되는 안정 기능만 사용합니다.
- 최신 미리 보기는 **.NET 11 Preview 7 / C# 15**입니다. C# 15의 union types, closed hierarchies, extension indexers 같은 기능은 preview SDK가 필요하므로 실행 코드에는 넣지 않았습니다. 미리 보기 기능은 명세와 도구가 바뀔 수 있어 별도 실험 프로젝트에서 평가하세요.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 11 Preview 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/11.0), [C# 14 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 완료 기준

빌드 경고 0개, self-test 4/4, 기본 실행의 예약 2건·거절 2건을 확인하고 Repository·Strategy·Application Service의 책임을 각각 한 문장으로 설명하면 완료입니다.
