# 05. Async, Cancellation, Retry

`async`/`await`는 네트워크, DB, 파일 I/O처럼 기다리는 시간이 있는 작업에서 중요합니다. `CancellationToken`은 더 이상 필요 없는 작업을 멈추는 표준 방식입니다.

## 핵심 개념

- `Task<T>`: 나중에 완료될 값을 나타냅니다.
- `await`: 비동기 작업 완료를 기다립니다.
- `CancellationToken`: 취소 신호입니다.
- retry: 일시 실패를 다시 시도합니다.

## 실무에서 왜 필요한가

외부 API는 일시적으로 실패할 수 있습니다. 재시도를 하되 timeout과 취소를 함께 설계해야 장애가 커지지 않습니다.
