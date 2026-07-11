# 03. Nullable, Record, Pattern Matching

현대 C# 코드에서는 nullable reference type, record, pattern matching을 자주 사용합니다. DTO, 설정 객체, 결과 모델에서 특히 많이 보입니다.

## 핵심 개념

- `string?`: null일 수 있는 문자열입니다.
- `required`: 객체 초기화 시 반드시 값을 넣어야 합니다.
- `record`: 값 비교와 출력이 편한 데이터 모델입니다.
- `record class`: 참조 타입입니다. DTO, 이벤트 메시지, API 응답 모델에 자주 씁니다.
- `record struct`: 값 타입입니다. 작고 자주 만들어지는 데이터 묶음에 쓸 수 있습니다.
- `readonly record struct`: 값 타입이면서 내부 상태를 바꾸지 못하게 막습니다.
- `is null`, `is not null`: null 패턴 검사입니다.
- `switch expression`: 값을 조건에 따라 다른 값으로 변환합니다.

## 실무에서 왜 필요한가

외부 API나 DB 데이터는 null이 섞여 들어옵니다. null 가능성을 코드에 표시하면 실수를 줄일 수 있습니다. record는 DTO나 이벤트 메시지처럼 “데이터 묶음”에 적합합니다.

## 메모리 관점

- `record class`는 힙에 객체가 만들어지고 변수에는 참조가 들어갑니다.
- `record struct`는 값 타입이라 변수 자체에 값이 들어갑니다. 작은 구조체에는 좋지만, 큰 구조체를 자주 복사하면 오히려 비용이 커집니다.
- `readonly record struct`는 값이 바뀌지 않는다는 의도가 명확해서, 실수로 상태를 변경하는 코드를 줄일 수 있습니다.
- DTO가 크거나 여러 곳에 공유된다면 `record class`, 작고 불변인 숫자/식별자 값이라면 `readonly record struct`를 검토합니다.

## 선택 기준

| 상황 | 추천 |
|------|------|
| API 응답 DTO | `record class` 또는 `sealed record` |
| ID, Money, Coordinate 같은 작은 값 객체 | `readonly record struct` |
| Entity처럼 수명과 변경이 있는 객체 | 일반 `class` |
| 대량 배열에 저장되는 작은 값 | `readonly struct` 또는 `readonly record struct` |
