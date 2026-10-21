# 2026-10-22 이해도 점검 — 시간대와 DST 안전성

먼저 답을 가리고 말이나 글로 설명하세요. “API 이름”보다 **어떤 잘못된 순간을 만들지 않기 위한가**를 설명하는 것이 목표입니다.

---

## 1. `DateOnly`는 어떤 정보를 담고, 무엇을 담지 않나요?

<details>
<summary>정답과 해설</summary>

달력의 연·월·일만 담습니다. 하루 안의 시각, UTC offset, 시간대 규칙, 하나의 실제 순간은 담지 않습니다. `2030-11-03`만으로는 서울의 하루인지 뉴욕의 하루인지도 결정되지 않습니다.

</details>

## 2. `TimeOnly`만으로 회의 시작 순간을 저장할 수 있나요?

<details>
<summary>정답과 해설</summary>

없습니다. `01:30`은 날짜와 시간대가 없고, DST 종료일에는 같은 지역에서도 두 번 나타날 수 있습니다. 날짜, 시간대 ID, 필요하면 offset까지 있어야 한 순간을 확정할 수 있습니다.

</details>

## 3. `DateTimeOffset`이 `DateTime`보다 하나의 순간을 표현하기 좋은 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

벽시계 값과 UTC offset을 함께 가져 UTC와의 관계가 명확합니다. 따라서 `ToUniversalTime()`으로 같은 순간을 일관되게 비교할 수 있습니다. 다만 offset은 `America/New_York` 같은 시간대 정체성과 미래 DST 규칙을 보존하지 않습니다.

</details>

## 4. 왜 지역 입력의 `DateTime.Kind`를 `Unspecified`로 만드나요?

<details>
<summary>정답과 해설</summary>

사용자 입력은 서버 PC의 `Local`도 `Utc`도 아니기 때문입니다. `Unspecified`는 자동 변환을 막고, 명시한 `TimeZoneInfo`의 규칙으로만 해석하겠다는 의도입니다.

</details>

## 5. 시간대 ID와 UTC offset은 같은 정보인가요?

<details>
<summary>정답과 해설</summary>

아닙니다. offset은 특정 순간의 UTC와의 차이일 뿐이고 여러 시간대가 같은 offset을 공유할 수 있습니다. 시간대 ID는 날짜별 offset과 DST 전환 규칙을 찾게 하지만, DST 중복 시각에는 후보 둘 중 하나를 단독으로 선택하지 못합니다.

</details>

## 6. DST 시작의 invalid time은 무엇인가요?

<details>
<summary>정답과 해설</summary>

시계가 앞으로 이동해 지역 벽시계에 실제로 존재하지 않는 구간입니다. 뉴욕의 `2030-03-10 02:30`은 `01:59:59` 다음이 `03:00:00`이므로 실제 순간으로 변환할 수 없습니다.

</details>

## 7. invalid time을 다음 가능한 시각으로 자동 보정하면 왜 위험한가요?

<details>
<summary>정답과 해설</summary>

사용자가 고르지 않은 시각으로 회의가 이동합니다. 항공·금융·의료처럼 시간이 중요한 업무에서는 조용한 보정보다 명확한 입력 오류와 재선택이 안전합니다.

</details>

## 8. DST 종료의 ambiguous time은 무엇인가요?

<details>
<summary>정답과 해설</summary>

시계가 뒤로 이동해 같은 지역 벽시계가 두 번 나타나는 구간입니다. 뉴욕 `2030-11-03 01:30`은 `-04:00`이면 `05:30Z`, `-05:00`이면 `06:30Z`입니다.

</details>

## 9. 왜 `RequireExplicitOffsetPolicy`는 서버가 임의로 하나를 고르지 않게 하나요?

<details>
<summary>정답과 해설</summary>

두 후보는 실제로 한 시간 차이이며 어느 것이 사용자 의도인지 서버가 알 수 없습니다. `allowedValues`를 돌려주고 명시적 선택을 받으면 추측을 API 계약에서 제거할 수 있습니다.

</details>

## 10. 평상시에도 잘못된 `preferredUtcOffset`을 거절하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

클라이언트가 오래된 시간대 규칙이나 잘못된 계산을 사용했다는 신호일 수 있습니다. 서버 계산과 불일치하는 offset을 조용히 무시하면 클라이언트가 다른 순간을 믿을 수 있으므로 명시적으로 고치게 합니다.

</details>

## 11. 원래 지역 날짜·시각·시간대 ID와 계산된 UTC를 왜 함께 보존하나요?

<details>
<summary>정답과 해설</summary>

UTC는 정렬·충돌·실행에 안정적이고, 지역 문맥은 사용자의 의도·표시·감사·향후 시간대 규칙 변경 검토에 필요합니다. 하나만 저장하면 나머지 질문에 답하기 어렵습니다.

</details>

## 12. 반열린 구간 `[start, end)`의 장점은 무엇인가요?

<details>
<summary>정답과 해설</summary>

끝 순간을 포함하지 않아 `10:00 종료`와 `10:00 시작`을 충돌 없이 이어 붙일 수 있습니다. 겹침 공식도 `A.Start < B.End && B.Start < A.End`로 일관됩니다.

</details>

## 13. 서로 다른 시간대의 회의 충돌을 지역 문자열로 비교하면 왜 틀릴 수 있나요?

<details>
<summary>정답과 해설</summary>

서울 `14:30`과 뉴욕 `00:30`처럼 글자는 달라도 같은 UTC 순간일 수 있고, 뉴욕의 같은 `01:30`도 offset에 따라 다른 순간일 수 있습니다. 충돌은 확정된 UTC 구간으로 비교해야 합니다.

</details>

## 14. `Result<T>`로 처리하기 좋은 실패 세 가지를 드세요.

<details>
<summary>정답과 해설</summary>

알 수 없는 시간대 ID, DST invalid/ambiguous 입력, 잘못된 duration, ID 중복, 주최자 시간 충돌 중 세 가지면 됩니다. 모두 호출자가 입력이나 선택을 바꿔 해결할 수 있는 예상된 실패입니다.

</details>

## 15. `OperationCanceledException`을 `Result.Failure`로 바꾸면 왜 좋지 않나요?

<details>
<summary>정답과 해설</summary>

취소는 업무 규칙 위반이 아니라 호출자가 결과를 더 원하지 않는다는 제어 흐름입니다. 오류로 바꾸면 잘못된 4xx·실패 지표가 생기고 상위 취소 전파와 자원 중단이 깨질 수 있습니다.

</details>

## 16. `IAmbiguousTimePolicy`가 SOLID의 어떤 생각을 보여 주나요?

<details>
<summary>정답과 해설</summary>

모호성 선택이라는 하나의 책임을 분리하고(SRP), 새 정책을 기존 Resolver/Service 수정 없이 추가하며(OCP), 고수준 흐름이 구체 정책 대신 abstraction에 의존합니다(DIP).

</details>

## 17. Application Service는 왜 `TimeZoneInfo`를 직접 호출하지 않나요?

<details>
<summary>정답과 해설</summary>

유스케이스 조율과 운영체제 시간대 기술을 분리하기 위해서입니다. `ILocalTimeResolver` Port 뒤에 두면 Test Double, 다른 시간대 라이브러리, 규칙 버전 저장 Adapter로 교체하기 쉽습니다.

</details>

## 18. Repository Port가 있는데도 Domain Model이 필요한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

Repository는 저장 경계를 추상화할 뿐 값의 의미와 불변식을 대신하지 않습니다. `MeetingSlot`의 길이·겹침, `MeetingReservation`의 식별자·제목 같은 규칙은 저장 기술과 무관하게 Domain에 남아야 합니다.

</details>

## 19. 충돌 조회와 저장을 같은 `lock` 안에 두는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

두 요청이 동시에 “충돌 없음”을 읽은 뒤 둘 다 저장하는 경쟁을 막기 위해서입니다. 확인과 변경이 하나의 원자 연산처럼 보여야 불변식이 유지됩니다.

</details>

## 20. 왜 C# `lock`은 여러 서버 인스턴스에서 충분하지 않나요?

<details>
<summary>정답과 해설</summary>

각 프로세스는 서로 다른 메모리와 lock을 가집니다. 서버 A와 B가 동시에 요청을 처리하면 서로의 임계 구역을 알지 못하므로 DB constraint/transaction 또는 분산 조정이 필요합니다.

</details>

## 21. `TimeProvider`가 어떤 테스트를 안정화하나요?

<details>
<summary>정답과 해설</summary>

“현재보다 30분 뒤” 규칙입니다. 시스템 시계를 직접 읽으면 어제 성공한 고정 입력이 오늘 과거가 되어 실패하지만, 고정 `TimeProvider`는 매번 같은 경계 결과를 만듭니다.

</details>

## 22. `TimeZoneInfo.TryFindSystemTimeZoneById`를 쓰는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

외부에서 온 잘못된 ID는 예상 가능한 입력 실패입니다. `Try*` API는 이를 예외 제어 흐름으로 처리하지 않고 성공 여부와 결과를 명시적으로 나누게 합니다.

</details>

## 23. `DateTimeOffset` 생성도 범위 오류가 날 수 있는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

연도 1의 지역 자정에 양수 offset을 빼면 UTC가 지원 최소값보다 앞설 수 있고, 연도 9999 끝에 음수 offset을 더하면 최대값을 넘을 수 있습니다. Adapter는 이를 `local_time.out_of_range` 예상 오류로 바꿉니다.

</details>

## 24. HTTP request의 `DateOnly`와 `TimeOnly`을 nullable로 받은 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

값 형식은 non-nullable이면 JSON 필드가 빠졌을 때 기본값이 들어갈 수 있습니다. `DateOnly?`, `TimeOnly?`로 받아 누락을 `local_date.required`, `local_start_time.required`로 명시적으로 거절합니다.

</details>

## 25. 시간대 규칙이 미래에 바뀌면 어떤 결정을 해야 하나요?

<details>
<summary>정답과 해설</summary>

기존 UTC를 고정할지 최신 규칙으로 재계산할지, 원래 지역 의도와 계산 당시 offset/규칙 버전을 어떻게 보존할지, 바뀐 회의를 누구에게 알리고 승인받을지 결정해야 합니다. 정답 하나가 아니라 제품 계약과 감사 가능한 절차가 필요합니다.

</details>

## 26. 반복 일정을 UTC에 7일씩 더하면 왜 지역 오전 9시가 깨질 수 있나요?

<details>
<summary>정답과 해설</summary>

DST 전환 뒤 지역의 UTC offset이 달라지기 때문입니다. “매주 지역 오전 9시”는 매 회차를 지역 달력에서 만든 뒤 그 날짜의 시간대 규칙으로 UTC를 다시 계산해야 합니다.

</details>

## 27. 메서드가 많은데도 Composition Root는 왜 한곳이어야 하나요?

<details>
<summary>정답과 해설</summary>

어떤 Port에 어떤 구체 Adapter/Strategy를 사용할지 한곳에서 보이게 해야 실행 구성을 추적하고 교체하기 쉽습니다. Domain과 Application 곳곳에서 `new InMemoryMeetingRepository()`를 만들면 결합과 수명 문제가 숨습니다.

</details>

## 28. 17개 self-test 통과가 무엇을 증명하고 무엇을 증명하지 못하나요?

<details>
<summary>정답과 해설</summary>

작성한 17개 경계 시나리오(24개 동시 경쟁 포함)가 현재 설치된 시간대 데이터와 runtime에서 통과했음을 보여 줍니다. 모든 국가의 역사적 규칙, 미래 법률 변경, 모든 thread interleaving, 운영 DB transaction, 여러 서버 인스턴스, 인증·보안을 증명하지는 않습니다.

</details>

---

## 최종 말하기 점검

아래 문장을 보지 않고 완성하면 오늘 목표를 달성한 것입니다.

> 사용자의 지역 시각은 곧바로 한 순간이 아니다. 먼저 ______와 ______을 보존하고, `TimeZoneInfo`로 ______/______ 시각을 구분한다. 한 순간을 확정한 뒤에는 ______로 저장·충돌 비교하되, 감사와 향후 규칙 변경을 위해 ______도 함께 남긴다.

예시 답: `날짜·벽시계`, `시간대 ID`, `존재하지 않는`, `중복되는`, `UTC`, `원래 지역 문맥과 적용 offset`.

복습이 끝나면 [`EXERCISES.md`](./EXERCISES.md)의 Intermediate 이상 과제 하나를 선택해 직접 구현하세요.
