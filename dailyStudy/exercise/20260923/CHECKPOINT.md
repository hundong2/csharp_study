# 이해도 체크포인트

먼저 답을 종이나 메모장에 적은 뒤 해설을 펼치세요. 상태 코드만 맞히는 것보다 “왜”와 “client의 다음 행동”까지 설명하는 것이 목표입니다.

## 1. ETag는 상품의 version 숫자와 같은가요?

<details>
<summary>해설 보기</summary>

같다고 가정하면 안 됩니다. 이 예제는 학습을 위해 version 2를 `"v2"`로 표현하지만 ETag는 server가 정한 불투명한 validator입니다. client는 내부를 parse하거나 다음 값을 예측하지 말고 응답에서 받은 문자열 전체를 저장해 조건 헤더에 그대로 보내야 합니다.

</details>

## 2. `If-None-Match`와 `If-Match`는 무엇이 다른가요?

<details>
<summary>해설 보기</summary>

`If-None-Match`는 “이 값과 같지 않을 때만 표현을 보내라”는 GET cache 재검증에 사용합니다. 같으면 304와 빈 본문입니다. `If-Match`는 “이 값과 같을 때만 상태를 바꿔라”는 쓰기 전제 조건입니다. 다르면 412로 수정하지 않습니다.

</details>

## 3. GET에서는 weak ETag가 맞지만 PATCH에서는 맞지 않을 수 있는 이유는 무엇인가요?

<details>
<summary>해설 보기</summary>

GET cache 재검증은 표현이 의미상 같으면 재사용할 수 있어 weak 비교를 사용합니다. 쓰기는 정확히 같은 상태에서만 적용해야 다른 변경을 잃지 않으므로 strong 비교를 사용합니다. 따라서 현재 `"v2"`에 대해 `W/"v2"`는 GET에는 맞아도 `If-Match` PATCH에는 맞지 않습니다.

</details>

## 4. 400, 412, 428은 어떻게 구분하나요?

<details>
<summary>해설 보기</summary>

400은 ETag 문법 또는 Domain 입력이 잘못되어 요청을 고쳐야 하는 경우입니다. 412는 문법은 맞지만 현재 상태와 조건이 달라 최신 값을 다시 읽고 변경을 재적용해야 하는 경우입니다. 428은 이 endpoint가 요구하는 `If-Match` 자체가 없어 먼저 GET해야 하는 경우입니다.

</details>

## 5. endpoint가 ETag를 검사했는데 Repository가 버전을 다시 비교하는 이유는 무엇인가요?

<details>
<summary>해설 보기</summary>

endpoint의 조회와 실제 저장 사이에 다른 요청이 먼저 저장할 수 있기 때문입니다. 이것이 time-of-check/time-of-use race입니다. 최종 저장소가 “현재 버전 비교 + 교체”를 하나의 원자적 연산으로 해야 그 짧은 틈의 lost update까지 막습니다.

</details>

## 6. 왜 `CatalogItem`은 public setter 대신 `Create`와 `Revise`를 사용하나요?

<details>
<summary>해설 보기</summary>

어떤 진입 경로에서도 이름·가격·버전 규칙을 우회하지 못하게 하기 위해서입니다. get 전용 속성과 private 생성자는 유효한 상태만 존재하게 하고, `Revise`는 기존 객체를 바꾸지 않은 새 snapshot을 만들어 동시 읽기와 테스트를 단순하게 합니다.

</details>

## 7. 입력 오류를 모두 예외로 던지지 않는 이유는 무엇인가요?

<details>
<summary>해설 보기</summary>

빈 이름과 음수 가격은 충분히 예상 가능하고 사용자가 고칠 수 있습니다. `Result<T>`로 표현하면 호출자가 정상 분기로 처리할 수 있고 stack trace도 필요 없습니다. 반대로 성공 결과에 상품이 없거나 seed가 중복되는 것은 개발자가 고칠 설계 오류이므로 예외로 빨리 드러냅니다.

</details>

## 8. `CancellationToken`은 업무 검증 실패인가요?

<details>
<summary>해설 보기</summary>

아닙니다. 호출자가 연결을 끊거나 작업이 더 필요 없음을 알리는 제어 흐름입니다. 원래 token을 Repository까지 전달하고 `OperationCanceledException`을 보존해야 상위 host가 취소를 올바르게 구분할 수 있습니다.

</details>

## 9. `IEntityTagCodec`을 interface로 둔 실용적인 이유는 무엇인가요?

<details>
<summary>해설 보기</summary>

ETag wire format과 비교 정책은 HTTP Adapter의 교체 가능한 전략입니다. endpoint가 따옴표·weak marker·목록 parser를 반복하지 않고, 테스트가 server 없이 정책만 확인할 수 있습니다. 향후 DB token이나 hash 기반 format으로 바꿀 때 영향 범위도 작습니다.

</details>

## 10. 메모리 Repository의 `lock`이 여러 서버 인스턴스도 보호하나요?

<details>
<summary>해설 보기</summary>

아닙니다. `lock`은 같은 process 안에서 같은 `_gate` 객체를 공유하는 thread만 보호합니다. 여러 process나 서버에서는 DB의 concurrency token과 조건부 UPDATE, transaction, 영향받은 행 수 검사가 필요합니다.

</details>

## 11. 모든 서비스를 Singleton으로 등록해도 되나요?

<details>
<summary>해설 보기</summary>

아닙니다. 오늘 Repository는 lock으로 공유 상태를 보호하고 Application Service와 codec은 요청별 mutable 상태가 없어 Singleton이 가능합니다. scoped `DbContext`를 쓰는 Repository로 바꾸면 그것을 잡는 Application Service도 scoped여야 합니다. Singleton이 scoped dependency를 보관하면 요청 수명이 섞이는 captive dependency가 됩니다.

</details>

## 12. 412를 받은 client가 같은 변경을 최신 ETag로 자동 재시도하면 충분한가요?

<details>
<summary>해설 보기</summary>

항상 그렇지 않습니다. 다른 사용자의 변경과 내 변경이 충돌할 수 있으므로 최신 상태를 읽고 차이를 보여 주거나 업무 규칙에 따라 병합해야 합니다. 아무 확인 없이 최신 ETag만 붙여 재시도하면 결국 다른 변경을 덮을 수 있습니다.

</details>
