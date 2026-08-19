# 실습 문제

## 1단계: 문법 익히기

`Program.cs`의 샘플 장애를 하나 추가하고 `foreach` 출력에서 담당 팀도 확인하세요. `IncidentSeverity.Medium`, 영향 사용자 50명, `ServiceHint`는 `null`로 지정합니다.

## 2단계: LINQ 연습

`TriageSummary`에 `PlatformCount` 계산 속성을 추가하세요. `Team == "platform"`인 배정 수를 `Count`로 계산합니다.

## 3단계: Strategy 확장

야간에는 `High` 장애도 `Immediate`로 올리는 `NightShiftPriorityPolicy`를 만드세요. 기존 `TriageIncidentsService`는 수정하지 않고 Composition Root에서 정책만 교체합니다.

## 4단계: 실패와 테스트

빈 저장소를 서비스에 전달하면 실패 `Result`가 반환되는 자체 테스트를 추가하세요. 예상 가능한 "처리 대상 없음"을 예외가 아닌 Result로 표현한 이유도 한 문장으로 적어 보세요.

## 도전 과제

실제 DB 저장을 가정해 같은 장애를 여러 작업자가 동시에 분류할 때 생기는 경쟁 조건을 설명하고, 고유 제약 조건과 낙관적 동시성 중 하나를 선택해 의사 코드로 표현하세요.
