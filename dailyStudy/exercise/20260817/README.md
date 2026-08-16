# 2026-08-17: 환불 요청 심사와 환불 수단 선택

## 처음 읽는 순서

1. 아래 **처음 만나는 기본 문법**을 먼저 읽습니다.
2. [`Program.cs`](./src/RefundReviewExercise/Program.cs)를 위에서 아래로 읽어 실행 흐름을 따라갑니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 작은 변경을 직접 구현합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 말로 답하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260817/src/RefundReviewExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고를 오류로 처리해 null 위험을 컴파일 단계에서 발견합니다.

## 처음 만나는 기본 문법

- `var requests = new[] { ... };`: 오른쪽 값에서 배열 형식을 추론합니다. C# 문장은 보통 `;`로 끝납니다.
- `string`, `string?`: `string`은 null이 아니어야 하고 `string?`는 값이 없을 수 있습니다. `?.`는 null일 때 접근을 멈춥니다.
- `decimal`, `int`, `enum`: 각각 정확한 금액, 정수, 제한된 선택지를 표현합니다. 금액 리터럴의 `m`은 `decimal`을 뜻합니다.
- `if`, `foreach`, `continue`, `return`: 조건 분기, 반복, 다음 반복으로 이동, 현재 메서드 종료입니다.
- `record`: 값 중심 데이터와 불변성을 간결하게 표현합니다.
- `interface`: 필요한 동작의 계약입니다. 구현 교체와 테스트 대역 사용을 쉽게 합니다.
- `Task<T>`, `async`, `await`: DB나 API 같은 I/O를 기다리는 동안 스레드를 불필요하게 점유하지 않는 비동기 문법입니다.
- LINQ의 `OrderBy`, `Single`: 컬렉션 정렬과 정확히 한 값 검증을 선언적으로 표현합니다.
- switch 식: 결제 코드별 결과를 짧고 빠짐없이 매핑합니다.
- 제네릭 `Result<T>`: `T`는 호출할 때 `decimal` 같은 실제 형식으로 정해집니다. 성공 여부를 먼저 확인한 뒤에만 `Value`를 읽는 계약입니다.

## 설계를 읽는 방법

`Program`(Composition Root) → `ReviewRefundsService`(Application Service) → `IRefundPolicy`(Domain 규칙) + `IRefundMethodStrategy`(Strategy) + Repository 순서입니다. 생성자로 인터페이스 의존성을 주입하는 DI와 DIP 덕분에 실제 DB 없이 테스트할 수 있습니다.

`RefundRequest`와 결과들은 record로 값과 불변성을 강조합니다. 정책과 환불 수단 선택을 분리해 SRP를 지키고, 새 수단 추가 시 서비스 흐름을 덜 고치는 OCP 방향을 보여 줍니다. 예상 가능한 입력 실패는 `Result<T>`로, 취소·인프라 장애·깨진 코드 계약은 예외로 구분합니다. 예외를 모든 업무 분기에 쓰면 정상 거절과 장애가 섞여 운영 대응이 어려워집니다.

메모리 Repository는 학습용입니다. 실무에서는 환불 ID unique 제약, 트랜잭션, 멱등성 키로 중복 환불을 막아야 합니다. 외부 결제 API와 DB 저장의 원자성이 필요하면 Outbox나 보상 작업을 고려합니다. `CancellationToken`을 끝까지 전달하고, 제한된 지수 백오프 재시도와 타임아웃을 둡니다. 로그에는 환불 ID·추적 ID·결과·소요 시간을 남기되 계좌번호나 개인정보는 마스킹하며, 승인/거절/실패율과 수동 검토 적체를 지표와 알림으로 관찰합니다.

## 버전 업데이트 (2026-08-17 확인)

- 최신 안정판은 **.NET 10.0.11(LTS), SDK 10.0.400, C# 14**입니다. 이 예제는 로컬 안정 SDK 10.0.301에서 컴파일되는 안정 기능만 사용합니다.
- 최신 미리 보기는 **.NET 11 Preview 7 / C# 15**입니다. C# 15 기능은 preview SDK가 필요하므로 실행 코드에 넣지 않았습니다. 미리 보기 기능은 설계가 바뀔 수 있어 학습·실험 프로젝트에서 별도로 평가하세요.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 11 Preview 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/11.0), [C# 14 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 완료 기준

빌드 경고 0개, self-test 4/4, 기본 실행의 승인 1건·거절 3건을 확인하고 Repository·Strategy·Application Service의 책임을 각각 한 문장으로 설명하면 완료입니다.
