# 2026-09-21 이해도 점검

먼저 답을 보지 않고 소리 내어 설명하세요. 모르면 해당 코드와 README 절로 돌아간 뒤 다시 답합니다.

## 질문

1. rate limit과 concurrency limit은 무엇이 다른가요?
2. 오늘 정책에서 `PermitLimit = 1`은 무엇을 뜻하나요?
3. `QueueLimit = 1`은 동시에 두 요청을 실행한다는 뜻인가요?
4. 같은 `alpha` 파티션의 세 번째 동시 요청과 `beta`의 첫 요청은 각각 어떻게 되나요?
5. limiter가 허용한 요청을 표현하는 lease를 반드시 정리해야 하는 이유는 무엇인가요?
6. `/health`가 아니라 `POST /reports`에만 named policy를 붙인 이유는 무엇인가요?
7. 사용자가 보낸 임의 헤더를 그대로 파티션 키로 쓰면 어떤 두 가지 문제가 생기나요?
8. 입력 오류를 `Result`로, 저장소 버그나 장애를 예외로 구분한 이유는 무엇인가요?
9. `record`를 사용했다고 해서 내부의 모든 참조 객체까지 자동으로 불변이 되나요?
10. Application Service가 구체 renderer와 repository가 아니라 interface에 의존하면 무엇이 좋아지나요?
11. 429 응답만으로 DDoS 방어가 완성되지 않는 이유는 무엇인가요?
12. 메모리 limiter가 여러 서버 인스턴스를 합친 전역 한도를 보장하나요?

## 정답과 해설

<details>
<summary>1. rate와 concurrency의 차이</summary>

rate limit은 “정해진 시간 동안 몇 건”을 제한하고, concurrency limit은 “바로 지금 동시에 실행되는 몇 건”을 제한합니다. 느린 보고서 생성처럼 한 요청이 자원을 오래 점유할 때는 동시성 제한이 직접적인 보호 수단입니다.
</details>

<details>
<summary>2. PermitLimit = 1</summary>

각 파티션에서 한 시점에 실행 lease를 한 개만 허용한다는 뜻입니다. 전체 서비스가 한 건만 처리한다는 뜻이 아니라 `alpha`, `beta`, `anonymous`가 각각 독립 permit을 가집니다.
</details>

<details>
<summary>3. QueueLimit = 1</summary>

아닙니다. 한 요청은 실행하고, permit이 없을 때 추가 한 요청만 대기할 수 있다는 뜻입니다. 큐의 요청은 앞 요청이 lease를 반납한 뒤에야 실행됩니다.
</details>

<details>
<summary>4. 파티션 격리</summary>

`alpha`의 첫 요청이 실행 중이고 둘째가 대기 중이면 셋째는 429로 거절됩니다. 같은 순간 `beta`의 첫 요청은 별도 파티션 permit을 얻어 실행할 수 있습니다.
</details>

<details>
<summary>5. lease 정리</summary>

concurrency limiter는 lease가 Dispose될 때 점유 permit을 돌려받습니다. 성공, 실패, 예외, 취소 어느 경로에서도 정리되지 않으면 permit이 새어 이후 요청이 계속 막힐 수 있습니다. ASP.NET Core middleware가 요청 수명과 lease 정리를 관리합니다.
</details>

<details>
<summary>6. named policy 경계</summary>

비싼 보고서 생성만 보호하고 저렴한 상태 확인까지 같은 대기열에 묶지 않기 위해서입니다. endpoint-specific policy는 제한 대상을 코드에서 명시적으로 드러냅니다.
</details>

<details>
<summary>7. 임의 키의 위험</summary>

사용자는 헤더 값을 바꾸며 새 파티션을 만들어 제한을 우회할 수 있습니다. 또한 끝없이 다른 키를 보내 limiter cache의 메모리를 늘리는 DoS를 만들 수 있습니다. 오늘 예제가 알려진 값만 허용하고 나머지를 `anonymous`로 모으는 이유입니다.
</details>

<details>
<summary>8. Result와 예외</summary>

빈 제목이나 지원하지 않는 형식은 호출자가 고칠 수 있는 예상 가능한 실패라 `Result`로 전달합니다. 저장소 계약 위반, 네트워크 장애, 프로그래밍 오류는 정상 업무 분기가 아니므로 예외를 숨기지 않아야 운영자가 발견하고 복구할 수 있습니다. 호출자 취소도 일반 실패로 바꾸지 않고 취소로 전파합니다.
</details>

<details>
<summary>9. record와 깊은 불변성</summary>

아닙니다. record의 속성이 배열이나 변경 가능한 목록을 가리키면 그 객체는 여전히 바뀔 수 있습니다. 방어적 복사나 불변 컬렉션 같은 별도 설계가 필요합니다.
</details>

<details>
<summary>10. Port와 DI</summary>

Application 규칙을 수정하지 않고 renderer나 저장 기술을 교체할 수 있습니다. 자체 테스트에서는 느린 실제 시스템 대신 결정적인 fake를 주입해 성공, 실패, 취소 경계를 빠르게 재현할 수 있습니다. 이는 DIP와 테스트 용이성을 함께 높입니다.
</details>

<details>
<summary>11. 429와 DDoS</summary>

애플리케이션까지 도달한 요청 자체가 네트워크, 프록시, TLS, 연결, 메모리 자원을 이미 소비할 수 있습니다. 따라서 CDN/WAF/reverse proxy, 인증, 연결 제한, autoscaling, 관측과 대응 절차가 함께 필요합니다.
</details>

<details>
<summary>12. 여러 인스턴스</summary>

보장하지 않습니다. 각 프로세스는 자기 limiter 상태만 압니다. 전역 계약이 필요하면 gateway나 분산 저장소 기반 정책을 검토하고, 장애 시 허용할지 거절할지와 일관성 비용을 명시해야 합니다.
</details>

## 통과 기준

- 12개 중 9개 이상을 코드 위치와 함께 설명합니다.
- 4번과 5번은 그림 없이 요청 순서까지 설명합니다.
- 틀린 항목은 README와 소스에서 근거를 찾아 한 문장으로 다시 씁니다.
