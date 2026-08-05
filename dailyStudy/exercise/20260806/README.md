# 2026-08-06: 재고 발주 제안기로 배우는 C#과 .NET 설계

## 초보자 읽기 순서

1. 아래 **실행하기**를 그대로 따라 정상 출력을 본다.
2. `Program.cs`의 맨 위부터 `StockItem`, `Result<T>`, 인터페이스 순서로 읽는다.
3. `TargetLevelReorderPolicy`에서 한 상품의 발주 판단을 추적한다.
4. `CreateReorderPlanService`에서 조회 → 판단 → 알림 흐름을 읽는다.
5. `--self-test`를 실행한 뒤 실습 1부터 한 단계씩 바꾼다.

처음에는 패턴 이름을 외우지 않아도 됩니다. **데이터**, **업무 규칙**, **작업 순서**, **외부 연결**이 왜 분리되었는지만 확인하세요.

## 오늘 만들 것

현재 재고가 발주 기준 이하인 활성 상품을 찾아 목표 재고까지 몇 개를 주문해야 하는지 계산합니다. 실제 업무에서 흔한 Application Service, Domain Model, Repository, Strategy, 의존성 주입(DI), Composition Root 구조를 작은 콘솔 앱으로 연습합니다.

## 실행하기

설치된 안정 SDK(.NET 10.0.301)에 맞춰 `net10.0`을 대상으로 합니다.

```powershell
cd dailyStudy/exercise/20260806/src/StockReorderExercise
dotnet run
dotnet run -- --self-test
```

예상 핵심 출력:

```text
PEN-01 검은 펜: 17개 발주
발주 제안 1건, 총 17개
self-test: 4/4 통과
```

## 첫 문법 안내

- `var quantity = ...;`: 오른쪽 값에서 지역 변수 타입을 컴파일러가 추론합니다. 정적 타입은 그대로 유지됩니다.
- `new StockItem(...)`: `new`는 객체를 만듭니다. 대상 타입이 분명한 곳의 `new(...)`는 타입 이름을 생략한 문법입니다.
- `string? Name`: `?`는 null 가능성을 타입에 표시합니다. `string.IsNullOrWhiteSpace` 검사 뒤에 안전하게 사용합니다.
- `if (...)`: 조건이 참일 때 블록을 실행합니다. `!`, `||`, `<=`는 각각 부정, 또는, 이하입니다.
- `record`: 값이 같은지를 비교하기 좋은 데이터 모델입니다. 변경을 줄이면 추적과 동시성 처리가 쉬워집니다.
- `interface`: 구현이 지켜야 할 계약입니다. `IStockRepository`를 메모리나 DB 구현으로 교체할 수 있습니다.
- `Task<T>`와 `await`: 비동기 작업의 완료를 기다립니다. 반환 타입 `T`는 완료 후 얻는 값입니다.
- `IReadOnlyList<T>`와 `Result<T>`: `<T>`는 여러 타입에 재사용하는 제네릭 문법입니다.
- 문자열 `$"{value}"`: 보간 문자열로 값을 읽기 좋게 삽입합니다.

## 설계 지도

```text
Program (Composition Root)
  └─ CreateReorderPlanService (Application Service)
       ├─ IStockRepository → InMemoryStockRepository
       ├─ IReorderPolicy   → TargetLevelReorderPolicy (Strategy)
       └─ IReorderNotifier → ConsoleReorderNotifier
```

- **Domain Model**: `StockItem`, `ReorderProposal`이 업무 데이터를 표현합니다.
- **Repository**: 저장소 접근 계약을 분리해 DB 없이 규칙과 흐름을 테스트합니다.
- **Strategy**: 발주 정책을 교체 가능하게 만듭니다.
- **Application Service**: 업무 유스케이스 순서만 조정합니다.
- **Composition Root**: 시작점 한 곳에서 구현을 조립합니다.
- **DI와 DIP**: 상위 업무 로직이 구체 구현이 아닌 인터페이스에 의존합니다.
- **SOLID**: 클래스별 변경 이유를 좁히고(SRP), 새 정책을 기존 서비스 변경 없이 추가합니다(OCP).

## 중급·고급 선택의 이유

### nullable 안전성과 불변 record

외부 데이터에는 이름 누락이 있을 수 있으므로 `string?`로 현실을 숨기지 않습니다. record와 읽기 전용 컬렉션은 처리 도중 값이 뜻밖에 바뀌는 범위를 줄입니다. 다만 record가 참조하는 컬렉션까지 자동으로 불변이 되는 것은 아닙니다.

### LINQ

`Select → Where → Select → OrderByDescending`은 판단, 성공값 선택, 정렬의 의도를 드러냅니다. LINQ는 지연 실행될 수 있으므로 `ToArray()`에서 한 번 확정합니다. 매우 큰 데이터는 DB 쿼리나 스트리밍으로 필터링해 메모리 사용을 제한해야 합니다.

### async/await와 취소

저장소와 알림은 실제 환경에서 DB·네트워크 I/O가 됩니다. `CancellationToken`을 끝까지 전달하면 종료나 요청 취소에 빨리 반응할 수 있습니다. `OperationCanceledException`은 실패 메시지로 바꾸지 않고 다시 던집니다.

### 예외와 Result

“발주 대상 아님”, “상품명 누락”처럼 예상되는 업무 결과는 `Result<T>`가 알맞습니다. 연결 단절처럼 정상 흐름으로 처리하기 어려운 기술 장애는 예외를 사용하고 Application Service 경계에서 번역합니다. 운영 로그에는 예외 전체를 남기되 사용자에게 내부 정보를 노출하지 않습니다.

### 테스트 가능성과 운영 관심사

인터페이스와 생성자 주입 덕분에 메모리 저장소와 수집용 알림기로 빠른 테스트가 가능합니다. 운영에서는 구조화 로그, 메트릭(대상 수·실패 수·처리 시간), 타임아웃, 재시도와 지수 백오프, 중복 알림 방지 키, 트레이싱을 추가해야 합니다. 재시도는 멱등성이 확인된 작업에만 적용합니다.

## 단계별 실습

1. **초급 검증**: `PEN-01`의 현재 재고를 11로 바꾸고 발주 제안이 0건인지 확인합니다.
2. **문법**: `Where(proposal => proposal.Quantity >= 10)`을 적절한 위치에 추가해 10개 이상만 알리세요.
3. **nullable**: 상품명이 빈 문자열인 데이터를 추가하고 정책이 실패를 반환하는지 self-test를 추가하세요.
4. **Strategy**: 목표 재고가 아니라 고정 10개를 주문하는 `FixedQuantityReorderPolicy`를 구현해 Composition Root에서 교체하세요.
5. **Repository**: JSON 파일을 읽는 저장소를 만들되 인터페이스와 서비스는 수정하지 마세요.
6. **운영 설계**: 같은 SKU를 두 번 알리지 않도록 멱등성 키 저장 위치와 실패 시 재시도 규칙을 글로 정리하세요.

## 초보자 검증 단계

```powershell
dotnet build
dotnet run
dotnet run -- --self-test
```

- 빌드가 경고 0개, 오류 0개인가?
- 일반 실행에서 `PEN-01` 한 건과 17개가 출력되는가?
- self-test가 `4/4 통과`인가?
- 실습 후 실패한다면 첫 오류 한 개의 파일명과 줄 번호부터 읽었는가?

## 버전 업데이트 (2026-08-06 확인)

- **안정 실행 기준**: C# 14는 .NET 10에서 지원되는 최신 안정 C#이며, .NET 10은 3년 지원되는 LTS입니다. 이 예제는 로컬 안정 SDK 10.0.301로 실제 컴파일합니다.
- **미리 보기 분리**: .NET 11과 C# 15 기능(예: union declarations/patterns)은 preview이므로 오늘 실행 코드에는 넣지 않았습니다. preview SDK를 별도 설치한 실험 프로젝트에서만 사용하세요.
- 공식 자료: [C# 14의 새로운 기능](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14), [.NET 10의 새로운 기능](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/overview), [.NET 11 Preview 6](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)

## 오늘의 짧은 복습 체크리스트

- [ ] nullable 표기가 왜 필요한지 말할 수 있다.
- [ ] record와 class의 사용 의도를 구분할 수 있다.
- [ ] LINQ가 언제 실행되는지 설명할 수 있다.
- [ ] 예상 업무 실패는 Result, 기술 장애는 예외로 나눈 이유를 말할 수 있다.
- [ ] Repository, Strategy, Application Service, Composition Root의 역할을 짚을 수 있다.
- [ ] DI와 SOLID가 테스트 가능성을 어떻게 높이는지 설명할 수 있다.
- [ ] async 작업에 취소·로그·메트릭·멱등성이 필요한 이유를 말할 수 있다.

소스: [`src/StockReorderExercise/Program.cs`](./src/StockReorderExercise/Program.cs)
