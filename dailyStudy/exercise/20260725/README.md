# 2026-07-25 C# 장애 알림 라우팅 실습

작은 장애 알림 프로그램을 실행하면서 C# 기초 문법부터 실무에서 자주 쓰는 책임 분리와 운영 안전성까지 연결합니다.

## 초보자 읽기 순서

1. 아래 명령으로 일반 실행과 자동 검증을 먼저 통과시킵니다.
2. `Program.cs`의 맨 위 실행부 → 요청 record → `Incident.Create` 순서로 읽습니다.
3. `Result<T>` → Strategy → Repository → Application Service → Composition Root 순서로 읽습니다.
4. `EXERCISES.md`를 1단계부터 수정하고 매번 `--self-test`를 실행합니다.
5. 마지막에 `CHECKPOINT.md`를 코드 없이 답합니다.

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260725
dotnet run --project .\src\IncidentRoutingExercise
dotnet run --project .\src\IncidentRoutingExercise -- --self-test
```

`초보자 검증 통과 (4/4)`가 나오면 시작 코드가 정상입니다.

## 처음 만나는 기본 문법

| 문법 | 첫 사용 | 무엇이며 왜 쓰는가 |
| --- | --- | --- |
| 변수와 타입 | `var service`, `Severity` | 변수는 값에 이름을 붙입니다. `var`도 컴파일 시 타입이 고정되며, enum은 허용 값을 제한합니다. |
| 조건과 반복 | `if`, `foreach` | 조건에 따라 분기하고 여러 요청을 하나씩 처리합니다. |
| 메서드와 반환값 | `Create`, `GetSummaryAsync` | 입력을 받아 한 책임을 수행하고 호출자에게 결과를 돌려줍니다. |
| nullable | `string?`, `??` | 값이 없을 수 있음을 타입에 표시하고 기본값을 명시합니다. 무분별한 `!` 대신 실제 검증을 합니다. |
| record와 불변성 | `record`, `init`, `required`, `with` | 값 중심 데이터를 쉽게 비교하고, 생성 뒤 직접 변경하지 않아 상태 추적을 단순하게 합니다. |
| 컬렉션과 LINQ | 배열, `Any`, `First`, `Count` | 여러 값을 저장하고 “존재/선택/개수” 의도를 반복문보다 선명하게 표현합니다. |
| async/await | `Task`, `async`, `await` | DB·네트워크 대기 중 스레드를 붙잡지 않습니다. `CancellationToken`으로 취소를 전달합니다. |

## 실무 구조 지도

```text
Program / Composition Root
          └─ IncidentApplicationService (Application Service)
              ├─ Incident (Domain Model)
              ├─ IRoutingStrategy (Strategy)
              ├─ IIncidentRepository (Repository)
              └─ INotifier
```

- Domain Model은 유효한 장애만 만들고 상태 규칙을 지킵니다. Application Service는 생성→중복 확인→라우팅→저장→알림이라는 유스케이스 순서만 조정합니다.
- Repository는 저장 기술을, Strategy는 변하는 라우팅 정책을 감춥니다. 생성자 주입(DI)으로 인터페이스에 의존하므로 메모리 구현이나 테스트 대역으로 교체하기 쉽습니다.
- SRP는 모델·정책·저장·알림의 책임 분리, OCP는 Strategy 추가, DIP는 Application Service가 구체 구현이 아닌 인터페이스에 의존하는 부분에 나타납니다.
- Composition Root만 구체 구현을 선택합니다. 실제 ASP.NET Core에서는 내장 DI 컨테이너 등록 코드가 이 역할을 맡습니다.
- 예상 가능한 입력·중복 실패는 `Result<T>`로 호출자가 분기하게 합니다. 취소, 네트워크 장애, 불변 조건 위반 같은 예상 밖 실패는 예외로 전달하고 중앙에서 로깅·재시도 정책을 적용합니다.
- 불변 record와 메모리 Repository를 사용해 테스트가 빠르고 결정적입니다. 외부 알림도 `INotifier` 대역으로 바꾸어 성공 호출 횟수를 검증합니다.

운영에서는 중복 확인과 저장을 DB unique 제약·트랜잭션으로 원자화해야 합니다. 알림에는 timeout, 지수 backoff와 최대 횟수가 있는 재시도, 회로 차단을 적용하고, 저장 후 전송 유실은 outbox로 보완합니다. 로그에는 correlation ID와 실패 코드를 구조화해 남기되 민감 정보는 제외하고, 성공률·지연 시간·실패 유형을 메트릭과 trace로 관찰합니다. 시간은 UTC로 저장하고 화면에서만 지역 시간으로 변환합니다.

## 실습 자료

- [단계별 실습](./EXERCISES.md)
- [초보자 확인 단계](./CHECKPOINT.md)
- [실행 코드](./src/IncidentRoutingExercise/Program.cs)

## 버전 업데이트 (2026-07-25 확인)

- 로컬 안정 SDK는 `.NET SDK 10.0.301`이며 예제는 `net10.0`, nullable 활성화, 경고를 오류로 처리하고 C# 14로 빌드합니다.
- Microsoft의 [.NET 10 새로운 기능](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)에 따르면 .NET 10은 3년 지원되는 LTS 안정 릴리스입니다.
- [C# 14 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 .NET 10에서 지원되는 최신 안정 언어이며 extension members, null 조건부 할당, `field` 지원 등을 포함한다고 설명합니다.
- 공식 [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)는 2026-07-14 공개된 미리 보기로 C# extension indexer, union 지원 타입, 비동기 DataAnnotations 검증 등을 소개합니다. 로컬에는 .NET 11 SDK가 없고 미리 보기 기능은 변경될 수 있으므로 오늘 실행 코드에는 넣지 않았습니다.

## 5분 복습 체크리스트

- [ ] nullable 표기와 null 처리 연산자의 목적을 설명할 수 있다.
- [ ] record, 불변성, LINQ, async/await를 각각 왜 쓰는지 말할 수 있다.
- [ ] Result와 예외를 구분하는 기준을 말할 수 있다.
- [ ] Domain Model, Application Service, Repository, Strategy, DI, Composition Root를 연결할 수 있다.
- [ ] SRP·OCP·DIP가 테스트 가능성에 어떤 도움을 주는지 설명할 수 있다.
- [ ] 중복, 취소, timeout, 재시도, outbox, UTC, 로그·메트릭 같은 운영 보완점을 말할 수 있다.
