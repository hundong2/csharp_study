# 2026-07-27 C# 재고 예약 실습

## 초보자 읽기 순서

1. 아래 명령으로 일반 실행과 자동 검증을 먼저 통과합니다.
2. `Program.cs`를 위에서부터 실행부 → 기본 타입과 `record` → `Result` → Domain Model → Strategy → Repository → Application Service 순으로 읽습니다.
3. `EXERCISES.md`의 1단계부터 코드를 바꾸고 매번 다시 검증합니다.
4. 마지막에 `CHECKPOINT.md`를 코드 없이 답합니다.

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260727
dotnet run --project .\src\InventoryReservationExercise
dotnet run --project .\src\InventoryReservationExercise -- --self-test
```

## 처음 만나는 기본 문법

| 문법 | 첫 사용 예 | 의미와 이유 |
| --- | --- | --- |
| 변수와 생성 | `var service = new ...` | 값을 이름으로 보관합니다. `var`여도 실제 타입은 컴파일 때 고정됩니다. |
| 배열과 반복 | `new[]`, `foreach` | 여러 요청을 모으고 하나씩 처리합니다. |
| 조건과 표현식 | `if`, `?:`, `switch` 대신 enum | 결과에 따라 실행을 나누며 상태를 제한합니다. |
| 클래스와 메서드 | `InventoryItem.Reserve` | 데이터와 그 데이터를 지키는 규칙을 함께 둡니다. |
| nullable | `InventoryItem?`, `is null`, `!` | 값이 없을 가능성을 타입으로 표시하고 사용 전에 검사합니다. `!`는 이미 검사된 값에만 씁니다. |
| record와 불변 데이터 | `ReservationRequest` | 값 중심 메시지를 간결하게 만들고 생성 후 변경을 제한합니다. |
| LINQ | `Where`, `OrderBy`, `Select` | 컬렉션의 필터·정렬·변환 의도를 단계별로 표현합니다. |
| async/await | `Task`, `await` | DB·네트워크 대기 동안 스레드를 붙잡지 않고 취소 신호를 전달합니다. |

## 실무 구조와 설계 선택

```text
Program (Composition Root: 구현을 한곳에서 조립)
  └─ ReservationApplicationService (사용 사례 순서)
      ├─ InventoryItem (Domain Model: 재고 규칙)
      ├─ IReservationPolicy (Strategy: 교체 가능한 제한 정책)
      ├─ IInventoryRepository (저장소 경계)
      └─ IAuditLog (운영 관찰 경계)
```

- 생성자 DI로 인터페이스를 주입합니다. 서비스는 구체 저장소를 몰라 가짜 구현으로 테스트할 수 있으며 DIP를 지킵니다.
- Domain Model은 재고 음수 방지, Application Service는 작업 순서, Strategy는 변경되는 정책을 맡습니다. 이는 SRP와 OCP를 적용한 것입니다.
- 재고 부족·없는 상품처럼 예상 가능한 실패는 `Result`로, 취소·저장소 장애·프로그래밍 오류처럼 중단과 로깅이 필요한 실패는 예외로 다룹니다.
- 실서비스에서는 SKU별 동시 예약을 트랜잭션·낙관적 동시성으로 보호하고 주문 ID를 unique key로 만들어 재시도 중복을 막습니다. timeout, 지수 backoff, outbox, UTC, correlation ID, 구조화 로그, 재고 부족률·지연 시간 메트릭과 trace도 필요합니다.

## 실습 자료

- [단계별 실습](./EXERCISES.md)
- [초보자 검증 단계](./CHECKPOINT.md)
- [실행 코드](./src/InventoryReservationExercise/Program.cs)

## 버전 업데이트 (2026-07-27 확인)

- 로컬 안정 SDK는 `.NET SDK 10.0.301`이며 예제는 `net10.0`, nullable 활성화, 경고를 오류로 처리하고 C# 14로 빌드합니다.
- Microsoft의 [.NET 10 새로운 기능](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)에 따르면 .NET 10은 3년 지원되는 LTS 안정 릴리스입니다.
- [C# 14 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)은 C# 14가 .NET 10에서 지원되는 최신 안정 언어이며 extension members, null 조건부 할당, `field` 지원 등을 포함한다고 설명합니다.
- 공식 [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)는 2026-07-14 공개된 미리 보기입니다. extension indexers, union 지원 타입, async 검증 등이 포함되지만 로컬에 .NET 11 SDK가 없고 API가 바뀔 수 있어 오늘 실행 코드에는 넣지 않았습니다.

## 5분 복습 체크리스트

- [ ] nullable 검사와 record 불변성의 목적을 설명한다.
- [ ] LINQ와 async/await, `CancellationToken`의 역할을 설명한다.
- [ ] Result와 예외의 선택 기준을 말한다.
- [ ] Application Service, Domain Model, Repository, Strategy, DI, Composition Root를 연결한다.
- [ ] SRP·OCP·DIP가 테스트 가능성을 높이는 이유를 말한다.
- [ ] 동시성, 멱등성, timeout, 재시도, outbox, UTC, 로그·메트릭·trace를 말한다.
