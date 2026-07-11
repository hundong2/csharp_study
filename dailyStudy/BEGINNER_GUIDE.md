# DailyStudy 비기너 실습 가이드

이 폴더의 날짜별 리포트는 `Span`, `Interlocked`, `Pipe`, `MemoryMappedFile`, `Dynamic PGO`처럼 실무 고성능 주제를 많이 다룹니다. C# 기초가 약한 상태에서 바로 리포트 본문 코드를 실행하면 흐름을 따라가기 어렵기 때문에, 아래 순서로 실습하는 것을 권장합니다.

## 먼저 실행할 파일

```powershell
cd dailyStudy
dotnet script tip_BeginnerSyntaxTour.csx
```

이 파일은 날짜별 고급 예제를 보기 전에 알아야 하는 기본 문법을 짧게 훑습니다.

- 변수와 타입
- `if`, `foreach`
- 메서드
- 클래스와 객체
- 인터페이스
- 제네릭
- `async` / `await`
- `using`과 리소스 정리

## 날짜별 권장 실습 순서

각 날짜 폴더에서는 `tip_...csx` 파일을 먼저 실행한 뒤, 본문 예제를 실행하세요.

| 날짜 | 먼저 볼 파일 | 익히는 실무 패턴 |
|------|--------------|------------------|
| 20260630 | [tip_ResultPattern.csx](./20260630/tip_ResultPattern.csx) | Result 패턴, `TryParse`, 실패를 값으로 표현 |
| 20260702 | [tip_DisposableResourcePattern.csx](./20260702/tip_DisposableResourcePattern.csx) | `IDisposable`, `using`, 파일 리소스 정리 |
| 20260703 | [tip_OptionsValidation.csx](./20260703/tip_OptionsValidation.csx) | Options 객체, 설정 검증, `required`, `init` |
| 20260704 | [tip_RetryWithCancellation.csx](./20260704/tip_RetryWithCancellation.csx) | 재시도, `CancellationToken`, `try/catch` |
| 20260705 | [tip_ObjectPoolPattern.csx](./20260705/tip_ObjectPoolPattern.csx) | Object Pool, `ConcurrentBag`, GC 부담 줄이기 |
| 20260706 | [tip_BackgroundQueuePattern.csx](./20260706/tip_BackgroundQueuePattern.csx) | 백그라운드 큐, `SemaphoreSlim`, 비동기 작업 처리 |
| 20260707 | [tip_EventAggregatorPattern.csx](./20260707/tip_EventAggregatorPattern.csx) | Event Bus, 느슨한 결합, `Action<T>` |
| 20260708 | [tip_CircuitBreakerPattern.csx](./20260708/tip_CircuitBreakerPattern.csx) | Circuit Breaker, 장애 전파 차단 |
| 20260709 | [tip_BatchingPattern.csx](./20260709/tip_BatchingPattern.csx) | Batching, `yield return`, 묶음 처리 |
| 20260710 | [tip_RepositoryPattern.csx](./20260710/tip_RepositoryPattern.csx) | Repository, 인터페이스로 저장소 추상화 |

## 실습 방식

1. `tip_...csx` 파일을 먼저 읽습니다.
2. 주석을 보면서 `dotnet script`로 실행합니다.
3. 출력 결과를 확인합니다.
4. 같은 폴더의 날짜 리포트 Markdown을 읽습니다.
5. `1_...csx`, `2_...csx` 순서로 본문 예제를 실행합니다.

예시:

```powershell
cd dailyStudy/20260710
dotnet script tip_RepositoryPattern.csx
dotnet script 1_SequentialRingIngest.csx
dotnet script 2_TimeSeriesExtension.csx
```

## 자주 나오는 문법 요약

### `var`

오른쪽 값을 보고 컴파일러가 타입을 추론합니다.

```csharp
var name = "gateway"; // string으로 추론
var count = 10;       // int로 추론
```

처음 배울 때는 명시 타입을 써도 됩니다. 실무에서는 오른쪽 타입이 명확할 때 `var`를 자주 씁니다.

### `public`, `private`

접근 제한자입니다.

- `public`: 외부 코드에서 접근 가능
- `private`: 현재 클래스 내부에서만 접근 가능

### `class`와 `struct`

- `class`: 참조 타입입니다. 객체를 변수에 담으면 실제 데이터 위치를 가리킵니다.
- `struct`: 값 타입입니다. 작은 데이터 묶음에 적합합니다.

고성능 코드에서는 `struct`, `record struct`, `Span<T>`가 자주 등장하지만, 먼저 값 타입과 참조 타입의 차이를 이해해야 합니다.

### `interface`

구현 클래스가 반드시 제공해야 하는 기능 목록입니다. 비즈니스 로직이 구체 클래스가 아니라 인터페이스에 의존하면 테스트와 교체가 쉬워집니다.

### `async` / `await`

I/O 대기 중 스레드를 묶어 두지 않기 위한 문법입니다. DB, 파일, 네트워크 호출처럼 기다리는 시간이 있는 작업에 주로 사용합니다.

### `CancellationToken`

비동기 작업에 “이제 그만해도 된다”는 신호를 전달합니다. 웹 요청이 취소되었거나 timeout이 지난 경우 불필요한 작업을 멈출 수 있습니다.

### `Interlocked`

여러 스레드가 같은 값을 동시에 바꿀 때 사용하는 원자적 연산 API입니다. `lock`보다 작은 플래그 제어에 적합합니다.

### `Span<T>` / `ReadOnlySpan<T>`

배열이나 메모리 일부를 복사하지 않고 가리키는 타입입니다. 고성능 버퍼 처리에서 중요하지만, 처음에는 “복사 없이 범위를 넘기는 타입” 정도로 이해하고 시작하면 됩니다.

## 학습 기준

처음부터 런타임 내부 최적화까지 완전히 이해할 필요는 없습니다. 아래 순서로 이해하면 됩니다.

1. 코드가 실행되는가
2. 입력과 출력이 무엇인가
3. 어떤 문법이 쓰였는가
4. 왜 이 패턴이 실무에서 필요한가
5. 고성능 주제와 어떻게 연결되는가

이 순서가 잡히면 날짜별 고급 리포트의 `Interlocked`, `Span`, `MemoryMappedFile`, `JIT PGO` 같은 키워드도 훨씬 덜 낯설어집니다.
