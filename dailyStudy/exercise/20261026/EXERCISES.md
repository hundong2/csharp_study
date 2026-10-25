# 2026-10-26 연습문제 — `System.IO.Pipelines`와 길이 접두사 protocol

연습은 쉬운 수정에서 운영 설계까지 이어집니다. 각 단계에서 먼저 결과를 예측하고, 한 번에 한 가지 계약만 바꾼 뒤 `--self-test`를 다시 실행하세요.

```powershell
dotnet run --project .\src\TelemetryStreamExercise\TelemetryStreamExercise.csproj -c Release -- --self-test
```

---

## 1. Beginner — demo 값을 바꾸고 결과 예측하기

[`Program.cs`](./src/TelemetryStreamExercise/Program.cs)의 장치 ID와 온도를 바꿉니다.

1. `sensor-seoul`의 온도를 `-10.75m`로 바꿉니다.
2. `sensor-busan` 앞뒤에 공백을 넣습니다.
3. 실행 전에 accepted/duplicate/rejected/stored 값을 적습니다.
4. 실행 결과와 비교합니다.

### 생각할 점

- `m` suffix가 없는 `21.5`는 어떤 숫자 형식인가요?
- `Trim`은 원본 문자열을 바꾸나요, 새 문자열을 만드나요?
- 장치 ID 정규화가 record equality와 중복 판정에 어떤 영향을 주나요?

### 완료 기준

- 값 형식과 정규화 결과를 말로 설명합니다.
- 원래 demo fixture로 되돌린 뒤 self-test가 다시 통과합니다.

---

## 2. Beginner — 새 Domain 규칙 추가하기

장치 ID는 소문자 영문, 숫자, `-`만 허용한다고 가정합니다.

1. [`TelemetryReading.Create`](./src/TelemetryStreamExercise/Domain/TelemetryReading.cs)에 검증을 추가합니다.
2. 실패 code를 `telemetry.device_id_invalid`로 고정합니다.
3. `sensor_jeju`, `SENSOR-1`, `sensor-1`을 각각 검사합니다.
4. 왜 이 오류가 `Protocol`이 아니라 `Business`인지 설명합니다.

### 힌트

정규식 없이 `All`과 pattern matching을 사용할 수 있습니다. 문화권에 따라 달라지는 대소문자 변환 대신 허용 문자 자체를 명시하세요.

### 완료 기준

- 정상/비정상 경계 test를 추가합니다.
- 모든 새 method 또는 local helper에 역할·입력·반환을 설명하는 한국어 주석을 답니다.

---

## 3. Beginner+ — frame byte를 손으로 읽기

다음 header가 나타내는 signed Int32 길이를 계산합니다.

```text
00 00 00 0A
00 00 04 00
FF FF FF FF
```

그다음 [`LengthPrefixedFrameEncoder.EncodeLengthPrefix`](./src/TelemetryStreamExercise/Infrastructure/Protocol/LengthPrefixedFrameEncoder.cs)와 decoder를 사용해 답을 확인합니다.

### 완료 기준

- big-endian을 “가장 큰 자리 byte가 먼저 온다”는 말로 설명합니다.
- `FF FF FF FF`가 음수 길이로 거절되는 이유를 설명합니다.

---

## 4. Intermediate — exact maximum 경계 검사하기

최대 payload가 1,024 byte일 때 아래 두 경우를 만듭니다.

- 정확히 1,024 byte payload: 성공
- 1,025 byte라고 선언한 header: body를 보내지 않아도 즉시 `frame.length_too_large`

### 주의

거대한 배열을 먼저 만들지 마세요. 초과 test는 4-byte header만으로 충분합니다. 이것이 “검증 후 할당” 원칙입니다.

### 완료 기준

- off-by-one 두 경계를 self-test로 고정합니다.
- 초과 길이가 `NeedMoreData`가 아닌 즉시 오류인 이유를 설명합니다.

---

## 5. Intermediate — 모든 분할 위치 검사하기

정상 frame 하나를 만들고, 입력이 다음 위치에서 잘려 들어오게 검사합니다.

1. header 뒤 1, 2, 3 byte
2. payload의 첫 byte 뒤
3. payload 정중앙
4. 마지막 byte 직전

`ChunkedReadStream`의 chunk 계획을 바꾸거나 작은 helper를 작성합니다.

### 확인할 계약

- 부분 frame은 오류가 아니라 대기입니다.
- header를 먼저 읽었어도 payload가 부족하면 다음 read에서 header부터 다시 볼 수 있어야 합니다.
- 완전한 frame은 정확히 한 번만 생산됩니다.

### 완료 기준

- 모든 분할에서 같은 `TelemetryReading`이 나옵니다.
- frame이 중복 생산되거나 byte가 유실되지 않습니다.

---

## 6. Intermediate+ — 여러 frame을 한 read에서 drain하기

`MemoryStream`에 정상 frame 세 개를 연속으로 넣습니다. 한 번의 `ReadAsync`가 충분한 byte를 줄 수 있어도 reader는 frame 세 개를 모두 생산해야 합니다.

### 잘못된 구현 예측

inner `while`을 `if`로 바꾸면 어떤 현상이 생길까요? 남은 buffer가 다시 반환될 수는 있지만, `examined` 위치에 따라 stall이나 불필요한 read가 생길 수 있습니다.

### 완료 기준

- 입력 순서가 보존됩니다.
- accepted가 3이고 Repository Count도 3입니다.
- 왜 outer loop와 inner loop가 모두 필요한지 설명합니다.

---

## 7. Advanced — strict UTF-8 정책 추가하기

현재 `System.Text.Json`이 거절하는 JSON과 “UTF-8 byte 자체가 잘못된 경우”를 구분해 봅니다.

1. UTF-8 continuation byte만 있는 payload를 만듭니다.
2. payload parser 앞에 strict UTF-8 validation Strategy를 둘지, JSON reader에 맡길지 결정합니다.
3. 오류 code와 어느 계층의 책임인지 문서화합니다.

### 설계 질문

- invalid UTF-8은 Protocol인가 Business인가?
- byte를 string으로 먼저 만들면 어떤 allocation이 생기나요?
- replacement character를 허용하면 원본 서명이 있는 protocol에서 어떤 문제가 생기나요?

### 완료 기준

- 정책을 test로 고정합니다.
- 예상한 입력 오류만 catch하고 `Exception` 전체를 삼키지 않습니다.

---

## 8. Advanced — newline framing Strategy 추가하기

각 JSON이 LF(`\n`)로 끝나는 두 번째 framing 방식을 만듭니다.

1. `IFrameDecoder`를 구현하는 `NewlineFrameDecoder`를 추가합니다.
2. 빈 줄 허용 여부와 최대 line byte 수를 정합니다.
3. `Program`에서 decoder 한 줄만 바꾸어 Application Service를 그대로 재사용합니다.
4. CRLF(`\r\n`)와 LF 정책을 명시합니다.

### 설계 의도

새 protocol 때문에 `TelemetryIngestService`의 `if`/`switch`가 늘어나면 Strategy 경계가 제대로 서지 않은 것입니다. OCP는 “절대 수정 금지”가 아니라 변화가 예상되는 축을 올바른 interface 뒤에 두는 원칙입니다.

### 완료 기준

- length-prefixed와 newline decoder의 공통 test 계약을 만듭니다.
- 조립 코드 외 기존 Application/Domain 파일은 수정하지 않습니다.

---

## 9. Advanced+ — CRC32가 있는 protocol v2 설계하기

다음 frame을 설계합니다.

```text
[1-byte version][4-byte payload length][payload][4-byte CRC32]
```

구현 전에 문서로 답합니다.

- CRC가 계산하는 범위는 version/header/payload 중 어디인가?
- CRC byte order는 무엇인가?
- version을 모르면 frame을 건너뛸 수 있는가, 연결을 끊어야 하는가?
- 최대 길이는 CRC를 포함하는가?
- checksum 실패는 Protocol인가 Business인가?

### 완료 기준

- 정상, 1-bit 변조, 잘린 checksum, 미지원 version test를 작성합니다.
- 기존 protocol v1 test를 깨뜨리지 않습니다.
- CRC는 우발적 손상 검출이지 인증이 아님을 설명합니다.

---

## 10. Pro — 내구성 있는 Repository Adapter 설계하기

메모리 Adapter를 관계형 database로 바꾼다고 가정합니다.

### 설계 문서에 포함할 것

- `event_id` unique constraint
- event ID와 canonical payload hash
- 동일 중복과 내용 충돌을 구분하는 transaction 흐름
- timeout/cancellation 전파
- transient exception retry 위치
- 여러 process가 동시에 같은 event를 저장할 때의 결과

### 실패 주입 검사

- insert 직전 취소
- insert는 성공했지만 응답 전에 연결 끊김
- 같은 event가 두 worker에 동시에 도착
- 같은 ID와 다른 내용 도착

### 완료 기준

- `Contains` 후 `Insert` 두 호출만으로 원자성이 보장되지 않는 이유를 설명합니다.
- Application Service가 특정 SQL provider에 의존하지 않습니다.

---

## 11. Pro — backpressure와 관측 가능성 설계하기

송신자는 초당 100,000 frame을 보내고 Repository는 초당 2,000건만 저장할 수 있다고 가정합니다.

### 결정할 것

- connection read를 늦출지, bounded queue를 둘지, 429/close 같은 신호를 보낼지
- queue capacity와 overflow 정책
- frame 처리 순서 보장 범위
- graceful shutdown 때 남은 data 처리 정책
- retry와 duplicate 의미

### metric 후보

- `telemetry_frames_total{outcome=accepted|duplicate|business_rejected|protocol_error}`
- `telemetry_frame_payload_bytes` histogram
- `telemetry_ingest_duration_seconds` histogram
- 현재 queue depth gauge

`eventId`와 `deviceId`는 높은 카디널리티이므로 metric label로 넣지 않습니다.

### 완료 기준

- 메모리 사용량 상한을 계산할 수 있습니다.
- backpressure가 단순 성능 옵션이 아니라 안정성 계약임을 설명합니다.
- logging/metric framework를 Domain Model에 직접 의존시키지 않습니다.

---

## 12. Pro+ — zero-copy의 비용을 설계로 설명하기

현재 decoder는 최대 1,024 byte payload를 `byte[]`로 복사합니다. 복사를 없애려면 `ReadOnlySequence<byte>`를 handler에 넘기고 싶을 수 있습니다.

### 반드시 해결할 질문

- handler가 처리하는 동안 `AdvanceTo`를 언제 호출할 수 있는가?
- 비동기 저장이 끝날 때까지 pipe buffer를 잡으면 backpressure와 memory는 어떻게 되는가?
- 여러 handler가 동시에 읽는 것을 허용할 것인가?
- pool memory lease를 누가 반환하는가?
- 예외·취소·early break에서 반환을 어떻게 보장하는가?

### 완료 기준

- “복사가 없으니 무조건 빠르다”라고 결론 내리지 않습니다.
- ownership API와 dispose 책임을 먼저 설계합니다.
- benchmark에는 처리량뿐 아니라 allocation, peak memory, tail latency를 포함합니다.

---

## 마무리 설명 과제

아래 내용을 코드 없이 3분 안에 설명해 보세요.

1. 왜 Stream과 frame은 같은 개념이 아닌가?
2. decoder가 부분 payload에서 header를 소비하지 않는 이유는 무엇인가?
3. `ReadOnlySequence<byte>`와 `ReadOnlyMemory<byte>`의 역할이 어떻게 다른가?
4. `AdvanceTo`의 두 위치를 틀리면 어떤 문제가 생기는가?
5. maximum frame length가 왜 보안 경계인가?
6. Result, 예외, cancellation을 각각 언제 쓰는가?
7. Repository, Strategy, Application Service, Composition Root가 각각 어떤 책임을 가지는가?
8. in-memory 예제를 운영 gateway로 옮길 때 가장 먼저 다시 설계할 세 가지는 무엇인가?
