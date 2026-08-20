# 실습 문제

## 1단계: 문법 익히기

샘플 키를 하나 추가하세요. ID는 `KEY-105`, 담당 팀은 `null`, 오늘로부터 5일 뒤 만료, 유출 의심은 `false`로 설정하고 출력 결과를 예상하세요.

## 2단계: LINQ 연습

`RotationSummary`에 `PlatformCount`를 추가하세요. `OwnerTeam == "platform"`인 계획 수를 LINQ `Count`로 계산합니다.

## 3단계: Strategy 확장

모든 키를 생성 60일 뒤 예약 교체하는 `StrictRotationPolicy`를 만드세요. `PlanApiKeyRotationsService`는 수정하지 않고 Composition Root에서 정책만 교체합니다.

## 4단계: 실패와 테스트

빈 저장소를 서비스에 전달했을 때 실패 `Result`가 반환되는 자체 테스트를 추가하세요. 왜 이 경우가 예외보다 Result에 적합한지 한 문장으로 설명하세요.

## 도전 과제

실제 비밀 저장소와 작업 큐를 사용한다고 가정하세요. 계획 저장은 성공했지만 큐 발행이 실패하는 문제를 설명하고 Outbox로 해결하는 의사 코드를 작성하세요. 로그에 넣으면 안 되는 값도 두 가지 적으세요.
