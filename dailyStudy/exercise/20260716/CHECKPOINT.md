# 초보자 검증과 복습 체크리스트

## 1. 실행 검증

```powershell
dotnet build .\src\ExpenseApprovalExercise\ExpenseApprovalExercise.csproj
dotnet run --project .\src\ExpenseApprovalExercise\ExpenseApprovalExercise.csproj
dotnet run --project .\src\ExpenseApprovalExercise\ExpenseApprovalExercise.csproj -- --self-test
```

확인할 출력:

- 일반 실행에 `기본 문법 둘러보기`와 `비용 승인 파이프라인`이 보인다.
- 65만원 장비 신청의 승인 경로가 `TeamLead`다.
- 자체 검증 마지막 줄이 `초보자 검증 통과`로 시작한다.

## 2. 말로 답하기

1. `decimal amount = 85_000m;`에서 `_`와 `m`은 각각 어떤 역할인가요?
2. `string?`와 `string`의 차이는 무엇인가요?
3. `foreach`는 어떤 값을 하나씩 꺼내나요?
4. class와 record를 오늘 코드에서 어떻게 나눠 사용했나요?
5. 검증 규칙이 실패하면 파이프라인은 왜 즉시 멈추나요?
6. `ExpenseService`가 `InMemoryExpenseRepository`를 직접 만들지 않는 이유는 무엇인가요?
7. `CancellationToken`은 어떤 상황에서 유용한가요?
8. 예상 가능한 입력 오류를 `Result<T>`로 표현하면 호출자에게 어떤 장점이 있나요?

## 3. 막힐 때 확인 순서

1. 명령을 `20260716` 폴더에서 실행했는지 확인합니다.
2. 컴파일 오류의 첫 번째 메시지와 줄 번호만 먼저 봅니다.
3. 괄호 `()`, 중괄호 `{}`, 세미콜론 `;`이 빠지지 않았는지 봅니다.
4. enum에 값을 추가했다면 `switch`에도 경우를 추가했는지 봅니다.
5. 규칙 클래스를 만들었다면 `CompositionRoot`의 배열에 등록했는지 봅니다.
6. 그래도 어렵다면 마지막 변경만 되돌리고 자체 검증을 다시 실행합니다.

## 4. concise review checklist

- [ ] 변수, nullable, 조건, 반복, 컬렉션을 코드에서 찾을 수 있다.
- [ ] `async` / `await`의 기다리는 흐름을 설명할 수 있다.
- [ ] Domain / Application / Infrastructure 책임을 한 문장씩 말할 수 있다.
- [ ] DI, 검증 파이프라인, Repository가 무엇을 분리하는지 안다.
- [ ] 정상·영수증 누락·한도 초과 사례를 직접 실행했다.
- [ ] 안정 기능과 Preview 기능을 운영 코드에서 구분해야 하는 이유를 안다.
