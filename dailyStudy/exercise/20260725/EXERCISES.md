# 단계별 실습

각 단계가 끝날 때마다 아래 명령으로 검증하세요.

```powershell
dotnet run --project .\src\IncidentRoutingExercise -- --self-test
```

## 1단계: 기본 문법 읽기

`CreateIncidentRequest`, `Incident.Create`, 맨 위의 `foreach`를 찾아 다음을 말로 설명하세요.

1. `string?`와 `string`은 무엇이 다른가?
2. `if`가 거짓이면 어느 코드가 건너뛰어지는가?
3. 메서드의 매개변수와 반환값은 무엇인가?

## 2단계: 직접 고치는 입문 과제

`Incident.Create`에 제목이 100자를 넘으면 `title_too_long` 실패를 반환하는 규칙을 추가하세요. 이후 검증 코드에 긴 제목 테스트를 한 개 더 추가하세요.

힌트:

```csharp
if (request.Title.Length > 100)
{
    return new Result<Incident>.Failure("title_too_long", "제목은 100자 이하여야 합니다.");
}
```

## 3단계: LINQ 연습

`IncidentSummary`에 `WarningCount`를 추가하고 `GetSummaryAsync`에서 `Count`와 람다식으로 계산하세요. 일반 실행 결과에도 경고 건수를 출력하세요.

## 4단계: Strategy 확장

`SecurityRoutingStrategy`를 만들어 팀이 `security`인 장애를 심각도와 관계없이 `pager-security`로 보냅니다. Composition Root에서 긴급 정책보다 앞에 배치하고 테스트를 추가하세요. 정책 순서가 왜 중요한지도 적어 보세요.

## 5단계: 운영 수준 사고하기

메모리 Repository의 중복 확인과 저장 사이에는 경쟁 조건이 있습니다. 실제 DB에서는 `CorrelationId` unique 제약과 트랜잭션을 사용해야 합니다. 다음 항목을 설계 메모로 작성하세요.

- 알림 API timeout과 제한된 재시도
- 저장 성공 후 알림 실패 시 outbox 처리
- 구조화 로그에 남길 필드와 숨길 개인정보
- 요청 취소와 서버 종료 시 처리
- 성공률, 지연 시간, 실패 코드별 메트릭
