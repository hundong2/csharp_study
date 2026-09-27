# 이해도 체크포인트

먼저 답을 말하거나 적은 뒤에만 `<details>`를 여세요.

## 1. `AddRequestTimeouts`만 호출하면 모든 endpoint가 자동으로 제한되는가?

<details>
<summary>해설 보기</summary>

아닙니다. 서비스 등록 뒤 pipeline에 `UseRequestTimeouts()`가 있어야 하고, 전역 기본 정책을 설정하거나 endpoint에 `WithRequestTimeout`/attribute로 정책을 연결해야 합니다.

</details>

## 2. timeout은 실행 중인 스레드를 강제로 종료하는가?

<details>
<summary>해설 보기</summary>

아닙니다. `HttpContext.RequestAborted`에 취소가 요청됩니다. Repository, 외부 HTTP, DB, CPU loop가 token을 관찰하고 스스로 멈춰야 합니다.

</details>

## 3. 왜 Endpoint의 token과 Repository의 token이 같아야 하는가?

<details>
<summary>해설 보기</summary>

중간 계층에서 새 token이나 `CancellationToken.None`으로 바꾸면 원래 요청의 timeout/연결 중단 신호가 아래 작업에 도달하지 않습니다. 같은 작업 수명을 나타내는 신호를 끝까지 전달해야 합니다.

</details>

## 4. 빈 고객 번호와 timeout을 모두 `Result.Failure`로 만들면 왜 안 되는가?

<details>
<summary>해설 보기</summary>

빈 고객 번호는 정상적인 업무 분기지만 timeout은 현재 제어 흐름을 중단하라는 신호입니다. 취소를 Result로 삼키면 timeout middleware가 504를 작성하지 못하고 호출자가 작업이 취소됐다는 사실도 잃습니다.

</details>

## 5. 없는 고객이 400이 아니라 404인 이유는?

<details>
<summary>해설 보기</summary>

`CUST-999`는 형식상 유효해서 요청 검증을 통과합니다. 다만 Repository에 해당 리소스가 없으므로 404가 맞습니다. `AB`처럼 형식 자체가 잘못된 값은 400입니다.

</details>

## 6. `simulateMs=600`이 왜 약 150ms 뒤 504가 되는가?

<details>
<summary>해설 보기</summary>

보고서 endpoint에 `report-generation` 150ms 정책이 연결되어 있습니다. 시간이 끝나면 `Task.Delay(600ms, token)`의 token이 취소되어 대기가 중단되고 middleware가 504 본문을 씁니다.

</details>

## 7. timeout 응답 writer에서만 `CancellationToken.None`을 쓰는 이유는?

<details>
<summary>해설 보기</summary>

writer가 실행될 때 `RequestAborted`는 이미 timeout으로 취소되었습니다. 같은 token으로 오류 본문을 쓰면 즉시 취소되어 클라이언트가 원인을 못 받을 수 있습니다. 여기서는 작고 고정된 JSON 쓰기에만 제한적으로 `None`을 사용합니다.

</details>

## 8. `record`가 이 예제에 주는 이점은?

<details>
<summary>해설 보기</summary>

`DomainError`, `InvoiceLine`, `ReportDocument`는 값 중심 데이터입니다. record는 값 비교와 읽기 전용 모델 표현이 간단해지고, 생성 후 변경을 줄여 비동기 흐름과 테스트의 추론을 쉽게 합니다.

</details>

## 9. Repository를 interface와 Adapter로 나눈 이유는?

<details>
<summary>해설 보기</summary>

Application이 메모리 Dictionary나 EF Core 같은 구체 저장 기술을 모르게 하려는 의존성 역전입니다. 테스트에서는 빠른 가짜 구현, 운영에서는 DB Adapter를 DI로 바꿀 수 있습니다.

</details>

## 10. CSV와 JSON을 `switch` 하나에 넣지 않고 Strategy로 나눈 이유는?

<details>
<summary>해설 보기</summary>

각 형식의 escaping, MIME, 파일 이름 규칙은 서로 다른 변경 이유입니다. Strategy로 나누면 새 형식을 추가할 때 Application 유스케이스를 수정하지 않고 구현·등록만 추가할 수 있습니다.

</details>

## 11. 디버거를 붙였을 때 timeout 테스트가 실패할 수 있는 이유는?

<details>
<summary>해설 보기</summary>

ASP.NET Core 공식 문서에 따라 디버그 중에는 request timeout middleware가 발동하지 않습니다. 중단점에서 사람이 오래 머물러도 요청이 임의로 취소되지 않게 하기 위한 동작입니다. 자동 검증은 디버거 없이 실행합니다.

</details>

## 12. 응답을 이미 시작한 뒤 timeout이 오면 항상 504로 바꿀 수 있는가?

<details>
<summary>해설 보기</summary>

아닙니다. header나 body가 이미 전송되면 상태 코드와 header를 다시 쓸 수 없습니다. streaming·대용량 다운로드는 시작 전 준비 budget, 중간 중단 계약, 클라이언트 재개 방식 등을 별도로 설계해야 합니다.

</details>

## 최종 30초 복습

- 등록: `AddRequestTimeouts`
- pipeline: `UseRequestTimeouts`
- endpoint 연결: `WithRequestTimeout`
- 제외: `DisableRequestTimeout`
- 신호: `HttpContext.RequestAborted`
- 전파: Endpoint → Application → Repository
- 예상 오류: `Result`
- 중단 신호: `OperationCanceledException` 재전파
- 자동 검증: Release build + self-test + 실제 HTTP 504
