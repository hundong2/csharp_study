# 실습 문제

## 1단계: 초보자 검증

1. `Quantity`를 `0`으로 바꾸고 실패 메시지를 확인하세요.
2. `CustomerEmail`을 `"wrong"`으로 바꾸고 nullable 선택값 검증 흐름을 따라가세요.
3. 서울과 제주 견적 차이가 왜 3,000원인지 `StandardShippingPolicy`에서 찾으세요.

## 2단계: 직접 수정

1. 무게가 10kg을 넘으면 2,000원을 추가하는 규칙을 작성하세요.
2. `IShippingPolicy`를 구현하는 `ExpressShippingPolicy`를 만들고 기본요금에 5,000원을 더하세요.
3. 새 정책을 Composition Root에서 교체하고 기존 서비스는 수정하지 않았는지 확인하세요.

## 3단계: 실무 확장

1. 요금 계산 결과에 `PolicyVersion`을 추가해 견적 재현성을 높이세요.
2. 취소된 토큰을 전달했을 때 `OperationCanceledException`이 유지되는 self-test를 추가하세요.
3. 외부 배송사 API Repository를 쓴다고 가정해 timeout, 재시도, 로그에 필요한 필드를 적으세요.
