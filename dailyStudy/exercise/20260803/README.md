# 2026-08-03 예약 만료 처리 C# 실습

## 초보자 읽는 순서

1. 이 문서의 **처음 만나는 문법**을 먼저 읽습니다.
2. `Program.cs` 맨 위 실행 코드와 `ExpireReservationsCommand`를 읽습니다.
3. `Reservation` → `DeadlineExpiryPolicy` → `ExpireReservationsService` 순서로 따라갑니다.
4. Repository, DI, Composition Root가 테스트하기 쉬운 구조를 만드는 이유를 확인합니다.
5. `EXERCISES.md`를 풀고 `CHECKPOINT.md`로 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260803/src/ReservationExpiryExercise
dotnet run
dotnet run -- --self-test
```

## 처음 만나는 문법과 기초 문법

- `var result = ...;`에서 `var`는 오른쪽 값으로 변수 형식을 추론하고, `;`은 문장의 끝입니다.
- 문자열은 `"..."`, 정수는 `100`, 시간 간격은 `TimeSpan.FromMinutes(30)`처럼 표현합니다.
- `string?`은 `null`을 허용합니다. 이 프로젝트의 `<Nullable>enable</Nullable>`은 실수로 null을 쓰는 문제를 컴파일 단계에서 경고합니다.
- `if`는 조건 분기, `foreach`는 모음의 각 항목 반복, `return`은 메서드 종료입니다. `is < 1 or > 1_000`은 범위 밖 값을 읽기 쉽게 검사하는 패턴입니다.
- 클래스는 데이터와 동작을 묶고, `interface`는 구현이 지켜야 할 계약입니다. 생성자 매개변수로 계약을 받는 것이 **의존성 주입(DI)**입니다.
- `record`는 값 중심 데이터에 적합합니다. `with` 식은 원본을 변경하지 않고 일부 값만 바꾼 복사본을 만들어 불변성을 돕습니다.
- `async Task<T>`와 `await`는 DB·네트워크 I/O 중 스레드를 붙잡지 않으며, `CancellationToken`은 안전한 종료 요청을 전달합니다.
- LINQ의 `Where`, `OrderBy`, `Take`, `ToArray`는 필터링·정렬·개수 제한·결과 생성을 선언적으로 표현합니다.

## 실무 설계 지도

- **Domain Model**: `Reservation`이 생성과 만료 상태 전이 규칙을 보호합니다.
- **Application Service**: `ExpireReservationsService`가 조회, 정책 판단, 저장의 유스케이스 순서만 조정합니다.
- **Strategy**: `IExpiryPolicy`로 만료 판단을 분리해 VIP 연장 정책 등을 서비스 수정 없이 추가할 수 있습니다.
- **Repository**: 저장 기술을 업무 규칙에서 분리합니다. 메모리 구현은 빠른 테스트에, EF Core 구현은 운영 DB에 사용할 수 있습니다.
- **DI와 Composition Root**: 서비스는 인터페이스에 의존하고 프로그램 시작 지점만 구현체를 조립합니다. 이는 SOLID의 SRP, OCP, DIP를 실천합니다.
- **Result와 예외**: 잘못된 입력이나 허용되지 않은 상태처럼 예상 가능한 실패는 `Result`, DB 단절처럼 예상 밖 운영 장애는 예외로 구분합니다.
- **nullable 안전성과 불변성**: null 가능성을 형식에 표시하고 record 복사본을 저장해 중간 상태와 동시성 실수를 줄입니다.
- **테스트 가능성**: 메모리 Repository와 조용한 로그를 주입하여 실제 DB나 콘솔에 의존하지 않고 경계값을 검사합니다.

## 운영 환경에서는 무엇을 더 생각할까?

여러 인스턴스가 같은 예약을 동시에 만료하지 않도록 낙관적 동시성 토큰이나 원자적 업데이트가 필요합니다. 배치 크기, 처리 지연, 성공·실패 건수를 metric과 trace로 관찰하고 구조화 로그에는 예약 ID를 남기되 개인정보는 피해야 합니다. 저장 재시도는 지수 백오프와 제한 횟수를 두고, 취소와 timeout을 전파해야 합니다. 만료 이벤트 발행까지 필요하다면 DB 저장과 메시지 발행 사이 유실을 막는 Outbox 패턴과 멱등 소비자를 고려합니다.

## 버전 업데이트 (2026-08-03 확인)

- 최신 안정 언어는 **C# 14**, 안정 플랫폼은 **.NET 10 LTS**입니다. .NET 10은 2028-11-10까지 지원됩니다.
- 로컬에는 안정 SDK **10.0.301**이 설치되어 있어 이 예제는 `net10.0`을 대상으로 빌드합니다.
- **C# 15 / .NET 11 Preview 6**은 프리뷰입니다. union types와 비동기 DataAnnotations 검증 같은 기능은 별도 실험 환경에서만 살펴보고, 오늘의 안정 예제에는 넣지 않았습니다.

공식 출처:

- [What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [Announcing .NET 10](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)
- [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [.NET 11 Preview 6 발표](https://devblogs.microsoft.com/dotnet/dotnet-11-preview-6/)
- [What's new in C# 15](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 완료 기준과 간단 복습 체크리스트

- [ ] `dotnet build`가 경고와 오류 없이 끝난다.
- [ ] 일반 실행 결과가 만료 1건, 유지 1건이다.
- [ ] self-test가 `4/4 통과`한다.
- [ ] `record`, nullable, LINQ, async/await를 한 문장씩 설명할 수 있다.
- [ ] Result와 예외를 언제 구분하는지 설명할 수 있다.
- [ ] Domain Model, Application Service, Repository, Strategy의 책임을 구분할 수 있다.
- [ ] DI, Composition Root, SOLID가 테스트 가능성에 주는 이점을 설명할 수 있다.
