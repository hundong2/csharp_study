# 2026-07-14 C# 실무 아키텍처와 기본 문법 실습

오늘의 목표는 **기초 문법을 모르는 개발자도 C# 콘솔 앱을 실행하며 주문 처리 서비스 구조를 설명할 수 있게 되는 것**입니다. 예제는 안정적으로 실행되도록 설치된 .NET 10 SDK와 C# 14 범위에서 작성했고, preview 기능은 별도 메모로 분리했습니다.

## 실행 방법

```powershell
cd D:\workspace\csharp_study\dailyStudy\exercise\20260714
dotnet run --project .\src\OrderProcessingExercise\OrderProcessingExercise.csproj
dotnet run --project .\src\OrderProcessingExercise\OrderProcessingExercise.csproj -- --self-test
```

첫 번째 명령은 학습 데모를 실행합니다. 두 번째 명령은 자료가 초보자에게 필요한 핵심 흐름을 검증할 수 있도록 자체 테스트를 실행합니다.

## 오늘 배우는 것

| 구분 | 핵심 내용 | 실무 연결 |
| --- | --- | --- |
| 기본 문법 | 변수, 조건문, 반복문, 컬렉션, 메서드, 람다, nullable | API 입력 검증과 데이터 가공 |
| 타입 설계 | `class`, `record`, `record struct`, `enum`, 인터페이스 | 도메인 모델과 계약 분리 |
| 비동기 | `async`, `await`, `CancellationToken`, `ValueTask` | DB, 결제, 외부 API 호출 |
| 아키텍처 | Domain / Application / Infrastructure 계층 | 테스트 가능한 주문 처리 서비스 |
| 오류 처리 | 예외 대신 `Result<T>`로 실패를 값으로 표현 | 사용자에게 복구 가능한 실패 메시지 전달 |
| 최신 기능 | .NET 11 Preview 5, C# 15 preview 메모 | preview 기능은 실무 도입 전 격리 평가 |

## 파일 구성

| 파일 | 용도 |
| --- | --- |
| [src/OrderProcessingExercise/Program.cs](./src/OrderProcessingExercise/Program.cs) | 실행 가능한 학습 예제와 자체 검증 코드 |
| [src/OrderProcessingExercise/OrderProcessingExercise.csproj](./src/OrderProcessingExercise/OrderProcessingExercise.csproj) | .NET 10 콘솔 프로젝트 설정 |
| [EXERCISES.md](./EXERCISES.md) | 직접 손으로 바꿔 보는 실습 문제 |
| [CHECKPOINT.md](./CHECKPOINT.md) | 초보자 완전 학습 검증표와 정답 |

## 실무 아키텍처 요약

이번 예제는 작은 콘솔 앱 안에 세 계층을 나눠 넣었습니다.

1. Domain: `Order`, `OrderLine`, `Money`, `OrderStatus`
2. Application: `OrderService`, `CreateOrderCommand`, `Result<T>`
3. Infrastructure: `InMemoryProductCatalog`, `InMemoryInventoryGateway`, `FakePaymentGateway`, `InMemoryOrderRepository`

핵심 감각은 간단합니다. 도메인 모델은 업무 규칙을 알고, 애플리케이션 서비스는 흐름을 조립하고, 인프라는 외부 저장소나 결제처럼 바뀌기 쉬운 세부 구현을 담당합니다.

## 최신 버전 메모

- Microsoft Learn의 .NET 11 문서는 2026-06-11 기준 Preview 5까지 업데이트되어 있으며, .NET 11은 2026년 11월 최종 릴리스를 목표로 하는 preview입니다. 주요 항목은 Runtime Async, JIT 개선, Process API, System.Text.Json 개선, OpenTelemetry MemoryCache metrics, LINQ `FullJoin` 등입니다. 참고: [What's new in .NET 11](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview)
- C# 15 preview는 .NET 11 preview SDK에서 사용할 수 있고, 문서 기준 기능은 collection expression arguments, union types, closed hierarchies, memory safety입니다. 참고: [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)
- 언어 버전 매핑은 .NET 10 = C# 14, .NET 11 = C# 15입니다. 실무 프로젝트에서는 target framework와 맞지 않는 `LangVersion`을 억지로 올리지 않는 것이 안전합니다. 참고: [C# language versioning](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-versioning)

## Preview 기능을 실무에서 보는 법

C# 15의 union types는 `Result<T>`나 상태 모델링을 더 명확하게 만들 가능성이 큽니다. 다만 현재 실습 프로젝트는 .NET 10으로 빌드되므로 preview 문법을 직접 사용하지 않습니다. 오늘은 `Result<T>` record로 같은 문제를 안정 버전에서 표현하고, preview 기능은 `CHECKPOINT.md`에서 비교 질문으로 다룹니다.

## 완료 기준

- `dotnet run`으로 학습 데모가 실행된다.
- `dotnet run -- --self-test`가 모든 검증을 통과한다.
- `CHECKPOINT.md`의 기초 문법, 아키텍처, async, 오류 처리 문항을 말로 설명할 수 있다.
- `EXERCISES.md`의 1단계와 2단계를 직접 수정해 보고 다시 self-test를 통과시킨다.

