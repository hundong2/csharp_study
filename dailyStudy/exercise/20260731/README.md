# 2026-07-31 기능 플래그 점진 배포 C# 실습

## 초보자 읽는 순서

1. 아래 **처음 만나는 문법**을 읽습니다.
2. `Program.cs` 맨 위의 실행 코드와 `AssignmentCommand`를 읽습니다.
3. `RolloutService`에서 한 요청의 처리 순서를 따라갑니다.
4. `Assignment.Create`와 두 Strategy에서 업무 규칙을 확인합니다.
5. Repository, DI, Composition Root가 테스트를 쉽게 만드는 이유를 읽습니다.
6. `EXERCISES.md`를 풀고 `CHECKPOINT.md`로 말하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260731/src/FeatureRolloutExercise
dotnet run
dotnet run -- --self-test
```

## 처음 만나는 문법과 기초 문법

- `var service = ...;`에서 `var`는 오른쪽 값으로 지역 변수 타입을 추론하며, `;`는 문장을 끝냅니다.
- `new("new-checkout", ...)`는 문맥에서 타입을 아는 객체 생성이고, 문자열은 `"..."`로 표현합니다.
- `string?`는 `null`일 수 있는 문자열입니다. `?.`, `!`, `is null`은 각각 안전 접근, null 아님 단언, null 검사입니다.
- `if`, `return`, `foreach`, `try/catch`는 조건 분기, 메서드 종료, 반복, 예외 경계를 나타냅니다.
- `async Task<T>`와 `await`는 DB·네트워크 같은 I/O를 기다리는 동안 스레드를 붙잡지 않게 합니다.
- `record`는 값 중심 불변 데이터에 적합하고, `x => x.Variant`는 값을 선택하는 람다 식입니다.
- LINQ의 `FirstOrDefault`, `GroupBy`, `Sum`은 찾기·그룹화·합계를 선언적으로 표현합니다.

## 실무 설계 지도

- **Domain Model**은 유효한 `Assignment`만 만들고 상태 변경을 보호합니다.
- **Application Service**는 플래그 조회 → 정책 선택 → 저장 → 감사 기록의 유스케이스 순서만 조율합니다.
- **Strategy**는 비활성/백분율 배정 정책을 교체 가능하게 해 SOLID의 SRP와 OCP를 돕습니다.
- **Repository**는 저장 기술을 업무 규칙에서 분리합니다.
- **DI/DIP**는 서비스가 구체 클래스 대신 인터페이스에 의존하게 하고, **Composition Root**가 실제 구현을 한곳에서 조립합니다.
- 예측 가능한 입력·업무 실패는 **Result**, DB 단절·버그처럼 정상 흐름으로 복구하기 어려운 실패는 **예외**가 알맞습니다.
- 불변 `record`와 캡슐화한 도메인 객체는 중간 상태를 줄이며, 메모리 Repository는 빠르고 결정적인 테스트를 가능하게 합니다.

운영 환경에서는 안정적인 해시, 플래그 설정 버전, 중복 배정 방지용 idempotency key, DB 트랜잭션, timeout/취소, 제한된 재시도, 감사 로그, 배정 비율 metric과 trace가 필요합니다. 개인정보는 로그에서 마스킹하고 플래그 장애 시 기본값과 긴급 중단 절차도 정해야 합니다.

## 버전 업데이트 (2026-07-31 확인)

- 최신 안정 언어는 **C# 14**이며 .NET 10 SDK에서 지원됩니다.
- **.NET 10은 LTS**이고 최신 패치는 10.0.10, 최신 SDK는 10.0.302입니다. 이 예제는 현재 설치된 안정 SDK 10.0.301과 `net10.0`으로 실행합니다.
- **C# 15와 .NET 11 Preview 6**는 프리뷰입니다. C# 15의 collection expression arguments, union types, closed hierarchies, extension indexers, memory safety 등은 학습용으로만 별도 확인하고 이 안정 예제에는 넣지 않았습니다.

공식 출처:

- [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)
- [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)

## 완료 기준

- `dotnet build`가 경고·오류 없이 끝납니다.
- 일반 실행이 배정 결과를 출력하고 self-test가 `4/4 통과`합니다.
- Result/예외, Domain/Application/Repository/Strategy, DI/Composition Root의 차이를 말로 설명할 수 있습니다.
