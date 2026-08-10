# 2026-08-11: 직원 접근 권한 프로비저닝

## 처음 읽는 순서

1. 아래 **기초 문법**을 먼저 읽습니다.
2. [`Program.cs`](./src/AccessProvisioningExercise/Program.cs)를 위에서 아래로 읽으며 실행 흐름을 따라갑니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 작은 변경을 직접 해봅니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md) 질문에 말로 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260811/src/AccessProvisioningExercise
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고를 오류로 처리하여 초보자가 null 위험을 일찍 발견하게 했습니다.

## 처음 만나는 기초 문법

- `var service = ...;`: 오른쪽 값으로 변수 형식을 추론하며 `;`로 문장을 끝냅니다.
- `string?`: 문자열이 `null`일 수 있음을 표시합니다. `value?.Count ?? 0`은 안전하게 접근하고 기본값을 정합니다.
- `record`: 직원처럼 값이 핵심인 데이터를 불변에 가깝게 표현하며 값 동등성을 제공합니다.
- `enum`: 부서처럼 가능한 값이 제한된 경우 오타가 잦은 문자열 대신 씁니다.
- `if`와 `조건 ? 참 : 거짓`: 조건에 따라 실행 경로나 값을 선택합니다.
- 배열 `[]`, `foreach`: 여러 값을 담고 하나씩 반복합니다.
- `interface`: 필요한 행동의 계약입니다. 구현을 바꾸기 쉬워 DI와 테스트에 유리합니다.
- `Task<T>`, `async`, `await`: DB나 네트워크 I/O를 기다리는 동안 스레드를 붙잡지 않는 비동기 문법입니다.

## 설계를 읽는 방법

`Program`(Composition Root) → `ProvisionAccessService`(Application Service) → `IEmployeeRepository`(조회) + `IAccessPolicy`(Domain Model의 정책/Strategy) + `IAccessGateway`(외부 IAM) 순서입니다. 생성자 매개변수로 의존성을 넣는 DI와 구체 구현보다 인터페이스에 의존하는 DIP를 사용했습니다. 유스케이스 조정, 권한 정책, 저장/외부 호출을 분리한 것은 SRP이며 새 정책을 서비스 수정 없이 추가하기 쉬운 구조는 OCP입니다.

LINQ는 필요한 권한에서 이미 가진 권한을 제외하는 데이터 흐름을 표현합니다. `record`와 읽기 전용 집합은 변경 지점을 줄여 테스트와 동시성 추론을 쉽게 합니다. 잘못된 ID 같은 예상 가능한 실패는 `Result<T>`로, 네트워크 장애·취소 같은 비정상 실패는 예외로 구분합니다. 운영 환경에서는 구조화 로그와 추적 ID, 메트릭, 시간 제한, 제한된 재시도, 감사 로그를 더해야 합니다. 권한 부여 API에는 멱등성 키나 현재 상태 확인을 사용해 재시도 시 중복 부여를 막아야 합니다.

## 버전 업데이트 (2026-08-11 확인)

- 최신 안정판은 **.NET 10.0.10(LTS), SDK 10.0.302, C# 14**입니다. 이 예제는 로컬의 안정 SDK 10.0.301에서 컴파일되는 기능만 사용합니다.
- 최신 미리보기는 **.NET 11 Preview 6 / C# 15**입니다. C# 15의 collection-expression arguments, union types, closed hierarchies, extension indexers, memory safety 변경은 미리보기 SDK가 필요하므로 실행 코드에 넣지 않았습니다.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 다운로드](https://dotnet.microsoft.com/en-us/download), [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/), [C# 15 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 완료 기준

빌드 경고 0개, self-test 4/4, 그리고 Repository·Strategy·Application Service를 각자 한 문장으로 설명할 수 있으면 완료입니다.
