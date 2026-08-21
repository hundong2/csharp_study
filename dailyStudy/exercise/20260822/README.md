# 2026-08-22: 비밀번호 재설정 요청 계획

## 처음 읽는 순서

1. 아래 **기초 문법 지도**를 먼저 읽습니다.
2. [`Program.cs`](./src/PasswordResetExercise/Program.cs)를 위에서 아래로 읽으며 `조립 → 실행 → 모델 → 계약 → 구현 → 테스트` 흐름을 찾습니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 1단계부터 한 번에 하나씩 수정합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 설명하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260822/src/PasswordResetExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고도 오류로 처리해 null 위험을 일찍 찾습니다.

## 기초 문법 지도

- `var now = ...;`에서 `var`는 오른쪽 값으로 변수 형식을 추론하며 문장은 보통 `;`로 끝납니다.
- `string`은 null이 아니어야 하고 `string?`는 값이 없을 수 있습니다. `IsNullOrWhiteSpace`는 null·빈 문자열·공백을 함께 검사합니다.
- `int`는 정수, `bool`은 참/거짓, `enum`은 제한된 이름 목록, `DateTimeOffset`은 시간대 오프셋을 포함한 시각입니다.
- `if`는 조건 분기, `foreach`는 반복, `return`은 값을 돌려주거나 메서드를 끝냅니다.
- `record`는 값 중심 불변 데이터에, `class`는 행동과 상태를 묶을 때 알맞습니다.
- `interface`는 필요한 행동의 계약이며 구현 교체와 테스트를 쉽게 합니다.
- `Task<T>`, `async`, `await`는 DB나 큐 같은 I/O를 기다리는 동안 스레드를 붙잡지 않는 비동기 문법입니다.
- LINQ의 `OrderBy`, `ThenBy`, `Count`는 정렬과 집계를 의도에 가깝게 표현합니다.
- `Result<T>`는 성공 값 또는 예상 가능한 오류를 담습니다. `IsSuccess` 확인 뒤 값을 사용합니다.

## 설계를 읽는 방법

실행 흐름은 `Program`(Composition Root) → `PlanPasswordResetsService`(Application Service) → `IResetPolicy`(Strategy)와 Repository 순서입니다. 생성자 주입은 구체 구현이 아닌 인터페이스에 의존하는 DI/DIP 방식입니다.

`ResetRequest`와 `ResetPlanItem`은 Domain Model입니다. record와 읽기 전용 결과는 처리 도중 값이 뜻밖에 바뀌는 일을 줄입니다. Application Service는 사용 사례 순서만 담당해 SRP를 지키고, 새 보안 정책은 Strategy 구현 추가로 확장해 OCP를 따릅니다. 이는 SOLID를 작은 실무 코드에 적용한 예입니다.

처리 대상 없음이나 계획 충돌 같은 예상 가능한 실패는 `Result<T>`로 명시합니다. 반면 취소, DB 연결 장애, 프로그래밍 계약 위반은 예외로 전파해 스택 정보와 장애 신호를 보존합니다. 모든 실패를 예외로 만들면 정상 업무 분기까지 경보가 되고, 모든 예외를 Result로 바꾸면 실제 장애를 숨길 수 있습니다.

없는 계정에도 같은 외부 문구와 비슷한 응답 시간을 사용해야 계정 열거를 어렵게 할 수 있습니다. 운영에서는 사용자·IP별 속도 제한, 해시된 일회용 토큰과 짧은 만료, 요청 ID 고유 제약, 낙관적 동시성으로 중복 발송을 막습니다. 재시도는 멱등해야 하며 DB 저장과 이메일 큐 발행을 함께 보장하려면 Outbox를 고려합니다. `CancellationToken`을 끝까지 전달하고 이메일·토큰·링크는 로그에 남기지 않습니다. 요청량, 차단률, 큐 지연, 발송 실패율을 메트릭과 추적으로 관찰해야 합니다.

## 버전 업데이트 (2026-08-22 확인)

- 안정 학습 기준은 **.NET 10 LTS / C# 14**입니다. 로컬 안정 SDK 10.0.301에서 컴파일되는 기능만 사용했습니다. Microsoft의 2026년 8월 서비스 업데이트는 보안·비보안 수정이 포함된 .NET 10.0.11을 안내하므로 운영 SDK와 런타임의 최신 패치 적용 여부를 확인하세요.
- 최신 미리보기는 **.NET 11 Preview 7 / C# 15**입니다. Preview 7은 labeled `break`/`continue`, union pattern, closed hierarchy 관련 개선 등을 소개합니다. C# 15의 union types, closed hierarchies, collection expression arguments 등은 현재 로컬 안정 SDK에서 컴파일되지 않으므로 실행 코드에서 제외하고 별도 실험 프로젝트에서만 평가해야 합니다.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [2026년 8월 .NET 서비스 업데이트](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-august-2026-servicing-updates/), [.NET 11 Preview 7 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-7/), [C# 14 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 간단 복습 체크리스트

- [ ] `string?`, record, enum, LINQ, `async`/`await`, `Result<T>`를 첫 사용 위치에서 설명할 수 있다.
- [ ] Repository, Strategy, Application Service, Composition Root의 책임을 구분한다.
- [ ] DI와 SOLID가 구현 교체와 테스트 가능성을 어떻게 높이는지 설명한다.
- [ ] 계정 열거 방지, 속도 제한, 멱등성, 동시성, 취소, Outbox, 관측성과 민감정보 로그 금지 이유를 말할 수 있다.
- [ ] 빌드 0경고/0오류, 기본 실행, self-test 4/4를 확인한다.
