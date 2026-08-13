# 2026-08-13: 대량 주문 가져오기 검증과 부분 실패 처리

## 처음 읽는 순서

1. 아래 **처음 만나는 기본 문법**을 먼저 읽습니다.
2. [`Program.cs`](./src/OrderImportExercise/Program.cs)를 위에서 아래로 읽어 실행 흐름을 따라갑니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 작은 변경을 직접 구현합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)에 코드 없이 말로 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260813/src/OrderImportExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 stable SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고를 오류로 처리해 null 위험을 일찍 발견합니다.

## 처음 만나는 기본 문법

- `var rows = new[] { ... };`: 오른쪽 값으로 변수 형식을 추론하고, 여러 값을 배열에 담습니다. 문장은 `;`로 끝납니다.
- `string?`: 문자열이 `null`일 수 있다는 표시입니다. `IsNullOrWhiteSpace`로 null·빈 값·공백을 함께 검사합니다.
- `if`와 `return`: 조건이 참이면 블록을 실행하고, `return`은 결과를 돌려주며 현재 메서드를 끝냅니다.
- `enum`: 가능한 값을 제한해 문자열 오타를 컴파일 가능한 안전한 값으로 바꿉니다.
- `record`: 값 중심 데이터를 간결하고 불변에 가깝게 표현합니다.
- `interface`: 필요한 행동의 계약입니다. 구현을 바꿔 끼울 수 있어 DI와 테스트에 유리합니다.
- `Task<T>`, `async`, `await`: 저장소 같은 I/O를 기다리는 동안 스레드를 붙잡지 않습니다.
- LINQ의 `Select`, `Where`, `Zip`, `ToArray`: 변환, 필터, 두 시퀀스 결합, 결과 확정을 선언적으로 표현합니다.
- `switch` 식과 `_`: 입력값에 맞는 결과를 고르며 `_`는 앞 조건에 맞지 않는 나머지를 뜻합니다.

## 설계를 읽는 방법

`Program`(Composition Root) → `ImportOrdersService`(Application Service) → `IImportPolicy`(Domain 규칙/Strategy) + `IOrderRepository`(저장) + `IImportReporter`(외부 보고) 순서입니다. 생성자 매개변수로 의존성을 주입하는 DI와 인터페이스 의존(DIP) 덕분에 실제 DB나 보고 시스템 없이도 테스트합니다.

검증 규칙은 Strategy 한 곳에 있어 새 고객사 포맷을 추가해도 유스케이스 흐름은 바뀌지 않습니다(OCP). Application Service는 검증·저장·보고의 순서만 맡고 각 구성 요소는 한 책임만 갖습니다(SRP). `record`는 가져온 값과 결과가 중간에 몰래 변경될 여지를 줄입니다. 예상 가능한 행 오류는 `Result<T>`로 모아 부분 성공을 허용하고, 저장소 장애·취소 같은 비정상 문제는 예외로 전파합니다.

실무에서는 입력 파일 크기를 제한하고 스트리밍/배치 처리하며, 파일 해시나 주문 ID로 멱등성을 보장해야 합니다. 전체 원자성이 필요하면 트랜잭션을, 정상 행만 수용한다면 오류 행 격리 저장소를 둡니다. 처리량·실패율·처리 시간을 메트릭으로 남기고 추적 ID를 구조화 로그에 넣되 이메일 같은 개인정보 원문은 기록하지 않습니다. 외부 보고 실패까지 확실히 복구하려면 Outbox와 제한된 지수 백오프 재시도를 고려합니다.

## 버전 업데이트 (2026-08-13 확인)

- 최신 stable은 **.NET 10.0.10(LTS), SDK 10.0.302, C# 14**입니다. 이 예제는 로컬 stable SDK 10.0.301에서 컴파일되는 stable 기능만 사용합니다.
- 최신 preview는 **.NET 11 Preview 6 / C# 15**입니다. C# 15의 collection-expression arguments, union types, closed hierarchies, extension indexers, memory safety 변경은 preview SDK가 필요하므로 실행 코드에 넣지 않았습니다.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 전체 다운로드](https://dotnet.microsoft.com/en-us/download), [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/), [C# 15 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 완료 기준

빌드 경고 0개, self-test 4/4, 그리고 Repository·Strategy·Application Service를 각각 한 문장으로 설명할 수 있으면 완료입니다.
