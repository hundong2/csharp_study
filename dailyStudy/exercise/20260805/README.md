# 2026-08-05 미납 청구서 알림 C# 실습

## 초보자 읽는 순서

1. 아래 **처음 만나는 문법**을 읽습니다.
2. `Program.cs`의 맨 위 실행 코드와 `Invoice` record를 읽습니다.
3. `OverdueReminderPolicy` → `SendInvoiceRemindersService` → Repository/Sender 순으로 책임을 따라갑니다.
4. 실행과 self-test를 통과시킨 뒤 `EXERCISES.md`를 풉니다.
5. 마지막에 `CHECKPOINT.md`로 말로 설명할 수 있는지 확인합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260805/src/InvoiceReminderExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

## 처음 만나는 문법과 필수 문법

- `var result = ...;`에서 `var`는 오른쪽 값으로 타입을 추론하고 `;`는 문장 끝입니다. 문자열은 `"..."`, 정수는 `1`, 돈 계산은 오차를 줄이는 `120_000m` 같은 `decimal`을 씁니다.
- `new Invoice(...)`는 객체 생성, `if`는 조건 분기, `return`은 메서드 종료입니다. `=>`는 짧은 식 하나로 값을 정의할 때 사용합니다.
- `string?`와 `DateOnly?`의 `?`는 값이 없을 수 있음을 뜻합니다. `<Nullable>enable</Nullable>`이 실수를 컴파일 단계에서 경고합니다.
- `record`는 값 중심 데이터와 불변성에 알맞습니다. 중간 상태가 바뀌지 않아 추론과 테스트가 쉬워집니다.
- `interface`는 구현체가 지켜야 할 계약입니다. 생성자로 계약을 받는 것이 의존성 주입(DI)이며 가짜 구현으로 교체해 테스트할 수 있습니다.
- LINQ의 `Where`, `Select`, `ToArray`는 모음을 필터링·변환·확정합니다. `async Task<T>`와 `await`는 DB나 네트워크 I/O를 효율적으로 기다립니다.

## 실무 설계 선택

- **Domain Model**: `Invoice`가 결제 여부라는 업무 규칙을 가집니다.
- **Application Service**: `SendInvoiceRemindersService`는 조회, 정책 적용, 전송 순서만 조정합니다.
- **Repository**: 저장 기술을 업무 흐름에서 분리합니다. 메모리 구현은 테스트용이고 실제로는 EF Core 구현으로 바꿀 수 있습니다.
- **Strategy**: `IReminderPolicy`로 알림 규칙을 분리해 등급별 정책을 확장합니다.
- **DI와 Composition Root**: 서비스는 인터페이스에 의존하고 시작점에서 구현체를 한 번 조립합니다.
- **SOLID**: 각 클래스가 하나의 변경 이유를 갖는 SRP, 새 정책을 추가하는 OCP, 추상화에 의존하는 DIP를 적용했습니다.
- **Result와 예외**: 대상 제외처럼 예상 가능한 업무 결과는 Result, 네트워크 중단처럼 비정상 기술 장애는 예외로 구분합니다.
- **테스트 가능성**: 고정 시계와 수집 Sender를 주입해 실제 시간·메일 시스템 없이 경계값을 검증합니다.

## 운영에서는 무엇을 더 고려할까?

메일 API에는 timeout, 제한된 재시도, circuit breaker를 적용하고 청구서 ID를 구조화 로그와 trace에 포함합니다. 중복 알림을 막는 idempotency key, 실패 메시지를 보관하는 dead-letter queue, 성공·실패·처리 시간 metric, 개인정보 마스킹이 필요합니다. 여러 인스턴스가 동시에 처리한다면 분산 잠금이나 DB 상태 전이를 사용하고, 청구서 상태 저장과 이벤트 발행의 원자성이 필요하면 Outbox를 고려합니다.

## 버전 업데이트 (2026-08-05 확인)

- 최신 안정 언어는 **C# 14**, 안정 플랫폼은 **.NET 10 LTS**입니다. 최신 패치는 .NET 10.0.10, 최신 SDK는 10.0.302이며 지원 종료일은 2028-11-14입니다.
- 로컬 설치 안정 SDK는 **10.0.301**이므로 이 예제는 안정 기능만 사용해 `net10.0`으로 실행합니다.
- **C# 15 / .NET 11 Preview 6**은 미리보기입니다. C# 15의 union types, closed hierarchies, extension indexers 등은 별도 preview SDK에서 실험하고 운영 예제에는 넣지 않았습니다.

공식 출처:

- [C# 14의 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [.NET 10 다운로드 및 최신 패치](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [C# 15의 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)
- [.NET 11 Preview 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/11.0)

## 간단 복습 체크리스트

- [ ] 빌드가 경고와 오류 없이 끝난다.
- [ ] 일반 실행은 알림 1건, self-test는 4/4 통과한다.
- [ ] nullable, record/불변성, LINQ, async/await를 설명할 수 있다.
- [ ] Result와 예외를 언제 구분하는지 설명할 수 있다.
- [ ] Domain Model, Application Service, Repository, Strategy의 책임을 구분한다.
- [ ] DI, Composition Root, SOLID가 테스트 가능성을 높이는 이유를 설명한다.
