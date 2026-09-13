# 2026-09-14 이해도 점검 — Generic Host 재고 감시 Worker

먼저 답을 펼치지 말고 각 질문을 한두 문장으로 설명하세요. 말로 설명하기 어렵다면 답 아래의 “다시 볼 파일”로 돌아가 직접 근거를 찾습니다.

## 1. `Host.CreateApplicationBuilder`는 무엇을 준비하나요?

<details>
<summary>답 보기</summary>

설정 공급자, `ILogger`, DI 컨테이너, 애플리케이션 시작·종료 수명을 함께 준비합니다. 오늘 `Program.cs`는 명령줄 인자를 담은 `HostApplicationBuilderSettings`를 전달하고, 서비스를 등록한 뒤 Host를 build하고 실행합니다.

다시 볼 파일: [`Program.cs`](./src/InventoryWatcherExercise/Program.cs)

</details>

## 2. hosted service가 singleton이라는 사실이 왜 중요한가요?

<details>
<summary>답 보기</summary>

`AddHostedService<InventoryWatcherWorker>()`로 등록한 Worker는 Host 수명 동안 하나입니다. 여기에 scoped Repository나 `IInventoryWatchCycle`을 직접 주입해 필드에 보관하면 짧은 수명의 객체가 singleton에 붙잡히는 captive dependency가 됩니다.

다시 볼 파일: [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs)

</details>

## 3. `IServiceScopeFactory`는 어떤 문제를 해결하나요?

<details>
<summary>답 보기</summary>

singleton Worker가 매 회차 새 DI scope를 만들 수 있게 합니다. `RunOnceAsync`는 scope 안에서 scoped `IInventoryWatchCycle`을 resolve하고, 성공·예외·취소 어느 경로에서도 `await using`으로 scope를 정리합니다.

다시 볼 파일: [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs)

</details>

## 4. `RunOnceAsync`를 공개 메서드로 둔 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

실제 timer나 무한 반복을 기다리지 않고 “scope 생성 → 한 회차 실행 → scope 해제”를 수동으로 한 번 호출하기 위해서입니다. 업무 규칙 테스트는 `InventoryWatchCycle`에, scheduling과 scope 수명 테스트는 Worker에 집중할 수 있습니다.

다시 볼 파일: [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs), [`SelfTests.cs`](./src/InventoryWatcherExercise/SelfTests.cs)

</details>

## 5. `PeriodicTimer`는 `Task.Delay` 반복과 비교해 무엇을 표현하나요?

<details>
<summary>답 보기</summary>

“다음 주기 신호를 기다린다”는 의도를 `WaitForNextTickAsync`로 직접 표현하고, dispose와 취소로 대기를 끝낼 수 있습니다. 오늘 코드는 이를 `ITickSource` 뒤에 숨겨 테스트에서는 실제 시간을 기다리지 않는 구현으로 바꿀 수 있게 했습니다.

이 주기는 ‘회차 완료 뒤 대기 시간’이 아닙니다. 처리 중 발생한 여러 tick은 하나로 병합될 수 있어 긴 회차 다음 대기가 즉시 끝날 수 있습니다.

`PeriodicTimer`가 놓친 실행을 디스크에 저장하거나 여러 서버의 한 번 실행을 보장하는 것은 아닙니다.

다시 볼 파일: [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs)

</details>

## 6. 한 프로세스 안에서 두 감시 회차가 겹치지 않는 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

`ExecuteAsync`가 tick을 받은 뒤 `RunOnceAsync`를 끝까지 await하고 나서 다음 `WaitForNextTickAsync`로 돌아가기 때문입니다. 그러나 여러 애플리케이션 replica는 각자 Worker를 가지므로 분산 환경의 겹침까지 막지는 않습니다.

다시 볼 파일: [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs)

</details>

## 7. `ValidateOnStart`가 없으면 어떤 문제가 생길 수 있나요?

<details>
<summary>답 보기</summary>

잘못된 간격이나 회차 수가 옵션 값을 처음 읽는 시점까지 숨어 있을 수 있습니다. 시작 시 검증하면 배포 직후 첫 tick 전에 구성 오류를 분명하게 실패시키므로 잘못된 서비스가 실행 중인 척하지 않습니다.

오늘 범위는 간격 25~60000ms, 최대 회차 1~100입니다.

다시 볼 파일: [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs), [`appsettings.json`](./src/InventoryWatcherExercise/appsettings.json)

</details>

## 8. `InventoryItem.Create`는 왜 예외 대신 `Result<InventoryItem>`을 반환하나요?

<details>
<summary>답 보기</summary>

빈 SKU, 공백 상품명, 음수 수량은 호출자가 값을 고쳐 다시 요청할 수 있는 예상 가능한 입력 실패입니다. 반대로 실패 Result의 `Value`를 읽거나 Repository가 계약을 어기는 일은 프로그래머 오류이므로 예외로 빠르게 알립니다.

다시 볼 파일: [`Domain.cs`](./src/InventoryWatcherExercise/Domain.cs)

</details>

## 9. `string?`의 `?`와 `_value!`의 `!`는 어떻게 다른가요?

<details>
<summary>답 보기</summary>

`string?`의 `?`는 null 가능성을 nullable 분석에 드러냅니다. `!`는 런타임 검사가 아니라 “이 지점에서는 null이 아님을 앞선 불변식으로 확인했다”고 컴파일러에 알리는 null-forgiving 연산자입니다. `Result<T>.Success`가 null을 막는 계약이 없다면 `!`만 붙여도 안전해지지 않습니다.

다시 볼 파일: [`Domain.cs`](./src/InventoryWatcherExercise/Domain.cs)

</details>

## 10. `record`를 썼다고 내부의 모든 것이 자동으로 깊은 불변인가요?

<details>
<summary>답 보기</summary>

아닙니다. record의 속성을 다시 할당하기 어렵고 값 기반 비교를 얻지만, 속성이 가리키는 컬렉션 자체가 변경 가능하면 내부 원소는 바뀔 수 있습니다. 그래서 오늘 보고서는 일반 class로 두고 배열 snapshot을 만든 뒤 읽기 전용 계약을 노출합니다. 운영 API에서도 immutable collection이나 명시적 복사 정책을 검토해야 합니다.

다시 볼 파일: [`Domain.cs`](./src/InventoryWatcherExercise/Domain.cs), [`Application.cs`](./src/InventoryWatcherExercise/Application.cs)

</details>

## 11. LINQ 경고 정렬 순서는 무엇이며 왜 결정적이어야 하나요?

<details>
<summary>답 보기</summary>

`OrderByDescending(alert => alert.Level)`로 Critical을 Low보다 먼저 두고, `ThenBy`로 같은 단계의 SKU를 ordinal 순서로 둡니다. Repository의 우연한 반환 순서와 무관해야 로그, 테스트, 재처리 비교가 매번 같습니다.

다시 볼 파일: [`Application.cs`](./src/InventoryWatcherExercise/Application.cs)

</details>

## 12. Alert가 없을 때 Sink를 호출하지 않는 이유는 무엇인가요?

<details>
<summary>답 보기</summary>

`alerts.Length > 0`일 때만 `PublishAsync`를 부르므로 “보낼 경고 없음” 때문에 외부 연결이나 빈 메시지를 만들지 않습니다. 회차 heartbeat가 필요하다면 저재고 전달 Port에 의미를 섞지 말고 별도 observer나 metric으로 모델링하는 편이 명확합니다.

다시 볼 파일: [`Application.cs`](./src/InventoryWatcherExercise/Application.cs)

</details>

## 13. 구조화 로그의 placeholder는 문자열 보간과 무엇이 다른가요?

<details>
<summary>답 보기</summary>

`"단계={Level} | SKU={Sku}"` 같은 고정 템플릿과 값 인자를 따로 넘기면 provider가 `Level`과 `Sku`를 이름 있는 필드로 저장할 수 있습니다. 문자열 보간은 호출 전에 하나의 문자열로 합쳐져 필드별 검색·집계가 어려워집니다.

다시 볼 파일: [`Infrastructure.cs`](./src/InventoryWatcherExercise/Infrastructure.cs), [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs)

</details>

## 14. Host 종료 토큰을 실패 Result로 바꾸면 왜 안 되나요?

<details>
<summary>답 보기</summary>

취소는 입력 오류가 아니라 호출자가 요청한 협력적 중단입니다. `OperationCanceledException`과 원래 토큰을 보존해야 Host와 관측 시스템이 정상 종료를 실제 장애·재시도 대상과 구분할 수 있습니다.

다시 볼 파일: [`Application.cs`](./src/InventoryWatcherExercise/Application.cs), [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs)

</details>

## 15. graceful shutdown은 모든 진행 중 일을 반드시 완료한다는 뜻인가요?

<details>
<summary>답 보기</summary>

아닙니다. 새 일을 받지 않고, 취소 신호를 아래 계층에 전달하고, scope와 timer 같은 자원을 정리한 뒤 제한 시간 안에 종료하는 협력 규약입니다. 외부 전송의 원자성이나 완료 보장이 필요하면 outbox, 멱등 key, reconciliation 같은 별도 설계가 필요합니다.

다시 볼 파일: [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs)

</details>

## 16. 같은 부족 재고가 매 tick마다 다시 알림되는 문제는 어떻게 다루나요?

<details>
<summary>답 보기</summary>

SKU와 창고 범위별 마지막 상태를 내구성 있게 저장하고, Healthy에서 Low/Critical로 바뀌거나 Low에서 Critical로 악화될 때만 안정적인 alert ID로 보냅니다. Sink와 수신자도 같은 ID를 멱등 처리해야 retry나 응답 유실 뒤 중복을 줄일 수 있습니다.

프로세스 메모리 집합만으로는 재시작과 여러 replica를 견디지 못합니다.

다시 볼 파일: [`README.md`](./README.md)

</details>

## 17. `PeriodicTimer` 대신 durable scheduler가 필요한 요구는 무엇인가요?

<details>
<summary>답 보기</summary>

앱이 꺼져 있던 동안의 예정 실행을 나중에 반드시 처리하거나, 여러 서버에서 job claim·재전달·완료 기록을 보존해야 하는 요구입니다. `PeriodicTimer`는 프로세스 메모리 안의 다음 tick만 알려 주므로 missed run과 crash recovery를 저장하지 않습니다.

다시 볼 파일: [`README.md`](./README.md), [`EXERCISES.md`](./EXERCISES.md)

</details>

## 18. .NET 10에서 BackgroundService의 일반 예외만 던지면 왜 운영 감독자가 실패를 놓칠 수 있나요?

<details>
<summary>답 보기</summary>

기본 `StopHost`는 Host를 중지하고 로그를 남기지만, .NET 10의 `RunAsync`는 그 실패를 성공 완료로 처리해 프로세스가 0으로 끝날 수 있습니다. 따라서 non-zero 종료 코드나 별도 실패 신호가 필요합니다. 오늘 코드는 `WorkerExitStatus`로 보정하며, .NET 11부터는 관련 Host 대기 API가 이 예외를 다시 throw합니다.

다시 볼 파일: [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs), [`Program.cs`](./src/InventoryWatcherExercise/Program.cs)

</details>

---

## 코드로 돌아가는 지도

| 막힌 주제 | 다시 볼 곳 |
| --- | --- |
| nullable, `record`, `Result<T>`, 도메인 검증 | [`Domain.cs`](./src/InventoryWatcherExercise/Domain.cs) |
| LINQ, Application Service, Port, 경고 순서 | [`Application.cs`](./src/InventoryWatcherExercise/Application.cs) |
| Repository와 구조화 로그 Sink | [`Infrastructure.cs`](./src/InventoryWatcherExercise/Infrastructure.cs) |
| Options, `BackgroundService`, timer, scope, 종료 | [`Hosting.cs`](./src/InventoryWatcherExercise/Hosting.cs) |
| Generic Host Composition Root | [`Program.cs`](./src/InventoryWatcherExercise/Program.cs) |
| 실제 계약과 회귀 사례 | [`SelfTests.cs`](./src/InventoryWatcherExercise/SelfTests.cs) |
| 운영 한계와 구조도 | [`README.md`](./README.md) |

## 최종 한 문장

빈칸을 보지 않고 말해 보세요.

> “`AddHostedService`의 Worker는 ______ 수명이므로, 매 tick마다 ______를 만들고 그 안에서 scoped ______를 resolve한다. Host의 ______ 토큰은 Repository와 Sink까지 전달하고, 실제 시간 없이 한 회차를 검증할 때는 ______를 직접 호출한다.”

정답 키워드: `singleton`, `scope`, `IInventoryWatchCycle`, `stoppingToken`, `RunOnceAsync`.
