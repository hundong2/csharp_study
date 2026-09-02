# 2026-09-03 초보자 이해도 검증 단계

코드를 보지 않고 먼저 답한 뒤, 파일과 실행 결과를 열어 스스로 채점하세요.

## A. 기본 문법

- [ ] `var`, `new`, `if`, `foreach`, `return`, 문자열 보간을 코드에서 각각 찾고 한 문장으로 설명한다.
- [ ] `record`, `enum`, `interface`, 제네릭 `<T>`, nullable `?`, 식 본문 `=>`의 역할을 말한다.
- [ ] `yield return`을 호출하는 순간 전체 결과가 만들어지지 않는 이유를 설명한다.
- [ ] `IEnumerable<T>`와 `IAsyncEnumerable<T>`가 다음 원소를 받는 방식의 차이를 설명한다.
- [ ] `await foreach`, `[EnumeratorCancellation]`, `CancellationToken`이 함께 필요한 이유를 말한다.

## B. 설계와 실무

- [ ] Domain Model, Application Service, Strategy, Repository, Port/Adapter, Composition Root를 실제 클래스와 연결한다.
- [ ] 생성자 DI가 테스트 대역 교체와 SOLID의 DIP에 어떤 도움을 주는지 설명한다.
- [ ] 잘못된 센서 값은 `Result<T>`, 잘못된 임계값 구성은 예외로 처리한 이유를 설명한다.
- [ ] `SensorId:Sequence` 멱등성 키가 재전송 중복을 막는 과정을 설명한다.
- [ ] 느린 소비자, 순서, 재처리, checkpoint, bounded buffer, Outbox 중 운영에서 필요한 항목을 세 가지 고른다.

## C. 직접 검증

```powershell
dotnet build ./src/SensorStreamingExercise/SensorStreamingExercise.csproj
dotnet run --project ./src/SensorStreamingExercise
dotnet run --project ./src/SensorStreamingExercise -- --self-test
```

- [ ] 빌드가 경고 0개, 오류 0개로 성공한다.
- [ ] 기본 실행에서 처리 5건, 새 경고 3건, 주의 2건, 심각 1건을 확인한다.
- [ ] 자체 테스트가 `5/5 통과`한다.
- [ ] 모든 직접 작성 메서드 위에 목적·매개변수·반환값 한글 설명이 있음을 확인한다.
