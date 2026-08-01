# 2026-08-01 API 요청 할당량 C# 실습

## 초보자 읽는 순서

1. 아래 **실행 방법**으로 완성본을 먼저 실행합니다.
2. `Program.cs`의 맨 위 실행 코드와 `QuotaCommand`를 읽습니다.
3. `QuotaService`의 요청 처리 순서를 따라갑니다.
4. `QuotaBucket`과 두 Strategy에서 업무 규칙을 확인합니다.
5. Repository, DI, Composition Root가 테스트를 쉽게 만드는 이유를 봅니다.
6. `EXERCISES.md`를 풀고 `CHECKPOINT.md`로 말하며 복습합니다.

## 실행 방법

```powershell
cd dailyStudy/exercise/20260801/src/ApiQuotaExercise
dotnet run
dotnet run -- --self-test
```

## 처음 만나는 문법과 기초 문법

- `var service = ...;`에서 `var`는 오른쪽 값으로 변수 타입을 추론하고, `;`는 문장을 끝냅니다.
- `new("CLIENT-1001", ...)`는 문맥에서 알 수 있는 타입의 객체를 생성합니다. 문자열은 `"..."`로 표현합니다.
- `if`는 조건 분기, `for`와 `foreach`는 반복, `return`은 메서드 종료입니다. `{ }`는 실행 범위를 묶습니다.
- `string?`는 null일 수 있는 문자열입니다. `?.`, `??`, `is null`은 각각 안전 접근, 기본값 선택, null 검사에 씁니다.
- `enum`은 가능한 값을 이름 붙여 제한합니다. `record`는 값 중심의 불변 데이터에 알맞고 `with`는 일부 값만 바꾼 복사본을 만듭니다.
- `async Task<T>`와 `await`는 DB·네트워크 I/O를 기다리는 동안 스레드를 붙잡지 않습니다. `CancellationToken`은 호출자가 대기를 취소하게 합니다.
- LINQ의 `FirstOrDefault`, `Where`, `Sum`은 찾기·필터·합계를 선언적으로 연결합니다. 결과가 없을 수 있는 연산은 nullable 검사가 필요합니다.

## 실무 설계 지도

- **Domain Model** `QuotaBucket`은 올바른 상태와 사용량 증가 규칙을 보호합니다.
- **Application Service** `QuotaService`는 조회, 정책 선택, 판단, 저장, 감사의 유스케이스 순서만 조율합니다.
- **Strategy**는 요금제별 제한을 교체 가능하게 하며 SOLID의 SRP와 OCP를 보여 줍니다.
- **Repository**는 저장 기술을 업무 규칙과 분리합니다.
- **DI/DIP**는 서비스가 구체 DB가 아닌 인터페이스에 의존하게 하고, **Composition Root**는 실제 구현을 한곳에서 조립합니다.
- 예상 가능한 입력·한도 실패는 **Result**, 연결 장애나 버그 같은 예상 밖 실패는 **예외**가 알맞습니다.
- 불변 `record`는 중간 상태 변화를 줄여 비동기 코드와 테스트를 예측 가능하게 합니다.

운영용 할당량 제한은 여러 서버의 동시 요청에도 정확해야 하므로 Redis 원자 연산이나 DB 트랜잭션이 필요합니다. 시간 창 만료, idempotency, timeout·취소, 제한된 재시도, 장애 시 허용/거부 정책을 정해야 합니다. 로그의 토큰·개인정보를 마스킹하고 허용률, 거부율, 저장소 지연 metric과 trace, 정책 버전을 남겨야 원인을 추적할 수 있습니다.

## 버전 업데이트 (2026-08-01 확인)

- 최신 안정 언어는 **C# 14**이며 .NET 10 SDK에서 지원됩니다.
- **.NET 10은 LTS**이고 공식 다운로드 페이지의 최신 패치는 런타임 10.0.10, SDK 10.0.302입니다. 이 실습은 현재 설치된 안정 SDK 10.0.301과 `net10.0`에서 실행합니다.
- **C# 15 / .NET 11 Preview 6**는 미리보기입니다. collection expression arguments, union types, closed hierarchies, extension indexers, memory safety 개선은 별도 미리보기 SDK에서 시험하고 안정 실습에는 섞지 않았습니다.

공식 출처:

- [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)
- [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)

## 완료 기준

- `dotnet build`가 경고와 오류 없이 끝납니다.
- 일반 실행이 할당량 결정을 출력하고 self-test가 `4/4 통과`합니다.
- Result/예외, Domain/Application/Repository/Strategy, DI/Composition Root의 차이를 말로 설명할 수 있습니다.
