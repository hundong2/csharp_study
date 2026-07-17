# 2026-07-18 C# 기초와 재고 예약 아키텍처 실습

오늘의 목표는 **C#을 처음 배우는 개발자가 기본 문법을 읽고, 재고 예약 요청이 검증·조회·변경·저장되는 흐름을 직접 실행하며 이해하는 것**입니다. 작은 콘솔 앱 안에 실무에서 자주 쓰는 Application Service, Domain Model, Repository, 생성자 의존성 주입(DI), Composition Root, Result 패턴과 중복 요청 방지(idempotency)를 담았습니다.

실행 예제는 설치된 안정 SDK `.NET 10.0.301`에서 컴파일되는 `net10.0`/C# 14 코드입니다. 최신 안정 패치는 10.0.10과 SDK 10.0.302이지만, 로컬 런타임 10.0.9에서도 이 예제는 동작합니다. 프리뷰 기능은 실행 코드와 분리했습니다.

## 먼저 실행하기

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260718
dotnet run --project .\src\InventoryExercise\InventoryExercise.csproj
dotnet run --project .\src\InventoryExercise\InventoryExercise.csproj -- --self-test
```

첫 명령은 문법 예제와 정상 재고 예약을 보여 줍니다. 두 번째 명령은 정상 예약, 같은 요청의 재시도, 재고 부족, 잘못된 수량, 실패 시 재고 보존을 자동 확인합니다. 성공하면 `초보자 검증 통과 (5/5)`가 보입니다.

## 오늘 배우는 기본 문법

| 문법 | 코드에서 찾을 곳 | 기억할 점 |
| --- | --- | --- |
| 기본 타입과 nullable | `string`, `int`, `bool`, `decimal?` | 값의 종류를 타입으로 표현하고, `?`는 값이 없을 수 있음을 나타냅니다. |
| 문자열 보간 | `$"현재 {Stock}개"` | `$` 문자열로 변수 값을 읽기 좋은 문장에 넣습니다. |
| 조건과 패턴 | `if`, 삼항 연산자, `switch` 식 | 간단한 양자택일은 삼항 연산자, 여러 값 분기는 `switch`가 읽기 쉽습니다. |
| 컬렉션과 반복 | 배열, `List<T>`, `Dictionary<TKey,TValue>`, `foreach` | 같은 타입의 여러 값을 저장하고 하나씩 처리합니다. |
| null 처리 | `optionalDiscountRate ?? 0m`, `InventoryItem?` | 값이 없을 수 있으면 확인하거나 기본값을 정합니다. |
| record와 `with` | `InventoryItem`, `this with { Stock = ... }` | 데이터 묶음을 표현하고 원본 대신 변경된 새 값을 만듭니다. |
| 인터페이스 | `IInventoryRepository` | 구현 기술이 아니라 필요한 동작의 계약에 의존합니다. |
| 비동기 | `async`, `await`, `ValueTask`, `CancellationToken` | DB나 외부 API처럼 기다리는 작업의 실무 기본 형태입니다. |
| LINQ | `Sum()`, `ToDictionary()` | 컬렉션의 합계 계산이나 변환을 간결하게 표현합니다. |

처음에는 `BasicSyntaxTour`의 변수 값을 바꾸고 출력을 예상해 보세요. 그다음 `InventoryDemo` → `CompositionRoot` → `InventoryService.ReserveAsync` → `InventoryItem.Reserve` → Repository 순서로 읽으면 호출 흐름을 따라가기 쉽습니다.

## 아키텍처 흐름

```text
Program
  -> CompositionRoot (구현 선택과 객체 연결)
  -> InventoryService (유스케이스 순서 조정)
     -> 입력 검증
     -> IReservationRepository (중복 요청 확인)
     -> IInventoryRepository (재고 조회/저장)
     -> InventoryItem.Reserve (업무 규칙)
     -> Reservation 저장
```

- **Application Service**는 한 유스케이스의 순서를 조정합니다. 저장 세부 기술과 재고 계산 규칙은 직접 구현하지 않습니다.
- **Domain Model**인 `InventoryItem`은 “판매 중이어야 하고 요청량이 재고 이하여야 한다”는 규칙을 데이터 가까이에 둡니다.
- **Repository 패턴**은 오늘의 메모리 저장소를 나중의 EF Core/SQL 구현으로 바꿔도 서비스 흐름을 유지하게 합니다.
- **생성자 DI**는 서비스가 필요한 계약을 외부에서 받게 하여 교체와 테스트를 쉽게 만듭니다.
- **Composition Root**는 구현을 선택하고 연결하는 책임을 한곳에 모읍니다. ASP.NET Core에서는 보통 `Program.cs`의 `builder.Services`가 이 역할을 합니다.
- **Result 패턴**은 재고 부족처럼 예상 가능한 실패를 예외가 아닌 명시적 결과로 전달합니다.
- **Idempotency**는 같은 `RequestId`가 재전송될 때 재고를 두 번 줄이지 않고 기존 예약을 반환합니다. 네트워크 재시도가 있는 API에서 중요한 실무 규칙입니다.

## 파일 안내

| 파일 | 용도 |
| --- | --- |
| [src/InventoryExercise/Program.cs](./src/InventoryExercise/Program.cs) | 실행 가능한 문법 예제, 재고 예약 아키텍처, 자동 검증 |
| [src/InventoryExercise/InventoryExercise.csproj](./src/InventoryExercise/InventoryExercise.csproj) | .NET 10/C# 14 프로젝트 설정 |
| [EXERCISES.md](./EXERCISES.md) | 작은 변경부터 취소 기능까지 단계별 실습 |
| [CHECKPOINT.md](./CHECKPOINT.md) | 초보자 실행 확인, 말로 답하기, 복습 체크리스트 |

## 버전 업데이트 (2026-07-18 확인)

- Microsoft의 [.NET 2026년 7월 서비스 업데이트](https://devblogs.microsoft.com/dotnet/dotnet-and-dotnet-framework-july-2026-servicing-updates/)에 따르면 최신 안정 .NET 10 패치는 `10.0.10`이며 보안 및 비보안 수정이 포함됩니다.
- 공식 [.NET 10 다운로드 페이지](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)는 2026-07-14 기준 SDK `10.0.302`, 런타임 `10.0.10`, 언어 C# 14를 안내합니다. 이 PC에는 안정 SDK `10.0.301`과 런타임 `10.0.9`가 있으므로 예제는 그 환경에서 검증했으며, 실제 개발 환경은 최신 보안 패치로 갱신하는 것이 좋습니다.
- [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)에는 비동기 DataAnnotations 검증, C# union의 `System.Text.Json` 직렬화, 스트림 어댑터 등의 시험 기능이 포함됩니다.
- Microsoft Learn의 [C# 15 새로운 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)은 C# 15가 프리뷰이며 union types와 collection expression arguments를 제공한다고 설명합니다. 로컬에는 .NET 11 Preview 6가 없으므로 이 기능은 실행 코드에 넣지 않았습니다. 안정 프로젝트에는 C# 14를 유지하고 별도 프리뷰 환경에서만 실험하세요.

## 완료 기준

- 일반 실행에서 기본 문법 출력과 예약 성공 결과를 확인했다.
- `--self-test`에서 다섯 검증 항목이 모두 통과했다.
- `InventoryService`와 `InventoryItem`의 책임 차이를 설명할 수 있다.
- 같은 `RequestId`가 재고를 두 번 줄이지 않는 이유를 설명할 수 있다.
- [CHECKPOINT.md](./CHECKPOINT.md)의 질문에 코드를 보지 않고 답할 수 있다.
