# 실습 문제

## 1단계: 초보자 검증

1. `command.Amount`를 `0m`으로 바꾸고 어떤 오류가 출력되는지 확인하세요.
2. 고객 ID를 `customer-2`로 바꿔 결과가 `Approved`가 되는지 확인하세요.
3. `string? Note`의 `?`를 제거했을 때 `null` 전달에 어떤 경고가 생기는지 확인하세요.

## 2단계: 직접 구현

1. 중복 시간 창을 10분에서 5분으로 바꾸고 self-test 기대값도 수정하세요.
2. 100만 원 이상이면 항상 `ManualReview`로 보내는 `HighAmountRule`을 만드세요.
3. 여러 Strategy를 순서대로 적용하는 `CompositeDuplicateRule`을 구현하세요.

## 3단계: 실무 확장

1. 같은 `PaymentId` 저장을 거부해 멱등성을 보장하세요.
2. 취소된 `CancellationToken`을 전달하는 테스트를 추가하세요.
3. 승인/수동 검토 건수를 집계하는 metric 인터페이스를 DI로 주입하세요.

힌트: 새 규칙을 추가할 때 `PaymentReviewService`를 고치지 않는다면 OCP를 잘 적용한 것입니다.
