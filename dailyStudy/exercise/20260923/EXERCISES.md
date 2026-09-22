# Beginner → Pro 연습문제

각 단계는 제공 상태의 성공 기준을 깨지 않도록 작은 단위로 진행합니다. 수정할 때마다 아래 공통 명령을 실행하세요.

```powershell
dotnet build .\dailyStudy\exercise\20260923\src\ConditionalCatalogApi\ConditionalCatalogApi.csproj -c Release --nologo
dotnet run --project .\dailyStudy\exercise\20260923\src\ConditionalCatalogApi\ConditionalCatalogApi.csproj -c Release --no-build -- --self-test
```

## 1. Beginner — 이름 길이 경계 테스트 추가

### 목표

이미 있는 Domain 규칙을 바꾸지 않고 경계값 테스트를 한 개 추가해 `string`, 반복, Result 검사를 익힙니다.

### 수정 위치

- [`SelfTestRunner.cs`](./src/ConditionalCatalogApi/SelfTesting/SelfTestRunner.cs)

### 할 일

1. 81글자 문자열을 만듭니다. `new string('가', 81)`을 사용할 수 있습니다.
2. 그 이름과 유효한 가격으로 `CatalogItem.Create`를 호출합니다.
3. 실패하고 오류 코드가 `NAME_LENGTH`인지 `Expect`로 확인합니다.
4. 현재 `TotalChecks`에 1을 더합니다.

### 성공 조건

- Release build 경고 0, 오류 0
- 자체 검증의 통과 수와 전체 수가 모두 1 증가함
- 80글자는 성공하고 81글자는 실패하는 이유를 설명할 수 있음

## 2. Junior — 상품 목록 조회와 LINQ 정렬

### 목표

Repository Port를 확장하고 LINQ를 실제 읽기 모델에 사용하되 계층 의존 방향을 유지합니다.

### 수정 위치

- [`ICatalogRepository.cs`](./src/ConditionalCatalogApi/Application/Ports/ICatalogRepository.cs)
- [`InMemoryCatalogRepository.cs`](./src/ConditionalCatalogApi/Infrastructure/InMemoryCatalogRepository.cs)
- [`CatalogApplicationService.cs`](./src/ConditionalCatalogApi/Application/CatalogApplicationService.cs)
- [`CatalogEndpoints.cs`](./src/ConditionalCatalogApi/Presentation/CatalogEndpoints.cs)
- [`SeedData.cs`](./src/ConditionalCatalogApi/Infrastructure/SeedData.cs)
- [`SelfTestRunner.cs`](./src/ConditionalCatalogApi/SelfTesting/SelfTestRunner.cs)
- [`verify-http.ps1`](./verify-http.ps1)

### 할 일

1. seed 상품을 두 개 이상으로 늘립니다.
2. Repository Port에 모든 snapshot을 읽는 비동기 메서드를 추가합니다.
3. 메모리 Adapter는 `lock` 안에서 collection을 복사해 외부에 내부 `Dictionary`를 노출하지 않습니다.
4. Application Service에서 이름 기준 `OrderBy`를 한 번만 적용해 불변 목록으로 반환합니다.
5. `GET /catalog` endpoint를 추가하고 response DTO 목록으로 바꿉니다.
6. 자체 검증과 HTTP script에 정렬 순서를 확인하는 검사를 추가합니다.

### 성공 조건

- 목록이 이름 오름차순으로 결정적임
- 반환 목록을 바꿔도 Repository 내부 상태가 바뀌지 않음
- LINQ 열거가 어느 계층에서 몇 번 일어나는지 설명할 수 있음
- 기존 단건 ETag 검증도 모두 계속 통과함

## 3. Senior — 조건부 DELETE 추가

### 목표

PATCH에서 배운 쓰기 전제 조건을 삭제에도 적용하고 Port 계약과 HTTP 상태를 일관되게 설계합니다.

### 수정 위치

- [`ICatalogRepository.cs`](./src/ConditionalCatalogApi/Application/Ports/ICatalogRepository.cs)
- [`InMemoryCatalogRepository.cs`](./src/ConditionalCatalogApi/Infrastructure/InMemoryCatalogRepository.cs)
- [`CatalogApplicationService.cs`](./src/ConditionalCatalogApi/Application/CatalogApplicationService.cs)
- [`CatalogEndpoints.cs`](./src/ConditionalCatalogApi/Presentation/CatalogEndpoints.cs)
- [`SelfTestRunner.cs`](./src/ConditionalCatalogApi/SelfTesting/SelfTestRunner.cs)
- [`verify-http.ps1`](./verify-http.ps1)

### 할 일

1. Repository에 `expectedVersion`과 현재 버전을 같은 `lock` 안에서 비교한 뒤 삭제하는 계약을 추가합니다.
2. Application Service가 삭제 성공·없음·버전 충돌을 명시적인 결과로 반환하게 합니다.
3. `DELETE /catalog/{id}`는 `If-Match` 누락 428, 문법 오류 400, 불일치 412, 성공 204를 반환하게 합니다.
4. weak ETag는 거절하고 strong 목록과 `*`는 기존 Strategy로 처리합니다.
5. 성공한 204에는 JSON 본문을 보내지 않고, 이후 GET이 404인지 검증합니다.

### 성공 조건

- 버전 비교와 삭제가 한 임계 구역에서 원자적임
- 오래된 삭제가 최신 상품을 지우지 않음
- 204 응답 본문이 비어 있음
- 새 정상·충돌·누락·문법·weak 경계 검사가 모두 통과함

## 4. Pro — 강제로 겹치는 두 수정의 race 테스트

### 목표

“우연히 빠르게 실행해서 통과하는 테스트”가 아니라 두 writer가 같은 version을 읽도록 제어한 결정적 동시성 테스트를 만듭니다.

### 수정 위치

- [`SelfTestRunner.cs`](./src/ConditionalCatalogApi/SelfTesting/SelfTestRunner.cs)
- 필요하면 같은 파일 안의 private 테스트 double

### 할 일

1. `ICatalogRepository`를 구현하는 테스트 double을 만듭니다.
2. 두 `FindAsync` 호출이 모두 version 1을 읽을 때까지 `TaskCompletionSource` 또는 barrier로 저장을 잠시 막습니다.
3. 서로 다른 가격을 쓰는 `UpdateAsync` 두 개를 동시에 시작합니다.
4. 저장 gate를 열고 결과를 모읍니다.
5. 결과가 정확히 `Updated` 하나, `VersionConflict` 하나인지 확인합니다.
6. 최종 저장 version이 2이고 성공한 writer의 값과 같은지 확인합니다.
7. timeout을 두어 테스트 자체가 무한 대기하지 않게 합니다.

### 성공 조건

- 20회 반복해도 매번 성공 1개·충돌 1개임
- `Thread.Sleep`에 의존하지 않고 명시적 동기화로 순서를 제어함
- 취소나 timeout 시 기다리는 두 Task가 모두 정리됨
- endpoint 사전 검사와 Repository 최종 compare-and-swap의 역할 차이를 테스트로 설명할 수 있음

## 마무리 질문

- 어느 연습에서 Domain 규칙이 바뀌었고, 어느 연습은 Application 또는 Adapter만 바뀌었나요?
- interface를 추가한 곳마다 정말 교체 가치나 I/O 경계가 있었나요?
- 새 HTTP 상태마다 client가 취해야 할 다음 행동이 명확한가요?
- concurrency test가 실패하면 재현 가능한 메시지와 종료 경로가 있나요?
