# 2026-07-24 C# 택배 배차 실습

작은 택배 배차 프로그램을 실행하면서 기초 문법부터 실무에서 자주 쓰는 책임 분리와 운영 안전성까지 연결합니다.

## 초보자 읽기 순서

1. 아래 명령으로 일반 실행과 자동 검증을 먼저 통과시킵니다.
2. `Program.cs`의 맨 위 실행부와 `DispatchRequest`, `Parcel`을 읽습니다.
3. `Result<T>` → Strategy → Repository → `DispatchService` 순서로 읽습니다.
4. `EXERCISES.md`의 1단계부터 수정하고 매번 `--self-test`를 실행합니다.
5. 마지막에 `CHECKPOINT.md`를 코드 없이 답합니다.

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260724
dotnet run --project .\src\ParcelDispatchExercise
dotnet run --project .\src\ParcelDispatchExercise -- --self-test
```

`초보자 검증 통과 (4/4)`가 나오면 시작 코드가 정상입니다.

## 처음 만나는 기본 문법

| 문법 | 첫 사용 | 뜻과 이유 |
| --- | --- | --- |
| 변수와 타입 | `var services`, `decimal WeightKg` | 변수는 값을 이름으로 보관합니다. `var`도 컴파일 시 타입이 고정되고, 돈·무게의 소수 계산에는 `decimal`을 사용했습니다. |
| 조건과 반복 | `if`, 삼항 연산자, `foreach` | 조건에 따라 흐름을 나누고 여러 요청을 순회합니다. |
| 메서드와 반환값 | `Create`, `Quote` | 입력을 받아 한 책임을 수행하고 결과를 반환합니다. |
| nullable | `string?`, `?.`, `??`, `!` | 값이 없을 가능성을 타입에 표시합니다. `!`는 검증 뒤 null이 아님을 컴파일러에 알리므로 남용하지 않습니다. |
| record와 불변성 | `record`, `init`, `required` | 값 중심 데이터를 쉽게 비교하고 생성 후 우발적 변경을 줄입니다. |
| 컬렉션과 LINQ | 배열, `Any`, `Single` | 여러 값을 보관하고 조건에 맞는 값의 존재 또는 유일성을 선언적으로 검사합니다. |
| async/await | `Task`, `async`, `await` | DB·네트워크 대기 중 스레드를 묶어 두지 않습니다. `CancellationToken`으로 중단 요청도 전달합니다. |

## 실무 구조 지도

```text
Program / Composition Root
          └─ DispatchService (Application Service)
              ├─ Parcel (Domain Model)
              ├─ IShippingStrategy (Strategy)
              └─ IParcelRepository (Repository)
```

- `Parcel`은 유효한 상태만 만들어지게 책임집니다. 이것이 빈 데이터 객체보다 강한 Domain Model입니다.
- `DispatchService`는 유스케이스 순서만 조정하는 Application Service입니다. SRP에 맞게 계산과 저장 구현을 분리했습니다.
- Strategy와 Repository 인터페이스에 의존하는 DIP, 새 배송 정책을 추가하는 OCP로 SOLID의 핵심을 연습합니다.
- Composition Root에서만 구현을 선택해 주입합니다. 실제 ASP.NET Core에서는 DI 컨테이너 등록 코드가 이 역할을 합니다.
- 메모리 Repository를 주입하면 DB 없이 빠르고 결정적인 테스트가 가능하므로 testability가 좋아집니다.
- 예상 가능한 입력·중복 실패는 `Result<T>`로 반환합니다. 연결 장애, 취소, 버그처럼 정상 흐름으로 복구하기 어려운 실패는 예외로 전달해 중앙 로깅·재시도 정책이 처리하게 합니다.

실서비스에서는 중복 확인과 저장 사이의 경쟁 조건을 DB unique 제약과 트랜잭션으로 막아야 합니다. 요청 ID를 구조화 로그와 trace에 남기되 배송 메모 같은 개인정보는 제외하고, 성공률·지연 시간·실패 유형 메트릭을 관찰합니다. 외부 택배사 호출에는 timeout, 제한된 재시도, 멱등성 키를 적용하고 시간은 UTC로 저장합니다.

## 실습 자료

- [단계별 실습](./EXERCISES.md)
- [초보자 확인 단계](./CHECKPOINT.md)
- [실행 코드](./src/ParcelDispatchExercise/Program.cs)

## 버전 업데이트 (2026-07-24 확인)

- 로컬 안정 SDK는 `.NET SDK 10.0.301`, 런타임은 `10.0.9`입니다. 예제는 `net10.0`, nullable 활성화, 경고를 오류로 처리하며 C# 14로 빌드합니다.
- Microsoft의 [.NET 10 새로운 기능](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)에 따르면 .NET 10은 3년 지원되는 LTS 안정 릴리스입니다.
- [C# 14 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 .NET 10에서 지원되는 최신 안정 언어이며 extension members, null 조건부 대입 등을 포함한다고 설명합니다.
- 공식 [.NET 11 Preview 5 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-5/)는 2026-06-09 공개된 미리 보기이며 C#의 closed class hierarchies와 union 선언 등을 소개합니다. 로컬에는 .NET 11 SDK가 없으므로 프리뷰 기능을 넣지 않았습니다.

## 짧은 복습 체크리스트

- [ ] nullable 표시와 null 처리 연산자의 목적을 설명할 수 있다.
- [ ] record, LINQ, async/await를 각각 왜 썼는지 말할 수 있다.
- [ ] Result와 예외를 구분하는 기준을 말할 수 있다.
- [ ] Domain Model, Application Service, Repository, Strategy, DI, Composition Root를 연결할 수 있다.
- [ ] SRP·OCP·DIP가 테스트 가능성에 어떤 도움을 주는지 설명할 수 있다.
- [ ] 중복, 취소, timeout, UTC, 로그·메트릭 등 운영 보완점을 말할 수 있다.
