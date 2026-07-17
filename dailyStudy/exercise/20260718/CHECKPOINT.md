# 초보자 검증과 복습 체크리스트

## 1. 실행 검증

```powershell
dotnet build .\src\InventoryExercise\InventoryExercise.csproj
dotnet run --project .\src\InventoryExercise\InventoryExercise.csproj
dotnet run --project .\src\InventoryExercise\InventoryExercise.csproj -- --self-test
```

다음 세 가지를 눈으로 확인하세요.

- 빌드가 경고와 오류 없이 끝난다.
- 일반 실행에서 예약 수량 3개와 남은 재고 4개가 보인다.
- 자동 검증 마지막에 `초보자 검증 통과 (5/5)`가 보인다.

실패하면 가장 위의 오류 한 줄부터 읽으세요. 파일명과 줄 번호로 이동한 뒤, 괄호·따옴표·세미콜론과 변수 이름을 먼저 확인하면 됩니다.

## 2. 말로 답하기

1. `int`와 `int?`는 무엇이 다른가요?
2. `switch` 식은 오늘 어떤 값을 계산하나요?
3. `Result<T>`가 예외 대신 표현하는 실패 두 가지를 찾아보세요.
4. `InventoryService`와 `InventoryItem`은 각각 어떤 책임을 가지나요?
5. Repository 인터페이스가 없으면 DB를 바꿀 때 어떤 코드가 영향을 받나요?
6. 같은 `RequestId`를 두 번 보내도 재고가 한 번만 줄어드는 흐름을 설명해 보세요.
7. `CancellationToken`은 기다리는 작업에 왜 전달하나요?

## 3. concise review checklist

- [ ] 변수, nullable, 조건, 반복, 컬렉션을 코드에서 찾을 수 있다.
- [ ] `record`와 `with`가 새 값을 만드는 방식을 설명할 수 있다.
- [ ] 입력 검증과 도메인 규칙의 위치가 다른 이유를 안다.
- [ ] DI와 Composition Root의 역할을 설명할 수 있다.
- [ ] Repository 구현을 EF Core로 교체할 지점을 찾을 수 있다.
- [ ] 정상·중복·부족·잘못된 입력 경로를 직접 실행했다.
- [ ] 안정 기능과 프리뷰 기능을 구분할 수 있다.
