# 2026-07-28 C# 웹훅 전송·재시도 실습

## 초보자 읽기 순서

1. 아래 일반 실행과 자동 검증을 먼저 통과시켜 완성 상태를 확인합니다.
2. `Program.cs`를 실행부 → 기본 타입과 `record` → `Result` → Domain Model → Strategy → Repository → Application Service 순으로 읽습니다.
3. `EXERCISES.md`의 1단계부터 한 번에 하나만 바꾸고 매번 다시 검증합니다.
4. 마지막에 코드를 보지 않고 `CHECKPOINT.md` 질문에 답합니다.

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260728
dotnet run --project .\src\WebhookDeliveryExercise
dotnet run --project .\src\WebhookDeliveryExercise -- --self-test
```

## 처음 만나는 기본 문법

| 문법 | 첫 사용 예 | 의미와 이유 |
| --- | --- | --- |
| 변수와 생성 | `var service = new ...` | 값을 이름으로 보관합니다. `var`도 컴파일 때 실제 타입이 고정됩니다. |
| 배열과 반복 | `new[]`, `foreach` | 여러 명령을 묶고 하나씩 처리합니다. |
| 조건과 분기 | `if`, `?:` | 성공·실패 조건에 따라 다른 코드를 실행합니다. |
| 클래스와 메서드 | `GetDeliveryEndpoint` | 데이터와 그 데이터를 지키는 규칙을 한곳에 둡니다. |
| nullable | `WebhookSubscription?`, `is null`, `!` | 값이 없을 수 있음을 타입으로 표시하고 검사 뒤 사용합니다. `!`는 이미 성공을 확인한 곳에만 씁니다. |
| record와 불변성 | `DeliverWebhookCommand` | 값 중심 메시지를 간결하게 만들고 생성 후 우발적 변경을 줄입니다. |
| LINQ | `Where`, `OrderBy`, `Select` | 컬렉션 필터·정렬·변환의 의도를 단계별로 표현합니다. |
| async/await | `Task`, `await` | 네트워크 대기 중 스레드를 붙잡지 않으며 취소 신호를 전달합니다. |
| 예외 | `ThrowIfCancellationRequested` | 정상 분기가 아닌 취소·장애로 흐름을 즉시 중단합니다. |

## 실무 구조와 설계 선택

```text
Program (Composition Root: 구현을 한곳에서 조립)
  └─ WebhookDeliveryApplicationService (사용 사례 순서)
      ├─ WebhookSubscription (Domain Model: 활성 구독 규칙)
      ├─ IRetryStrategy (Strategy: 교체 가능한 재시도 정책)
      ├─ IWebhookRepository (저장소 경계)
      ├─ IWebhookClient (외부 HTTP 경계)
      └─ IDeliveryLog (운영 관찰 경계)
```

- 생성자 DI로 인터페이스를 주입합니다. 서비스는 구체 저장소·HTTP 구현을 몰라 가짜 구현으로 빠르게 테스트할 수 있고 DIP를 지킵니다.
- Domain Model은 활성 구독 규칙, Application Service는 작업 순서, Strategy는 변경되는 재시도 정책을 맡아 SRP와 OCP를 적용합니다.
- 없는 구독·비활성 구독·재시도 소진 같은 예상 가능한 실패는 `Result`로, 취소·네트워크 라이브러리 장애·프로그래밍 오류는 예외로 다룹니다.
- 실서비스에서는 메시지 ID unique key로 멱등성을 보장하고 timeout, 지수 backoff와 jitter, 재시도 상한, dead-letter queue, circuit breaker를 둡니다. 비밀 서명, payload 크기 제한, UTC, correlation ID, 구조화 로그, 성공률·지연·재시도 횟수 메트릭과 trace도 필요합니다.

## 실습 자료

- [단계별 실습](./EXERCISES.md)
- [초보자 검증 단계](./CHECKPOINT.md)
- [실행 코드](./src/WebhookDeliveryExercise/Program.cs)

## 버전 업데이트 (2026-07-28 확인)

- 로컬 안정 SDK는 `.NET SDK 10.0.301`이며 예제는 `net10.0`, nullable 활성화, 경고를 오류로 처리하는 C# 14 코드로 빌드합니다.
- Microsoft의 [.NET 10 새로운 기능](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)에 따르면 .NET 10은 3년 지원되는 LTS 안정 릴리스입니다.
- [C# 14 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 .NET 10에서 지원되는 최신 안정 언어이며 extension members, null 조건부 할당, `field` 지원 등을 포함한다고 설명합니다.
- 공식 [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)는 2026-07-14 공개된 미리 보기입니다. extension indexers, union 지원 타입, 비동기 검증 등이 포함되지만 로컬에 .NET 11 SDK가 없고 API가 바뀔 수 있어 실행 코드에는 넣지 않았습니다.

## 5분 복습 체크리스트

- [ ] nullable 검사와 record 불변성의 목적을 설명한다.
- [ ] LINQ, async/await, `CancellationToken`의 역할을 설명한다.
- [ ] Result와 예외의 선택 기준을 말한다.
- [ ] Application Service, Domain Model, Repository, Strategy, DI, Composition Root를 연결한다.
- [ ] SRP·OCP·DIP가 테스트 가능성을 높이는 이유를 말한다.
- [ ] 멱등성, timeout, backoff/jitter, dead-letter queue, circuit breaker, 로그·메트릭·trace를 말한다.
