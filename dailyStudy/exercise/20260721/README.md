# 2026-07-21 C# 회의실 예약 실습

회의실 예약 프로그램을 실행하며 C# 기본 문법과 실무 설계를 함께 익히는 입문 자료입니다. 읽기 15분, 실행 10분, 수정 실습 30분을 권장합니다.

## 초보자 읽기 순서

1. 아래 명령으로 일반 실행과 검증을 먼저 통과시킵니다.
2. `Program.cs` 맨 위부터 `[기초]` 주석과 실행 흐름을 읽습니다.
3. record와 Result를 읽고, “데이터”와 “성공/실패”를 어떻게 표현하는지 확인합니다.
4. Strategy → Repository → Application Service → Composition Root 순서로 책임 분리를 따라갑니다.
5. `EXERCISES.md` 1~3단계를 직접 수정하고 `CHECKPOINT.md`로 복습합니다.

## 실행

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260721
dotnet run --project .\src\MeetingRoomExercise
dotnet run --project .\src\MeetingRoomExercise -- --self-test
```

마지막 줄이 `초보자 검증 통과 (4/4)`이면 시작 코드가 정상입니다.

## 처음 만나는 문법

| 문법 | 첫 사용 | 의미 |
| --- | --- | --- |
| 변수와 타입 | `var service`, `int Id`, `bool Passed` | 값에 이름과 타입을 부여합니다. `var`도 컴파일 시 타입이 정해집니다. |
| nullable | `string?`, `room is null`, `??` | 값이 없을 가능성을 타입에 표시하고 명시적으로 처리합니다. |
| 조건/패턴 | `if`, `switch`, `is < 15 or > 480` | 입력이나 결과의 상태에 따라 분기합니다. |
| 컬렉션/반복 | `List<T>`, 배열, `foreach` | 같은 종류의 여러 값을 저장하고 순회합니다. |
| 람다/LINQ | `Where(room => ...)`, `OrderBy`, `Any` | 컬렉션을 선언적으로 필터링·정렬·검사합니다. |
| 비동기 | `Task`, `async`, `await` | DB·네트워크 같은 대기 작업을 비동기 계약으로 표현합니다. |

## 실무 설계 지도

```text
Program(입출력)
  → ReservationService(Application Service: 유스케이스 조정)
      → IRoomSelectionStrategy(Strategy: 방 선택 규칙)
      → IReservationRepository(Repository: 저장 계약)
          → InMemoryReservationRepository(현재 저장 구현)
CompositionRoot가 위 객체를 선택하고 연결
```

- nullable 안전성은 “값 없음”을 숨기지 않고 `string?`, `MeetingRoom?`로 드러냅니다.
- record와 `init` 성격의 생성자 값은 값 객체를 불변에 가깝게 만들어 추론과 테스트를 쉽게 합니다.
- Result는 “조건에 맞는 방 없음” 같은 예상 가능한 업무 실패에, 예외는 잘못된 null 인수나 취소 같은 비정상 흐름에 사용합니다.
- Repository는 저장소 교체 가능성을, Strategy는 정책 교체 가능성을 제공합니다. 두 인터페이스는 의존성 역전 원칙(DIP)을 적용합니다.
- Application Service는 한 유스케이스만 조정하고, 방 선택과 저장 책임은 각각 위임합니다. 이는 단일 책임 원칙(SRP)과 개방-폐쇄 원칙(OCP)에 가깝습니다.
- 의존성 주입과 Composition Root 덕분에 서비스 테스트에서 메모리 저장소나 가짜 정책을 넣을 수 있습니다.

작은 콘솔 예제라 생략했지만 운영에서는 동시 예약을 막는 DB 트랜잭션/고유 제약, UTC와 시간대, 구조화 로그, 메트릭, 재시도 정책, 영속 저장소, 비밀 관리가 필요합니다. 특히 “조회 후 저장” 사이 경쟁 조건은 메모리 코드만으로 해결되지 않습니다.

## 실습과 검증

- [EXERCISES.md](./EXERCISES.md): 문법 수정부터 정책 교체와 운영 안전성까지 5단계
- [CHECKPOINT.md](./CHECKPOINT.md): 코드를 보지 않고 답하는 초보자 검증
- [Program.cs](./src/MeetingRoomExercise/Program.cs): 실행 예제와 4개 자체 검증

## 버전 업데이트 (2026-07-21 확인)

- 로컬 안정 SDK는 `.NET SDK 10.0.301`, 런타임은 `10.0.9`이며 이 예제는 `net10.0`과 기본 C# 14로 빌드합니다.
- [.NET 10 발표](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)에 따르면 .NET 10은 2025-11-11 출시된 LTS이며 2028-11-10까지 지원됩니다.
- [C# 14의 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 최신 안정 릴리스이며 .NET 10에서 지원된다고 설명합니다. 확장 멤버, null 조건부 대입, `field` 기반 속성 등이 포함됩니다.
- [.NET 10의 새로운 기능](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)에는 런타임, 라이브러리, SDK 개선이 정리되어 있습니다.
- 현재 설치된 안정 SDK보다 새로운 미리 보기 언어/SDK 기능은 이 실행 예제에 넣지 않았습니다. 미리 보기 기능은 별도 프로젝트와 명시적 `LangVersion`으로 격리해 시험하세요.

## 짧은 복습 체크리스트

- [ ] 일반 실행과 `--self-test`가 모두 성공한다.
- [ ] nullable, record, LINQ, async/await의 코드 위치와 이유를 말할 수 있다.
- [ ] 예상 가능한 실패에 Result, 비정상 흐름에 예외를 쓰는 차이를 설명한다.
- [ ] Application Service, Repository, Strategy, DI, Composition Root의 책임을 한 문장씩 말한다.
- [ ] 동시 예약을 실제 운영에서 DB 제약/트랜잭션으로 막아야 하는 이유를 설명한다.
