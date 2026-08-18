# 2026-08-19: 휴가 요청 승인과 정책 선택

## 처음 읽는 순서

1. 아래 **기초 문법 지도**를 읽습니다.
2. [`Program.cs`](./src/LeaveApprovalExercise/Program.cs)를 위에서 아래로 읽어 실행 흐름을 봅니다.
3. `dotnet run`과 `dotnet run -- --self-test`를 실행합니다.
4. [`EXERCISES.md`](./EXERCISES.md)의 1단계부터 한 번에 하나씩 수정합니다.
5. [`CHECKPOINT.md`](./CHECKPOINT.md)를 코드 없이 설명하며 복습합니다.

## 실행

```powershell
cd dailyStudy/exercise/20260819/src/LeaveApprovalExercise
dotnet build
dotnet run
dotnet run -- --self-test
```

설치된 안정 SDK 10.0.301과 `net10.0`을 사용합니다. Nullable 경고를 오류로 취급해 null 위험을 컴파일 단계에서 찾습니다.

## 기초 문법 지도

- `var repository = ...;`: 오른쪽 값으로 변수 형식을 추론하며 문장은 보통 `;`로 끝납니다.
- `string`은 null이 아니어야 하고 `string?`은 값이 없을 수 있습니다. `IsNullOrWhiteSpace`로 null·빈 문자열·공백을 함께 검사합니다.
- `int`는 정수, `bool`은 참/거짓, `enum`은 제한된 이름 목록을 나타냅니다.
- `if`는 조건 분기, `foreach`는 반복, `return`은 값을 돌려주며 메서드를 끝냅니다.
- `record`는 값 중심 데이터를 불변에 가깝게 표현합니다. `class`는 동작과 변경 가능한 상태를 묶을 때 사용합니다.
- `interface`는 필요한 동작의 계약입니다. 구체 구현을 바꿀 수 있어 테스트와 확장이 쉬워집니다.
- `Task<T>`, `async`, `await`는 DB 같은 I/O 대기 중 스레드를 붙잡지 않는 비동기 문법입니다.
- LINQ의 `OrderBy`, `Count`는 컬렉션의 정렬과 집계를 의도를 드러내며 표현합니다.
- 제네릭 `Result<T>`의 `T` 자리에 실제 성공 값 형식이 들어갑니다. 성공 여부를 확인한 뒤 값을 사용합니다.

## 설계를 읽는 방법

실행 흐름은 `Program`(Composition Root) → `ReviewLeaveRequestsService`(Application Service) → `ILeaveApprovalPolicy`(Strategy)와 Repository 순서입니다. 생성자 주입은 서비스가 구체 클래스가 아닌 인터페이스에 의존하게 하는 DI/DIP 방식입니다. 이로써 정책과 저장소를 테스트 대역으로 교체할 수 있습니다.

`LeaveRequest`와 `ReviewResult`는 Domain Model입니다. record와 읽기 전용 결과 목록은 처리 중 데이터가 의도치 않게 바뀔 가능성을 줄입니다. Application Service는 사용 사례 순서만 담당해 SRP를 지키고, 새 승인 정책은 Strategy 구현 추가로 확장해 OCP를 따릅니다. 이것이 SOLID를 작게 적용한 예입니다.

잔여 일수 부족 같은 예상 가능한 업무 실패는 `Result<T>`나 명시적 결정으로 다룹니다. 반면 저장소 장애, 취소, 코드 계약 위반은 정상 분기가 아니므로 예외가 적합합니다. 모든 실패를 예외로 만들면 거절과 장애가 로그에서 섞이고, 모든 예외를 Result로 바꾸면 장애의 스택 정보가 약해질 수 있습니다.

메모리 Repository는 학습용입니다. 실제 운영에서는 요청 ID에 고유 제약을 두고 결정 저장과 잔여 일수 차감을 한 트랜잭션으로 처리해야 동시 승인 문제를 막습니다. 재시도에는 멱등성 키를 사용하고, 승인 알림이 필요하면 Outbox를 고려합니다. `CancellationToken`은 끝까지 전달하고, 로그·추적에는 요청 ID, 결과, 지연 시간을 남기되 병가 사유 같은 민감 정보는 제외합니다. 실패율, 처리 지연, 관리자 검토 적체를 지표와 경보로 관찰합니다.

## 버전 업데이트 (2026-08-19 확인)

- 최신 안정 버전은 **.NET 10.0.11(LTS), SDK 10.0.400, C# 14**입니다. 이 예제는 로컬 안정 SDK 10.0.301에서도 컴파일되는 안정 기능만 사용합니다.
- 최신 미리 보기는 **.NET 11 Preview 7 / C# 15**입니다. C# 15의 union types, closed hierarchies, extension indexers 등은 preview SDK가 필요하므로 실행 코드에는 넣지 않았습니다. 미리 보기 기능은 명세와 도구가 바뀔 수 있어 별도 실험 프로젝트에서 평가하세요.
- 공식 출처: [.NET 10 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [.NET 11 Preview 다운로드](https://dotnet.microsoft.com/en-us/download/dotnet/11.0), [C# 14 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14), [C# 15 새 기능](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-15)

## 완료 기준

빌드 경고 0개, self-test 4/4, 기본 실행 결과 승인 2건·거절 1건·관리자 검토 1건을 확인하고 Repository·Strategy·Application Service의 책임을 각각 한 문장으로 설명하면 완료입니다.
