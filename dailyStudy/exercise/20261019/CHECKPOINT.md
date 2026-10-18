# 이해도 검증 — 먼저 답하고 펼치기

각 질문에 소리 내어 한 문장으로 답한 뒤 정답을 펼치세요. 틀렸다면 [`README`의 코드 읽는 순서](./README.md#-코드-읽는-순서-reading-order)에서 해당 파일을 찾아 다시 읽고 직접 실행합니다.

---

## 1. `AddValidation()`은 무엇을 추가하나요?

<details>
<summary>정답 보기</summary>

Minimal API handler 매개변수의 검증 metadata를 이용하는 endpoint filter와 관련 서비스를 등록합니다. 유효하지 않으면 handler 전에 400 ValidationProblemDetails를 만듭니다.

</details>

## 2. `[Required]`와 `string?`는 같은 역할인가요?

<details>
<summary>정답 보기</summary>

아닙니다. `string?`는 compiler의 nullable 정적 분석 정보이고, `[Required]`는 runtime 입력 검증 규칙입니다.

</details>

## 3. `ValidationAttribute`가 잘 맞는 규칙은 무엇인가요?

<details>
<summary>정답 보기</summary>

UTC offset처럼 한 값만으로 빠르고 결정적으로 검사할 수 있는 전송 형식 규칙입니다.

</details>

## 4. `IValidatableObject`는 왜 사용했나요?

<details>
<summary>정답 보기</summary>

mode와 meetingUrl/room 조합, 면접관 목록 중복처럼 여러 속성을 함께 봐야 하는 객체 단위 규칙을 검사하기 위해 사용했습니다.

</details>

## 5. 속성 오류가 있을 때 `IValidatableObject.Validate`가 항상 실행되나요?

<details>
<summary>정답 보기</summary>

아닙니다. 속성 검증이 먼저이며 그 단계가 실패하면 객체 단위 검증까지 진행하지 않을 수 있습니다. 따라서 모든 종류의 오류가 한 응답에 동시에 나온다고 가정하면 안 됩니다.

</details>

## 6. validator에서 DB를 조회하지 않는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

검증은 여러 번 실행될 수 있고 동기 계약이 많습니다. I/O를 섞으면 지연, 취소, timeout, 장애, 테스트 책임이 흐려지므로 Application/Repository 경계에서 처리합니다.

</details>

## 7. 자동 검증 뒤에도 Domain 팩터리가 필요한 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

Application Service는 HTTP 외에 배치나 메시지 소비자에서도 호출될 수 있습니다. Domain은 진입 경로와 무관하게 잘못된 상태 생성을 막아야 합니다.

</details>

## 8. `record`를 Domain Model에 쓴 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

`record`의 값 비교와 값 중심 표현을 사용하기 위해서입니다. 다만 `record` 자체가 깊은 불변성을 보장하지 않으므로, 이 예제는 private 생성자·get 전용 속성·`ImmutableArray<string>`과 검증 팩터리를 함께 사용해 우발적 변경을 막습니다.

</details>

## 9. `Result<T>`와 예외는 어떻게 구분하나요?

<details>
<summary>정답 보기</summary>

잘못된 입력·일정 충돌처럼 예상 가능한 실패는 `Result<T>`, 구성 오류·저장소 장애 같은 예상 밖 실패는 예외로 둡니다.

</details>

## 10. 취소를 실패 Result로 바꾸지 않는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

호출자가 더 이상 결과를 원하지 않는다는 제어 신호이므로 `OperationCanceledException`을 전파해 host와 하위 I/O가 취소로 인식하게 해야 합니다.

</details>

## 11. `TimeProvider`의 장점은 무엇인가요?

<details>
<summary>정답 보기</summary>

운영에서는 실제 시계를 쓰고 테스트에서는 고정·이동 가능한 시계를 주입해 15분과 180일 경계를 재현할 수 있습니다.

</details>

## 12. Strategy 패턴은 어디에 적용됐나요?

<details>
<summary>정답 보기</summary>

`IInterviewBufferPolicy` 뒤에 원격 5분, 대면 15분 준비 규칙을 각각 구현했습니다. Application Service는 mode에 맞는 Strategy만 선택합니다.

</details>

## 13. Strategy가 큰 `if/else`보다 나은 점은 무엇인가요?

<details>
<summary>정답 보기</summary>

각 정책을 독립적으로 테스트·교체하고 새 mode를 별도 구현으로 추가할 수 있어 SRP와 OCP에 도움이 됩니다.

</details>

## 14. Repository Port의 역할은 무엇인가요?

<details>
<summary>정답 보기</summary>

Application이 Dictionary, EF Core, SQL 같은 저장 기술을 모르고 원자적 예약과 조회라는 업무 의미에만 의존하게 합니다.

</details>

## 15. 충돌 확인과 저장을 두 메서드로 나누면 어떤 문제가 생기나요?

<details>
<summary>정답 보기</summary>

두 요청이 모두 “충돌 없음”을 확인한 뒤 둘 다 저장하는 TOCTOU 경쟁 조건으로 같은 면접관이 이중 예약될 수 있습니다.

</details>

## 16. 메모리 Repository의 `lock`은 무엇을 보장하나요?

<details>
<summary>정답 보기</summary>

한 프로세스 안에서 충돌 검사와 Dictionary 추가가 다른 요청과 끼어들지 않는 하나의 임계 구역으로 실행되게 합니다.

</details>

## 17. 여러 서버 인스턴스에서도 이 `lock`이 충분한가요?

<details>
<summary>정답 보기</summary>

아닙니다. process-local lock은 다른 프로세스를 보지 못하므로 운영 DB transaction/constraint 또는 분산 조정이 필요합니다.

</details>

## 18. 반열린 구간 `[start, end)`의 장점은 무엇인가요?

<details>
<summary>정답 보기</summary>

앞 일정의 end와 다음 일정의 start가 정확히 같을 때 겹치지 않는다는 경계 규칙을 단순하게 표현합니다.

</details>

## 19. 면접 시간이 끝나는 즉시 다음 원격 면접을 시작할 수 있나요?

<details>
<summary>정답 보기</summary>

같은 면접관이라면 안 됩니다. 기존 after 5분과 새 before 5분을 모두 고려해 두 원격 면접 본 시간 사이에는 10분이 필요합니다.

</details>

## 20. DI와 Composition Root는 어디에 있나요?

<details>
<summary>정답 보기</summary>

`Program.BuildApplication`에서 Port와 Adapter, 두 Strategy, TimeProvider, Application Service를 등록하고 연결합니다.

</details>

## 21. `yield return`은 왜 사용했나요?

<details>
<summary>정답 보기</summary>

`IValidatableObject.Validate`에서 발견한 여러 ValidationResult를 하나씩 열거 결과로 전달하면서 다른 규칙 검사도 계속하기 위해 사용했습니다.

</details>

## 22. `GeneratedRegex`의 목적은 무엇인가요?

<details>
<summary>정답 보기</summary>

compiler가 정규식 구현을 미리 생성해 runtime 파싱을 줄이고 정규식 선언을 한 곳에 명확히 두기 위해 사용합니다.

</details>

## 23. HTTP 검증과 self-test는 무엇이 다른가요?

<details>
<summary>정답 보기</summary>

self-test는 Domain/Application/동시성 규칙을 빠르게 검사하고, HTTP 검증은 실제 Kestrel의 binding, 자동 validation filter, 상태 코드, header, JSON 계약까지 black-box로 확인합니다.

</details>

## 24. 오늘 실행 코드는 어떤 버전을 사용하나요?

<details>
<summary>정답 보기</summary>

설치된 안정 SDK 10.0.301로 `net10.0`과 C# 14를 사용합니다. 공식 최신 안정 상태는 .NET 10.0.13/SDK 10.0.402와 C# 14입니다.

</details>

## 25. C# 15/.NET 11 RC2 기능을 코드에 넣지 않은 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

현재 설치된 Stable SDK에서 확실히 빌드되는 자료를 우선하기 위해서입니다. Preview/RC 상태는 README에서 별도로 설명만 합니다.

</details>

## 26. JSON의 `-00:00`도 `[UtcDateTime]`을 통과하는 이유는 무엇인가요?

<details>
<summary>정답 보기</summary>

ASP.NET Core가 원문을 `DateTimeOffset`으로 binding할 때 `Z`, `+00:00`, `-00:00`을 모두 `Offset == TimeSpan.Zero`인 값으로 만들고 원문 철자를 버리기 때문입니다. 이 Attribute는 binding 뒤 의미를 검사합니다. 원문 철자를 제한해야 한다면 `string` DTO 또는 사용자 정의 JSON converter가 필요합니다.

</details>

---

## 최종 복습 체크

- [ ] 자동 검증이 handler 전에 실행됨을 설명한다.
- [ ] Attribute, 객체 교차 검증, Domain, Application 규칙을 구분한다.
- [ ] validator에서 I/O를 하지 않는 이유를 말한다.
- [ ] `Result<T>`와 예외·취소의 경계를 말한다.
- [ ] TimeProvider와 Strategy가 테스트 용이성을 높이는 이유를 말한다.
- [ ] 충돌 검사와 저장의 원자성이 필요한 이유를 그린다.
- [ ] 반열린 구간과 양쪽 buffer를 계산한다.
- [ ] self-test 29개와 HTTP assertion 78개를 직접 통과시킨다.
