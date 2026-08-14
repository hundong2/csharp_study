# 2026-08-15: 주문 출고 준비와 운송사 선택

## 처음 읽는 순서

1. 아래 **처음 만나는 기본 문법**을 먼저 읽습니다.
2. [`Program.cs`](./src/ShipmentPreparationExercise/Program.cs)를 위에서 아래로 읽으며 실행 흐름을 따라갑니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 작은 변경을 직접 구현합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 말로 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260815/src/ShipmentPreparationExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고를 오류로 처리해 null 위험을 컴파일 단계에서 찾습니다.

## 처음 만나는 기본 문법

- `var orders = new[] { ... };`: 오른쪽 값으로 변수 형식을 추론하고 여러 값을 배열에 담습니다. C# 문장은 보통 `;`로 끝납니다.
- `string?`: 문자열이 `null`일 수 있다는 뜻입니다. `IsNullOrWhiteSpace`로 null·빈 문자열·공백을 함께 검사합니다.
- `decimal`과 `2.2m`: 금액처럼 정밀도가 중요한 값에 쓰며 `m`은 decimal 리터럴 표시입니다.
- `if`, `foreach`, `continue`, `return`: 조건 분기, 반복, 다음 반복으로 이동, 현재 메서드 종료를 뜻합니다.
- `enum`: 가능한 선택지를 제한된 이름으로 표현합니다.
- `record`: 값 중심 데이터를 간결하고 불변에 가깝게 표현합니다.
- `interface`: 필요한 동작의 계약입니다. 구현 교체가 쉬워 DI와 테스트에 유리합니다.
- `Task<T>`, `async`, `await`: DB 같은 I/O를 기다리는 동안 스레드를 붙잡지 않는 비동기 문법입니다.
- LINQ의 `Any`, `GroupBy`, `Select`, `Single`: 컬렉션 검색·그룹화·변환·단일 값 검증을 선언적으로 표현합니다.
- `switch` 식과 패턴: 여러 입력 조건을 하나의 결과 값으로 매핑합니다.

## 설계를 읽는 방법

`Program`(Composition Root) → `PrepareShipmentsService`(Application Service) → `IShippingStrategy`(Domain 규칙/Strategy) + `IShipmentRepository`(저장 경계) 순서입니다. 생성자 매개변수로 의존성을 받는 DI와 인터페이스 의존(DIP) 덕분에 실제 DB나 외부 운송사 API 없이 테스트할 수 있습니다.

`Order`와 `ShipmentPlan`은 record로 만들어 값의 의미와 불변성을 강조합니다. nullable 참조 형식과 입력 검증은 null 안전성을 높입니다. Strategy는 운송사 선택 책임만 가져 SRP를 지키며 새 정책을 추가할 때 서비스 실행 흐름을 바꾸지 않는 OCP를 돕습니다. Application Service는 중복 확인, 도메인 규칙 적용, 저장이라는 유스케이스 순서만 조정합니다. 예상 가능한 잘못된 입력은 `Result<T>`로, 취소·인프라 장애처럼 정상 흐름 밖의 실패는 예외로 구분합니다.

메모리 Repository의 중복 검사는 학습용입니다. 운영에서는 여러 서버가 동시에 처리할 수 있으므로 DB unique 제약과 트랜잭션이 필요합니다. 저장과 외부 운송사 요청을 함께 신뢰성 있게 처리하려면 Outbox, 멱등 키, 제한된 재시도와 지수 백오프를 고려합니다. 로그에는 개인정보 대신 주문 ID와 추적 ID를 남기고, 운송사별 성공률·지연 시간·실패율을 메트릭으로 관찰합니다. `CancellationToken`은 끝까지 전달하고 타임아웃, 서킷 브레이커, 실패 격리와 알림 기준도 정해야 합니다.

## 버전 업데이트 (2026-08-15 확인)

- 최신 안정판은 **.NET 10.0.10(LTS), SDK 10.0.302, C# 14**입니다. 이 예제는 로컬 안정 SDK 10.0.301에서 컴파일되는 안정 기능만 사용합니다.
- 최신 미리 보기는 **.NET 11 Preview 6 / C# 15**입니다. C# 15의 collection-expression arguments, union types, closed hierarchies, extension indexers, memory safety 변경은 preview SDK가 필요하므로 실행 코드에 넣지 않았습니다.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 전체 다운로드](https://dotnet.microsoft.com/en-us/download), [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 완료 기준

빌드 경고 0개, self-test 4/4, 기본 실행 결과 계획 2건·제외 3건을 확인하고 Repository·Strategy·Application Service의 책임을 각각 한 문장으로 설명하면 완료입니다.
