# 07. System.Text.Json

`System.Text.Json`은 .NET 기본 JSON 라이브러리입니다. DTO, API 응답, 설정 파일 처리에서 자주 사용합니다.

## 핵심 개념

- `[JsonPropertyName]`: JSON 필드 이름을 지정합니다.
- `JsonSerializerOptions`: 대소문자, 숫자 처리, 포맷 등을 제어합니다.
- `JsonConverter<T>`: 특정 타입의 읽기/쓰기 방식을 직접 정의합니다.

## 실무에서 왜 필요한가

외부 API는 종종 숫자를 문자열로 보내거나 날짜 형식이 제각각입니다. 옵션과 컨버터를 알아야 통신 장애를 줄일 수 있습니다.
