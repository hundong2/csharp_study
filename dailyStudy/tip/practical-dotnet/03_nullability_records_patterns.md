# 03. Nullable, Record, Pattern Matching

현대 C# 코드에서는 nullable reference type, record, pattern matching을 자주 사용합니다. DTO, 설정 객체, 결과 모델에서 특히 많이 보입니다.

## 핵심 개념

- `string?`: null일 수 있는 문자열입니다.
- `required`: 객체 초기화 시 반드시 값을 넣어야 합니다.
- `record`: 값 비교와 출력이 편한 데이터 모델입니다.
- `is null`, `is not null`: null 패턴 검사입니다.
- `switch expression`: 값을 조건에 따라 다른 값으로 변환합니다.

## 실무에서 왜 필요한가

외부 API나 DB 데이터는 null이 섞여 들어옵니다. null 가능성을 코드에 표시하면 실수를 줄일 수 있습니다. record는 DTO나 이벤트 메시지처럼 “데이터 묶음”에 적합합니다.
