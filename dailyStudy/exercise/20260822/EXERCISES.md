# 실습 문제

## 1단계: 문법 익히기

`REQ-105` 요청을 추가하세요. 이메일은 `park@example.com`, 계정 존재는 `true`, 최근 요청 수는 2, 요청 시각은 `now.AddMinutes(-2)`로 두고 결과를 예상하세요.

## 2단계: LINQ 연습

`ResetSummary`에 `SafeResponseCount`를 추가하세요. 차단이 아닌 항목 수를 LINQ `Count`로 계산하고 출력하세요.

## 3단계: Strategy 확장

최근 요청이 1건 이상이면 차단하는 `StrictResetPolicy`를 만드세요. `PlanPasswordResetsService`는 수정하지 않고 Composition Root에서 정책만 교체하세요.

## 4단계: 실패와 테스트

빈 저장소를 전달했을 때 실패 `Result`가 반환되는 자체 테스트를 추가하세요. 이 상황이 예외보다 Result에 적합한 이유를 한 문장으로 설명하세요.

## 도전 과제

계획 저장은 성공했지만 이메일 큐 발행이 실패했다고 가정하세요. Outbox로 원자성을 확보하는 의사 코드를 쓰고, 이메일 발송 멱등 키와 로그에서 제외할 민감정보 두 가지를 정하세요.
