# 2026-07-30 주문 환불 처리 C# 실습

## 초보자 읽기 순서

1. 아래 문법 지도를 읽습니다.
2. `Program.cs`의 실행 코드와 `RefundCommand`를 읽습니다.
3. `Refund.Create`에서 규칙과 Result를 확인합니다.
4. `RefundService`에서 흐름을 따라갑니다.
5. Strategy, Repository, Composition Root를 읽고 self-test를 실행합니다.
6. `EXERCISES.md`를 풀고 `CHECKPOINT.md`로 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260730/src/RefundProcessingExercise
dotnet run
dotnet run -- --self-test
```

## 처음 만나는 문법 지도

- `var`는 오른쪽 값으로 지역 변수 타입을 추론하고 `;`은 문장 끝입니다.
- `new(...)`은 객체 생성, `35_000m`의 밑줄은 가독성, `m`은 decimal 표시입니다.
- `string?`는 null 가능 문자열이며 nullable 검사가 실수를 컴파일 단계에서 찾습니다.
- `if`, `return`, `foreach`, `try/catch`는 조건, 종료, 반복, 예외 경계입니다.
- `async Task<T>`와 `await`는 I/O 대기 중 스레드를 막지 않습니다.
- `record`는 값 중심 불변 데이터, `x => x.Amount`는 LINQ에 전달하는 람다입니다.

## 실무 설계 지도

- Domain Model은 상태와 규칙을 보호하고 Application Service는 유스케이스 순서를 조율합니다.
- Strategy는 정책을 교체·추가하며 OCP를 돕고 Repository는 저장 기술을 숨깁니다.
- DI/DIP는 인터페이스를 생성자로 받고 Composition Root는 실제 구현을 조립합니다.
- 예상 가능한 실패는 Result, 인프라 장애나 버그는 예외로 구분합니다.
- 메모리 Repository는 DB 없이 빠르고 결정적인 테스트를 가능하게 합니다.

운영에서는 DB 트랜잭션·동시성, idempotency key, timeout/취소, 알림 outbox/재시도, metric/trace를 추가합니다. 로그에는 환불 ID를 구조화해 남기고 이메일은 마스킹합니다.

## 버전 업데이트 (2026-07-30 확인)

- 최신 안정 언어는 **C# 14**이며 .NET 10 SDK에서 사용할 수 있습니다.
- **.NET 10은 LTS**이고 최신 패치는 10.0.10, 최신 SDK는 10.0.302입니다. 예제는 설치된 안정 SDK 10.0.301로 실행합니다.
- **.NET 11 Preview 6**에는 extension indexer, union 지원 타입, async validation 등이 포함됩니다. Preview는 운영용이 아니므로 실행 코드에는 넣지 않았습니다.

공식 출처:

- [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [.NET 공식 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)

## 완료 기준

- 일반 실행에서 환불 ID와 `Approved`가 출력됩니다.
- self-test가 `4/4 통과`로 끝납니다.
- 연습 문제를 풀고 체크리스트를 말로 설명할 수 있습니다.
