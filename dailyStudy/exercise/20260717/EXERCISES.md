# 단계별 실습

각 단계가 끝날 때마다 아래 명령을 실행하세요.

```powershell
dotnet run --project .\src\NotificationExercise\NotificationExercise.csproj -- --self-test
```

오류가 나오면 첫 오류의 파일명과 줄 번호부터 확인하세요. 한 번에 한 부분만 고치고 다시 실행하는 습관이 중요합니다.

## 1단계: 값과 조건 바꾸기 (10분)

`BasicSyntaxTour`의 `unreadCount`를 `0`, `2`, `7`로 차례로 바꾸세요.

1. 실행 전 `urgency` 출력값을 종이에 예상합니다.
2. 일반 실행으로 예상과 실제를 비교합니다.
3. `marketingAgreed`를 `true`로 바꾸고 삼항 연산자의 결과도 확인합니다.

## 2단계: 템플릿 종류 추가하기 (15분)

`NotificationKind`에 `PaymentCompleted`를 추가하고 `NotificationTemplate.Render`의 `switch`에 제목과 본문을 추가하세요.

완료 조건:

- 새 enum 값 때문에 발생한 컴파일 오류를 `switch` 수정으로 해결한다.
- `NotificationDemo`에서 새 종류를 선택하면 결제 완료 제목이 출력된다.
- 기존 자체 검증도 모두 통과한다.

## 3단계: Push Strategy 추가하기 (20분)

`PushSender : INotificationSender`를 만들고 `Channel`이 `NotificationChannel.Push`를 반환하게 하세요.

1. `EmailSender`를 복사하기 전에 인터페이스의 세 멤버를 먼저 확인합니다.
2. `CompositionRoot`의 `senders` 배열에 새 구현을 등록합니다.
3. 자체 검증의 Push 기대값을 실패에서 성공으로 바꿉니다.

이 단계의 핵심은 `NotificationService`를 수정하지 않고 기능을 확장하는 것입니다. 새 전략을 만들고 조립 지점에 등록하는 것만으로 동작해야 합니다.

## 4단계: 하루 발송 한도 생각하기 (말로 설계, 10분)

사용자별 하루 발송 횟수를 제한한다고 가정하세요.

- 횟수 조회 메서드는 어느 Repository 계약에 둘 것인가?
- 제한 규칙은 `NotificationService`, Sender, 별도 정책 클래스 중 어디에 둘 것인가?
- 여러 서버가 동시에 발송할 때 메모리 저장소로 충분하지 않은 이유는 무엇인가?
- 외부 이메일 API가 실패하면 `Result<T>`와 예외를 각각 언제 사용할 것인가?

정답 하나보다 각 클래스의 책임을 한 문장으로 설명할 수 있는지가 중요합니다.
