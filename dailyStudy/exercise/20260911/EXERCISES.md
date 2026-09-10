# 2026-09-11 연습문제 — 비동기 자원 수명과 commit-or-abort

## 연습 방법

1. 먼저 원본 상태에서 Release 빌드와 `--self-test` 19/19를 확인합니다.
2. 문제마다 작은 Git 브랜치나 별도 커밋을 사용합니다.
3. 코드를 바꾸기 전에 예상 출력과 지켜야 할 불변식을 한 줄로 적습니다.
4. 새 메서드에는 목적, 파라미터, 반환값을 설명하는 한글 주석을 답니다.
5. 각 단계가 끝날 때 빌드, 일반 데모, 자체 테스트를 다시 실행합니다.

```powershell
dotnet build dailyStudy/exercise/20260911/src/SettlementExportExercise/SettlementExportExercise.csproj -c Release
dotnet run --project dailyStudy/exercise/20260911/src/SettlementExportExercise/SettlementExportExercise.csproj -c Release --no-build
dotnet run --project dailyStudy/exercise/20260911/src/SettlementExportExercise/SettlementExportExercise.csproj -c Release --no-build -- --self-test
```

---

## Beginner 1 — 입력 검증 경계 찾기

[`Domain.cs`](./src/SettlementExportExercise/Domain.cs)의 `SettlementExportRequest.Create`를 읽고 다음 입력의 결과 코드 또는 성공값을 실행 전에 적습니다.

| 입력 이름 | 예상 |
| --- | --- |
| `null` | ? |
| `"   "` | ? |
| `" ../escape "` | ? |
| `" daily_export "` | ? |
| `"보고서"` | ? |

그다음 `RequestValidationStopsBeforeDependenciesAsync`에 표의 사례를 추가합니다. 실패 입력에서 Repository와 Session Factory 호출 수가 모두 0인지 함께 검증하세요.

완료 조건:

- nullable `string?`와 검증 뒤 `string`의 차이를 설명한다.
- `field` accessor가 `daily_export`로 Trim하는 지점을 찾는다.
- 사용자 입력 실패가 예외가 아니라 `Result`인 이유를 말한다.

## Beginner 2 — 상태와 반복문

`Program.cs`의 초기 데이터에 다음 항목을 추가합니다.

```text
PAY-000 / 새벽 상점 / 500.25 / 2026-09-11 / Ready
```

코드를 실행하기 전에 다음을 예측하세요.

- 파일의 첫 데이터 행은 무엇인가요?
- `rows`와 `total`은 얼마인가요?
- `Held` 항목은 왜 빠지나요?

기대값과 `isExpected`, README의 데모 출력은 연습 브랜치에서만 함께 갱신합니다. `decimal` literal에는 `m` 접미사를 사용하세요.

## Beginner 3 — CSV escaping 검증

`CsvSettlementFormatter`가 아래 상점명을 어떻게 출력해야 하는지 먼저 손으로 적고 자체 테스트를 추가합니다.

- `서울,상점`
- `"바다"상점`
- `평범한상점`

큰따옴표가 필요한 조건과 내부 큰따옴표를 두 번 쓰는 이유를 설명하세요. 구조적 CSV escaping과 spreadsheet formula injection 방어가 서로 다른 문제라는 테스트 이름 또는 주석도 추가합니다.

---

## Intermediate 1 — 새 TSV Strategy 추가

탭으로 구분하는 `TsvSettlementFormatter`를 추가합니다.

1. `ExportFormat`에 `Tsv`를 추가합니다.
2. `ISettlementFormatter` 구현을 만듭니다.
3. 탭과 역슬래시를 손실 없이 표현할 escaping 규칙을 정합니다.
4. Composition Root와 테스트용 `CreateService`에 새 Strategy를 등록합니다.
5. 정확한 헤더, 행, 확장자 `.tsv`를 확인하는 자체 테스트를 추가합니다.

생성자 누락 검사가 `Enum.GetValues<ExportFormat>()`를 사용하므로 enum만 추가하고 Strategy 등록을 잊으면 시작 시 실패해야 합니다. 이것이 fail-fast 구성 검사의 장점입니다.

## Intermediate 2 — 빈 파일 정책 바꾸기

현재는 Ready 항목이 없으면 `export.no_ready_settlements` Result를 반환하고 세션을 열지 않습니다. 제품 요구가 “헤더만 있는 빈 파일도 발행”으로 바뀌었다고 가정합니다.

- 어떤 검사를 세션 획득 뒤로 옮겨야 하나요?
- receipt의 `ExportedCount`와 `TotalAmount`는 무엇이어야 하나요?
- 기존 `NoReadyRowsAvoidsSessionAsync`는 어떤 새 이름과 assertion이 필요하나요?
- 빈 파일 발행과 “데이터 조회 장애”를 어떻게 구분하나요?

요구가 바뀐 만큼 README의 업무 정책도 함께 수정하세요. 단순히 테스트를 통과시키려고 예외를 삼키지 마세요.

## Intermediate 3 — `await using`을 풀어 써 보기

연습용 메서드 `ExportWithExplicitFinallyAsync`를 만들고 `await using` 대신 명시적인 `try/finally`를 사용해 같은 동작을 구현합니다.

```csharp
ISettlementExportSession? session = null;
try
{
    // Open, write, commit
}
finally
{
    if (session is not null)
    {
        await session.DisposeAsync();
    }
}
```

주의할 점:

- `OpenAsync`가 실패하면 아직 정리할 세션이 없습니다.
- 세션을 받은 직후부터 모든 코드가 `try` 범위에 들어가야 합니다.
- `DisposeAsync`에 이미 취소된 요청 토큰을 전달하려 하지 않습니다.
- 성공 `return`에서도 finally가 먼저 끝납니다.

`SelfTests`의 서비스 호출을 하나의 `ExportOperation` delegate로 모으고 각 테스트에 전달하세요. 테스트 runner는 아래 두 delegate로 같은 19개 시나리오를 각각 실행해야 합니다.

```csharp
private delegate Task<Result<SettlementExportReceipt>> ExportOperation(
    SettlementExportService service,
    string? name,
    DateOnly date,
    ExportFormat format,
    CancellationToken token);

ExportOperation awaitUsingOperation = (service, name, date, format, token) =>
    service.ExportAsync(name, date, format, token);

ExportOperation explicitFinallyOperation = (service, name, date, format, token) =>
    service.ExportWithExplicitFinallyAsync(name, date, format, token);
```

`delegate`는 같은 매개변수와 반환형을 가진 메서드를 값처럼 전달하기 위한 형식입니다. 여기서는 테스트 본문이 특정 구현 이름을 직접 부르지 않게 합니다. 출력에서 `await using` 구현 19개와 명시적 `try/finally` 구현 19개가 모두 통과하는지 확인합니다. 새 메서드를 호출하지 않는 기존 테스트만 다시 돌린 것은 완료로 인정하지 않습니다. 비교가 끝나면 실제 코드에는 더 읽기 쉬운 `await using`을 유지합니다.

---

## Advanced 1 — 중복 정산 ID를 업무 실패로 바꿀지 결정하기

현재 Repository가 중복 ID를 반환하면 Adapter 계약 버그로 보고 예외를 던집니다. 외부 공급자가 중복 데이터를 정상적으로 보낼 수 있는 시스템이라면 정책을 다시 설계하세요.

선택지 A: 첫 항목만 선택하고 중복 개수를 receipt에 기록합니다.

선택지 B: 중복 전체를 실패 `Result`로 거부합니다.

선택지 C: 별도 quarantine 파일로 분리하고 정상 행만 commit합니다.

하나를 선택하고 다음을 구현합니다.

- 선택의 데이터 손실·재처리 위험을 README에 기록합니다.
- `HashSet<string>.Add` 결과를 이용해 중복을 결정적으로 찾습니다.
- 중복이 첫 행 뒤 발견되어도 잘못된 최종 파일이 보이지 않는 테스트를 추가합니다.
- 같은 요청 재실행 결과가 달라지지 않는지 검증합니다.

## Advanced 2 — 실제 파일 시스템 Adapter

새 `FileSystemSettlementExportSessionFactory`를 별도 파일에 구현합니다.

필수 조건:

1. 사용자가 준 문자열을 경로로 직접 결합하지 않고, 검증된 leaf name과 고정 root를 사용합니다.
2. 최종 파일과 같은 디렉터리에 예측 불가능한 `.part` 파일을 만듭니다.
3. `StreamWriter` 또는 `FileStream`을 비동기 옵션으로 열고 `WriteLineAsync`를 구현합니다.
4. `CommitAsync`에서 flush하고 writer를 닫은 뒤 overwrite 없이 move/rename합니다.
5. commit 전 `DisposeAsync`는 `.part`를 best-effort로 정리합니다.
6. 같은 final name 경쟁에서는 정확히 한 요청만 성공합니다.
7. 테스트에는 `Path.GetTempPath()` 아래 테스트 전용 디렉터리를 사용하고, 정리 대상의 절대 경로가 그 디렉터리 안인지 확인합니다.

운영 문서에는 OS/file system/network share마다 rename 원자성과 durability 보장이 다름을 명시하세요.

## Advanced 3 — cancellation gate 테스트

`TaskCompletionSource` 두 개로 commit 직전 실제 비동기 gate를 만듭니다.

1. 세션이 commit 경계에 도착하면 `reachedCommit`을 완료합니다.
2. 테스트는 그 신호를 기다린 뒤 토큰을 취소합니다.
3. `allowCommit`을 열어 세션이 계속 진행하게 합니다.
4. 최종 파일 없음, abort 1, Dispose 1, 원래 토큰의 `OperationCanceledException`을 검증합니다.

`Thread.Sleep`과 임의 지연은 사용하지 마세요. `WaitAsync(TimeSpan)`은 테스트가 영원히 멈추지 않게 하는 실패 안전장치로만 사용합니다.

---

## Pro 1 — 결과 불확실성과 멱등 재시도

“Object Storage가 파일 공개는 끝냈지만 응답 전에 연결이 끊겼다”는 상황을 설계합니다.

- 호출자가 같은 `ExportId`로 재시도할 수 있게 request를 확장합니다.
- final object metadata에 `ExportId`, row count, checksum을 기록합니다.
- 이미 같은 ID·checksum이 있으면 기존 영수증을 반환합니다.
- 같은 ID인데 checksum이 다르면 충돌로 거부합니다.
- commit 응답 예외 뒤 조회로 성공 여부를 판정합니다.

최소 테스트:

- 첫 commit 성공
- 같은 ID·같은 내용 재시도
- 같은 ID·다른 내용 충돌
- 공개 뒤 응답 손실
- 두 Task의 같은 ID 경쟁

“exactly once”라고 부르기보다 어떤 저장소 조건부 연산과 조회가 실제로 보장되는지 구체적으로 적으세요.

## Pro 2 — crash orphan janitor

프로세스가 commit 또는 Dispose 전에 종료되어 `.part`가 남는 상황을 위한 janitor를 설계합니다.

- 파일 이름에 신뢰할 수 있는 export ID를 넣습니다.
- 생성 시각, owner, lease 또는 heartbeat 중 무엇을 근거로 고아를 판단할지 정합니다.
- 너무 최근 파일과 현재 사용 중인 파일은 지우지 않습니다.
- 삭제 전 root 경계와 symbolic link/reparse point 위험을 검토합니다.
- dry-run 모드, 삭제 metric, 감사 로그를 포함합니다.

시간 기반 테스트는 실제 시계 대신 `TimeProvider`를 주입해 결정적으로 만드세요.

---

## 최종 완료 체크리스트

- [ ] 새 코드의 모든 메서드에 목적·파라미터·반환값 한글 설명이 있다.
- [ ] 처음 등장하는 `field`, `await using`, `IAsyncDisposable`, `ValueTask`를 쉬운 말로 설명했다.
- [ ] 성공 전 부분 데이터는 staging에만 있고 최종 이름으로 보이지 않는다.
- [ ] commit 전 Result·예외·취소 경로에서 자원이 정리된다.
- [ ] commit 뒤 늦은 취소가 성공을 실패로 뒤집지 않는다.
- [ ] Result, 예외, 취소의 경계를 테스트 이름으로 드러냈다.
- [ ] deterministic sort, invariant decimal/date, delimiter escaping을 검증했다.
- [ ] 같은 최종 이름을 조용히 덮어쓰지 않는다.
- [ ] process-local 예제와 운영 보장의 차이를 문서화했다.
- [ ] Release 빌드 경고 0·오류 0, 데모 exit code 0, 전체 self-test 통과를 확인했다.
