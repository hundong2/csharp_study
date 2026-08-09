# 초보자 확인 및 복습

1. `string`과 `string?`, `decimal`과 `m`의 의미는 무엇인가요?
2. record와 interface는 각각 왜 사용했나요?
3. `Sum`, `Any`, `await`, `CancellationToken`은 무슨 일을 하나요?
4. Strategy·Repository·Application Service·Composition Root의 책임은 무엇인가요?
5. 없는 장바구니는 Result, DB 단절은 예외인 이유는 무엇인가요?

## 모범 답안 요약

nullable은 값 누락을 공개하고 decimal은 금액 오차를 줄입니다. record는 값 데이터, interface는 교체 가능한 계약입니다. Strategy는 규칙, Repository는 저장, Application Service는 순서, Composition Root는 조립을 담당합니다. 예상 가능한 실패는 Result, 예상 밖 기술 장애는 경계에서 기록할 예외가 적절합니다.
