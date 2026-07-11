# 06. HttpClient와 JSON API

실무 백엔드에서는 외부 API 호출이 매우 흔합니다. `HttpClient`는 .NET 표준 HTTP 클라이언트입니다.

## 핵심 개념

- `HttpClient`: HTTP 요청을 보냅니다.
- `HttpRequestMessage`: 요청 메서드, URI, 헤더를 담습니다.
- `HttpResponseMessage`: 응답 상태 코드와 본문을 담습니다.
- `JsonSerializer`: JSON 문자열과 C# 객체를 변환합니다.

## 실무 주의점

실제 서비스에서는 `new HttpClient()`를 요청마다 만들지 않습니다. ASP.NET Core에서는 `IHttpClientFactory`를 주로 사용합니다. 이 예제는 외부 네트워크 없이 구조를 익히기 위해 fake handler를 사용합니다.
