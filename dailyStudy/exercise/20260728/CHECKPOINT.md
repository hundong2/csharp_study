# 초보자 검증 단계

## 1단계: 실행 확인

- 일반 실행에서 재시도 후 성공, 비활성 구독, 없는 구독이 각각 출력되는가?
- `--self-test`에서 `PASS` 네 줄이 나오고 종료 코드가 0인가?

## 2단계: 코드 읽기

1. `WebhookSubscription?`의 `?`는 무엇을 뜻하는가?
2. `subscription is null` 검사가 없으면 어떤 문제가 생기는가?
3. 명령과 영수증에 `record`를 사용한 이유는 무엇인가?
4. `await` 호출마다 `CancellationToken`을 전달하는 이유는 무엇인가?

## 3단계: 설계 말하기

1. 재시도 정책 변경 시 Strategy만 교체할 수 있는 이유를 설명한다.
2. Repository와 HTTP 인터페이스가 테스트를 쉽게 만드는 이유를 설명한다.
3. 비활성 구독은 Result이고 네트워크 장애는 예외인 이유를 설명한다.
4. Program이 Composition Root인 이유를 설명한다.

막히면 정답을 외우지 말고 `Program.cs`에서 실제 구현 한 줄을 찾아 근거로 답하세요.
