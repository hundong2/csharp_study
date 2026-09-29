# 2026-09-30 실행형 연습문제 — 오류 경계를 직접 바꾸기

각 단계는 앞 단계를 완료했다고 가정합니다. 변경할 때마다 아래 회귀 명령을 실행하세요.

```powershell
dotnet build .\src\OrderIntakeApi\OrderIntakeApi.csproj -c Release
dotnet run --project .\src\OrderIntakeApi\OrderIntakeApi.csproj -c Release --no-build -- --self-test
.\verify-http.ps1
```

> 실수해도 괜찮습니다. 먼저 실패를 재현하고, 한 가지 계약만 고친 뒤 다시 실행하는 습관이 목표입니다.

---

## Level 1 — Beginner: 세 실패 흐름 관찰하기

### 목표

코드를 바꾸기 전에 Result, 예외, 404 status page가 서로 다른 경로로 같은 Problem Details 모양을 만드는지 관찰합니다.

### 실행

1. README의 “직접 서버 실행” 명령으로 Development 서버를 띄웁니다.
2. 상태 변경 요청 한 번과 오류를 만드는 요청 세 번을 차례로 보냅니다.

```powershell
$baseUrl = 'http://127.0.0.1:50930'

$invalid = @{
  orderId = 'bad id'
  customerId = 'customer-1'
  items = @(@{ sku = 'BOOK-CS'; quantity = 1 })
} | ConvertTo-Json -Depth 4

Invoke-WebRequest -Uri "$baseUrl/orders" -Method Post -ContentType 'application/json' -Body $invalid -SkipHttpErrorCheck
Invoke-WebRequest -Uri "$baseUrl/no-route" -SkipHttpErrorCheck
Invoke-WebRequest -Uri "$baseUrl/demo/catalog/unavailable" -Method Put
Invoke-WebRequest -Uri "$baseUrl/orders" -Method Post -ContentType 'application/json' -Body ($invalid -replace 'bad id', 'order-101') -SkipHttpErrorCheck
```

3. 200인 상태 변경 응답은 제외하고, 세 **오류 응답**의 `status`, `code`, `traceId`, `instance`를 표로 적습니다.

### 완료 기준

- 400은 `OrderHttpMapper`, route 404는 Status Code Pages, 503은 `ApiExceptionHandler`가 만들었다고 말할 수 있습니다.
- 세 응답 모두 같은 공통 필드를 가진다는 것을 확인했습니다.

---

## Level 2 — Beginner+: 무료 배송 경계 변경하기

### 목표

Strategy를 바꾸면 어느 테스트가 계약 변경을 알려 주는지 경험합니다.

### 작업

1. `ThresholdShippingFeePolicy.FreeShippingThreshold`를 `40_000m`으로 바꿉니다.
2. self-test를 실행해 기존 “49,999원은 유료” assertion이 실패하는지 확인합니다.
3. 새 정책에 맞게 테스트 경계값을 `39_999m`과 `40_000m`으로 바꿉니다.
4. README의 정책 설명과 self-test 경계값 assertion도 함께 고칩니다.

### 완료 기준

- 구현만 바꾸고 문서·테스트를 그대로 두는 것이 왜 위험한지 설명할 수 있습니다.
- `39,999 → 3,000`, `40,000 → 0` 경계를 자동 검증합니다.

---

## Level 3 — Intermediate: 주문 총수량 제한 Result 추가하기

### 목표

예상 가능한 업무 실패를 예외가 아닌 Result로 추가합니다.

### 요구사항

- 한 주문의 총수량은 120개 이하여야 합니다.
- 초과 시 HTTP 400과 `order.total_quantity.exceeded`를 반환합니다.
- 카탈로그를 호출하기 **전에** 실패해야 합니다.

### 구현 힌트

`OrderDraft.Create`에서 줄 검증 뒤 다음 계산을 추가할 수 있습니다.

```csharp
var totalQuantity = lineArray.Sum(line => line.Quantity);
```

그 뒤 `Result<OrderDraft>.Failure(...)`를 반환하세요. `OrderHttpMapper`는 `order.` 접두사를 이미 400으로 처리합니다.

### 테스트

- 60개씩 서로 다른 SKU 두 줄은 성공 경계입니다.
- 61개와 60개는 400입니다.
- `DemoProductCatalog`에 호출 횟수를 추가해 초과 요청에서 0인지 확인하면 Pro 수준 검증입니다.

### 완료 기준

- 새 실패가 handler의 500을 거치지 않습니다.
- Problem Details에 `code`, `traceId`, `instance`가 모두 있습니다.

---

## Level 4 — Intermediate+: 별도 품절 Result 추가하기

### 목표

“상품 자체가 없음”과 “상품은 있지만 재고가 없음”을 다른 안정 코드로 표현합니다.

### 요구사항

- `IInventoryGateway` Port를 만듭니다.
- 학습용 Adapter는 SKU별 가용 수량을 반환합니다.
- 부족하면 `inventory.insufficient` Result와 HTTP 409를 반환합니다.
- 재고 Adapter 일시 장애는 Result가 아니라 별도 예외로 남깁니다.

### 설계 질문

1. 재고 부족은 사용자가 수량을 바꾸면 해결할 수 있으므로 예상 실패인가요?
2. 재고 서버 연결 실패는 사용자가 입력을 고쳐 해결할 수 있나요?
3. `OrderHttpMapper`에서 `inventory.insufficient`를 명시적으로 409에 추가해야 하나요?

### 완료 기준

- 품절과 인프라 장애가 서로 다른 HTTP/오류 코드로 보입니다.
- Application Service는 구체 Adapter가 아니라 `IInventoryGateway`에 의존합니다.

---

## Level 5 — Advanced: handler 책임 연쇄 만들기

### 목표

여러 `IExceptionHandler`가 등록 순서대로 기회를 받는 구조를 실습합니다.

### 작업

1. `CatalogExceptionHandler`를 새로 만들어 `ProductCatalogUnavailableException`만 처리하고 그 밖에는 `false`를 반환합니다.
2. 기존 handler는 `UnexpectedExceptionHandler`로 이름을 바꾸고 알 수 없는 예외만 500으로 처리합니다.
3. `Program.cs`에 구체적인 handler를 먼저, catch-all handler를 나중에 등록합니다.

```csharp
builder.Services.AddExceptionHandler<CatalogExceptionHandler>();
builder.Services.AddExceptionHandler<UnexpectedExceptionHandler>();
```

4. 두 handler가 모두 같은 예외를 기록하지 않는지 로그를 확인합니다.

### 완료 기준

- 알려진 예외는 첫 handler에서 `true`, 알 수 없는 예외는 첫 handler `false` 후 두 번째 handler `true`입니다.
- 등록 순서를 뒤집었을 때 왜 구체 handler가 실행되지 않는지 설명할 수 있습니다.

---

## Level 6 — Pro: 취소와 timeout을 분리 검증하기

### 목표

외부 호출 자체의 시간 예산과 클라이언트 취소 원인을 구분합니다.

### 요구사항

- `PlaceOrderService` 또는 별도 catalog decorator에 200ms 가격 조회 예산을 둡니다.
- `CancellationTokenSource.CreateLinkedTokenSource`로 상위 취소와 내부 timeout을 합칩니다.
- 내부 timeout만 발생하면 `ProductCatalogUnavailableException`으로 바꿔 503을 반환합니다.
- 상위 `RequestAborted`가 원인이면 `OperationCanceledException`을 그대로 전파합니다.

### 꼭 필요한 필터

```csharp
catch (OperationCanceledException) when (
    !cancellationToken.IsCancellationRequested &&
    timeoutCancellation.IsCancellationRequested)
{
    // 내부 budget만 끝난 경우
}
```

### 완료 기준

- 내부 timeout → 503 + `Retry-After`
- 상위 취소 → 500 Problem Details를 만들지 않음
- 느린 Adapter가 실제로 취소 토큰을 관찰했음을 assertion으로 확인

---

## Level 7 — Pro+: 운영 저장소와 멱등성 계약 설계하기

### 목표

학습용 메모리 Repository를 실제 DB 계약으로 옮길 때 필요한 원자성과 오류 매핑을 설계합니다.

### 요구사항

1. `orderId`에 unique constraint가 있는 테이블을 가정합니다.
2. “먼저 조회하고 나중에 insert”만 하는 구현의 race를 sequence diagram으로 그립니다.
3. unique constraint 위반만 `order.duplicate` Result로 번역합니다.
4. 연결 끊김, timeout, 알 수 없는 DB 예외는 중복 Result로 바꾸지 않습니다.
5. 동일 `orderId`인데 본문이 다른 경우 409로 거절할지, 원본 결과를 재생할지 API 계약을 문서화합니다.

### 완료 기준

- 원자적 제약이 Application의 사전 조회보다 최종 진실인 이유를 설명합니다.
- DB 예외 전체를 `catch (Exception) => duplicate`로 바꾸지 않습니다.
- retry 후 결과가 불명확한 “unknown commit outcome”을 어떻게 다룰지 적었습니다.

---

## 최종 제출 체크

- [ ] 모든 새 메서드에 무엇을 하는지, parameter 의미, 반환값을 설명하는 한글 주석이 있다.
- [ ] 처음 등장한 낯선 문법 바로 위에 쉬운 설명이 있다.
- [ ] 예상 실패, 예외, 취소의 경계가 문서와 코드에서 일치한다.
- [ ] 500 본문에 내부 예외 형식·message·stack trace가 없다.
- [ ] `dotnet build`가 경고 0, 오류 0이다.
- [ ] self-test와 실제 HTTP 검증이 통과한다.
- [ ] `bin/`, `obj/`가 Git stage에 없다.
