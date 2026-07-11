# 11. Concurrency Primitives

멀티스레드 환경에서는 여러 작업이 같은 값을 동시에 바꿀 수 있습니다. 이때 `lock`, `Interlocked`, `SemaphoreSlim`을 적절히 사용합니다.

## 기준

- 단순 숫자 증가: `Interlocked`
- 여러 줄의 상태 변경: `lock`
- 비동기 동시성 제한: `SemaphoreSlim`

## 실무에서 왜 필요한가

동시성 버그는 재현이 어렵습니다. 처음부터 작은 동기화 도구를 정확히 골라야 합니다.
