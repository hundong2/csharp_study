# 2026-08-10: 로그인 위험도 검토

## 처음 읽는 순서

1. 이 README의 **기초 문법**을 읽습니다.
2. [`Program.cs`](./src/LoginRiskExercise/Program.cs)를 위에서 아래로 실행 흐름대로 읽습니다.
3. `dotnet run`, `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)를 한 문제씩 풉니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)로 말하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260810/src/LoginRiskExercise
dotnet run
dotnet run -- --self-test
```

로컬 안정 SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고도 오류로 처리해 초보자가 null 위험을 일찍 발견하게 했습니다.

## 처음 만나는 기초 문법

- `var score = ...;`: 오른쪽 값으로 변수 형식을 추론하며 `;`로 문장을 끝냅니다.
- `string?`: 문자열이 `null`일 수 있다는 표시이고, `country ?? "미확인"`은 null일 때 기본값을 고릅니다.
- `record`: 주문·로그인처럼 값이 핵심인 데이터를 간결하게 표현하며 값 동등성과 불변 설계에 유리합니다.
- `enum`: `Low`, `Medium`, `High`처럼 가능한 값을 제한해 잘못된 문자열을 막습니다.
- `if`, 삼항 연산자 `조건 ? 참 : 거짓`: 조건에 따라 실행이나 값을 선택합니다.
- `Task<T>`, `async`, `await`: I/O가 끝날 때 스레드를 붙잡지 않고 기다리는 비동기 문법입니다.
- `interface`: 필요한 행동의 계약입니다. 구현을 바꿔 끼울 수 있어 DI와 테스트 대역이 쉬워집니다.

## 설계 지도를 읽는 법

`Program`(Composition Root) → `ReviewLoginService`(Application Service) → `ILoginRepository`(조회) + `IRiskStrategy`(Domain Model의 정책) 순입니다. 생성자 매개변수로 의존성을 주입하는 DI는 상위 정책이 구체 구현에 의존하지 않게 하는 DIP를 돕습니다. 점수 책임을 Strategy로 분리한 것은 SRP, 새 정책을 기존 서비스 수정 없이 추가하는 것은 OCP의 작은 예입니다.

LINQ는 성공한 평가를 골라 점수순으로 정렬하는 데이터 흐름을 표현합니다. record와 읽기 전용 인터페이스는 변경 지점을 줄입니다. 예상 가능한 입력 오류는 `Result<T>`로, 저장소 장애나 취소 같은 비정상 흐름은 예외로 구분합니다. 운영 코드라면 구조화 로그, 추적 ID, 메트릭, 타임아웃, 개인정보 마스킹을 추가해야 하며 재시도는 멱등성과 과부하를 고려해 경계에서 제한해야 합니다.

## 버전 업데이트 (2026-08-10 확인)

- 최신 안정판은 **.NET 10.0.10(LTS), SDK 10.0.302**이고 C# 14를 지원합니다. 이 예제는 설치된 안정 SDK 10.0.301에서도 동작하는 문법만 사용합니다.
- 최신 미리보기는 **.NET 11 Preview 6 / C# 15**입니다. C# 15의 collection-expression arguments, union types, closed hierarchies 등은 미리보기 SDK가 필요하므로 오늘 실행 코드에는 넣지 않았습니다.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 다운로드](https://dotnet.microsoft.com/en-us/download), [C# 15 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15), [.NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

## 완료 기준

빌드 경고 0개, self-test 4/4, 그리고 각 패턴을 “무엇”뿐 아니라 “왜” 쓰는지 설명하면 완료입니다.
