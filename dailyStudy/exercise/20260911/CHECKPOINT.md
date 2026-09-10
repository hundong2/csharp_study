# 2026-09-11 이해도 점검 — 비동기 자원 수명

먼저 답을 보지 말고 각 질문을 한두 문장으로 설명하세요. 말로 설명하기 어렵다면 해당 링크의 코드로 돌아가세요.

## 1. `string?`와 `string`은 무엇이 다른가요?

<details>
<summary>답 보기</summary>

`string?`는 `null`이 올 수 있다는 설계 의도를 nullable 정적 분석에 알립니다. `SettlementExportService.ExportAsync`는 외부 `string?`을 받고, `SettlementExportRequest.Create`가 검사한 뒤 아래 계층에는 null이 아닌 `string ExportName`을 전달합니다. `?` 자체가 런타임의 `string` 형식을 바꾸는 것은 아닙니다.

</details>

## 2. C# 14 `field`는 왜 사용했나요?

<details>
<summary>답 보기</summary>

속성의 명시적인 `_exportName` backing field를 직접 선언하지 않고도 `init` accessor 안에서 컴파일러가 만든 필드에 접근하기 위해 사용했습니다. `private init => field = value.Trim()`은 생성 과정에서 이름을 정규화하고 이후 변경을 막습니다. 사용자 실패 검증은 여전히 `Create` 팩터리의 Result가 담당합니다.

</details>

## 3. `record`가 오늘 Domain 값에 알맞은 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

정산 항목, 요청, 영수증, Problem은 생성 뒤 바뀌지 않는 값으로 다루기 좋습니다. record의 값 기반 비교는 테스트를 단순하게 하고, 불변 스냅샷은 다른 코드가 과거 데이터를 제자리에서 바꾸는 위험을 줄입니다.

</details>

## 4. `await using`은 어떤 경로에서 `DisposeAsync`를 보장하나요?

<details>
<summary>답 보기</summary>

정상 종료와 `return`, Formatter/I/O 예외, `OperationCanceledException`처럼 scope를 빠져나가는 모든 경로에서 정리를 기다립니다. 개념적으로 비동기 `try/finally`와 같으며, 이 예제에서는 성공 Result가 호출자에게 도착하기 전에 Dispose가 완료됩니다.

</details>

## 5. `IAsyncDisposable`이 동기 `IDisposable`과 다른 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

원격 stream 종료, 비동기 flush, multipart upload abort처럼 정리 자체에 기다려야 하는 I/O가 있을 수 있습니다. `IAsyncDisposable.DisposeAsync`는 `ValueTask`를 반환하고 호출자는 `await using`으로 완료를 기다립니다.

</details>

## 6. `DisposeAsync`가 실행되면 항상 commit된 것인가요?

<details>
<summary>답 보기</summary>

아닙니다. Active 상태에서 Dispose하면 staging을 abort하고, Committed 상태에서 Dispose하면 이미 공개된 결과를 유지한 채 handle만 정리합니다. 정리와 업무 성공은 서로 다른 개념입니다.

</details>

## 7. staging에 먼저 쓰는 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

쓰기 중 오류나 취소가 발생해도 소비자가 헤더와 일부 행만 있는 파일을 완성본으로 보지 않게 하기 위해서입니다. 모든 행이 성공한 뒤 commit 경계를 통과한 결과만 최종 이름으로 공개합니다.

</details>

## 8. commit 직전 취소와 commit 직후 취소는 결과가 왜 다른가요?

<details>
<summary>답 보기</summary>

commit 전에는 아직 외부 결과가 없으므로 취소 예외와 abort가 일치합니다. 원자 공개가 끝난 뒤에는 이미 파일이 보이므로 토큰을 다시 검사해 실패라고 반환하면 호출자가 중복 재시도할 수 있습니다. 따라서 서비스는 commit 성공 뒤 취소를 다시 검사하지 않고 영수증을 반환합니다.

</details>

## 9. Ready 항목이 없는 경우는 왜 예외가 아니라 Result인가요?

<details>
<summary>답 보기</summary>

조회 장애나 코드 버그가 아니라 충분히 예상 가능한 업무 상태이기 때문입니다. 호출자는 “오늘 내보낼 내용 없음”을 안내하거나 제품 정책에 따라 헤더만 있는 파일을 선택할 수 있습니다.

</details>

## 10. Repository가 Held 항목을 반환하면 왜 Result가 아니라 예외인가요?

<details>
<summary>답 보기</summary>

`ListReadyAsync` 계약은 같은 날짜의 Ready 항목만 반환하는 것입니다. 이를 어긴 결과를 사용자 실패로 숨기면 잘못된 Adapter가 계속 운영될 수 있으므로 서비스가 구성·코드 오류로 빠르게 알립니다.

</details>

## 11. Strategy와 DI는 오늘 어디에 있나요?

<details>
<summary>답 보기</summary>

`ISettlementFormatter`가 Strategy 계약이고 CSV와 Pipe 구현이 교체 가능한 규칙입니다. `Program` Composition Root가 Repository, Session Factory, 두 Formatter를 만들고 `SettlementExportService` 생성자에 주입합니다. 서비스는 구체 타입을 직접 생성하지 않습니다.

</details>

## 12. CSV escaping은 무엇을 보장하고 무엇을 보장하지 않나요?

<details>
<summary>답 보기</summary>

쉼표·큰따옴표·줄바꿈이 필드 구조를 깨지 않게 인용하고 큰따옴표를 두 배로 만듭니다. 하지만 Excel 같은 프로그램에서 `=`, `+`, `-`, `@`로 시작하는 셀을 수식으로 해석하는 formula injection까지 자동으로 방어하는 것은 아닙니다. 소비 방식에 맞춘 별도 정책이 필요합니다.

</details>

## 13. 같은 목적지에 두 번 commit하면 어떻게 되나요?

<details>
<summary>답 보기</summary>

메모리 Adapter의 `ConcurrentDictionary.TryAdd`가 두 번째 공개를 `IOException`으로 거부합니다. 첫 파일은 유지되고 두 번째 staging은 `await using` 정리에서 abort됩니다. 조용한 overwrite는 데이터 추적과 재시도 판정을 어렵게 하므로 허용하지 않습니다.

</details>

## 14. 이 예제가 실제 운영에서 보장하지 않는 것은 무엇인가요?

<details>
<summary>답 보기</summary>

프로세스 재시작 뒤 내구성, 여러 서버 사이의 원자성, OS·network share의 rename 보장, crash orphan 정리, DB와 파일의 동시 원자 commit, checksum, 암호화, 권한, exactly-once를 보장하지 않습니다. 실제 Adapter와 export job/idempotency/reconciliation 설계가 더 필요합니다.

</details>

---

## 코드로 돌아가는 지도

| 막힌 질문 | 다시 볼 곳 |
| --- | --- |
| nullable, record, Result, `field` | [`Domain.cs`](./src/SettlementExportExercise/Domain.cs) |
| `await using`, Port, commit 전후 취소 | [`Application.cs`](./src/SettlementExportExercise/Application.cs) |
| CSV/Pipe, state, commit/abort/Dispose | [`Infrastructure.cs`](./src/SettlementExportExercise/Infrastructure.cs) |
| DI와 Composition Root | [`Program.cs`](./src/SettlementExportExercise/Program.cs) |
| 오류·취소 시 실제 상태 | [`SelfTests.cs`](./src/SettlementExportExercise/SelfTests.cs) |
| 구조도와 운영 한계 | [`README.md`](./README.md) |

## 최종 한 문장

빈칸을 보지 않고 말해 보세요.

> “Factory에서 연 비동기 세션은 ______로 소유하고, 완성 전 줄은 ______에 둔다. 공개 전 실패는 ______하고, 원자 ______ 뒤에는 늦은 취소로 성공을 뒤집지 않는다.”

정답 키워드: `await using`, `staging`, `abort`, `commit`.
