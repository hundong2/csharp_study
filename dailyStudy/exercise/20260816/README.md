# 2026-08-16: 재고 이동 요청 승인과 창고 선택

## 처음 읽는 순서

1. 아래 **처음 만나는 기본 문법**을 먼저 읽습니다.
2. [`Program.cs`](./src/InventoryTransferExercise/Program.cs)를 위에서 아래로 읽어 실행 흐름을 따라갑니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 작은 변경을 직접 구현합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 말로 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260816/src/InventoryTransferExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고를 오류로 처리해 null 위험을 컴파일 단계에서 찾습니다.

## 처음 만나는 기본 문법

- `var requests = new[] { ... };`: 오른쪽 값으로 변수 형식을 추론하고 여러 값을 배열에 담습니다. C# 문장은 보통 `;`로 끝납니다.
- `string`과 `string?`: `string`은 null이 아니어야 하고 `string?`는 null일 수 있습니다. `IsNullOrWhiteSpace`로 null·빈 문자열·공백을 함께 검사합니다.
- `int`, `bool`, `enum`: 정수, 참/거짓, 제한된 선택지를 표현합니다.
- `if`, `foreach`, `continue`, `return`: 조건 분기, 반복, 다음 반복으로 이동, 현재 메서드 종료입니다.
- `record`: 값 중심 데이터를 간결하고 불변에 가깝게 표현합니다.
- `interface`: 필요한 동작의 계약입니다. 구현 교체가 쉬워져 DI와 테스트에 유리합니다.
- `Task<T>`, `async`, `await`: DB 같은 I/O를 기다리는 동안 스레드를 붙잡지 않는 비동기 문법입니다.
- LINQ의 `OrderBy`, `Single`: 컬렉션 정렬과 단일 값 검증을 선언적으로 표현합니다.
- `T?`, `??`: 값이 없을 가능성을 표시하고, 없을 때 대체 값을 선택합니다.

## 설계를 읽는 방법

`Program`(Composition Root) → `PlanTransfersService`(Application Service) → `IApprovalStrategy`(Domain 규칙/Strategy) + Repository 순서입니다. 생성자 매개변수로 의존성을 받는 DI와 인터페이스에 의존하는 DIP 덕분에 실제 DB 없이 테스트할 수 있습니다.

`TransferRequest`와 `TransferPlan`은 record로 값과 불변성을 강조합니다. Strategy는 승인 정책만 맡아 SRP를 지키고, 정책을 추가할 때 서비스 흐름을 덜 고치는 OCP를 돕습니다. Application Service는 조회·규칙 적용·저장 순서를 조정합니다. 예상 가능한 잘못된 입력은 `Result<T>`로, 취소·인프라 장애·깨진 코드 계약처럼 정상 흐름 밖 실패는 예외로 구분합니다.

메모리 Repository의 중복 검사는 학습용입니다. 운영에서는 DB unique 제약과 트랜잭션으로 멱등성을 보장하고, 재고 차감에는 낙관적 동시성 또는 잠금이 필요합니다. 저장과 메시지 발행을 함께 처리한다면 Outbox를 고려합니다. 로그에는 요청 ID와 추적 ID를 남기되 개인정보는 피하고, 승인/보류 수·처리 시간·실패율을 메트릭으로 관찰합니다. `CancellationToken`, 타임아웃, 제한된 재시도와 백오프, 실패 격리·알림 기준도 정해야 합니다.

## 버전 업데이트 (2026-08-16 확인)

- 최신 안정판은 **.NET 10.0.10(LTS), SDK 10.0.302, C# 14**입니다. 이 예제는 로컬 안정 SDK 10.0.301에서 컴파일되는 안정 기능만 사용합니다.
- 최신 미리 보기는 **.NET 11 Preview 6 / C# 15**입니다. C# 15의 collection-expression arguments, union types, closed hierarchies, extension indexers, memory safety 변경은 preview SDK가 필요하므로 실행 코드에 넣지 않았습니다.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 전체 버전](https://dotnet.microsoft.com/en-us/download/dotnet), [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/), [C# 14 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 완료 기준

빌드 경고 0개, self-test 4/4, 기본 실행 결과 승인 1건·보류 3건을 확인하고 Repository·Strategy·Application Service의 책임을 각각 한 문장으로 설명하면 완료입니다.
