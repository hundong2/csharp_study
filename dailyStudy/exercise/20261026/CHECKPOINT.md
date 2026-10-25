# 2026-10-26 이해도 점검 — `System.IO.Pipelines`와 안전한 framing

먼저 답을 가리고 말이나 글로 설명하세요. API 이름 암기보다 **어떤 byte를 언제 소비하고, 어떤 실패에서 계속할 수 있는지**를 설명하는 것이 목표입니다.

---

## 1. 왜 `Stream.ReadAsync` 한 번이 message 하나를 반환한다고 가정하면 안 되나요?

<details>
<summary>정답과 해설</summary>

Stream은 순서 있는 byte 흐름일 뿐 message 경계를 보존하지 않습니다. 한 frame이 여러 read로 나뉘거나 여러 frame이 한 read에 함께 올 수 있습니다. 따라서 application이 length prefix, delimiter 같은 framing 규칙으로 경계를 복원해야 합니다.

</details>

## 2. 이 예제의 4-byte header는 무엇을 뜻하나요?

<details>
<summary>정답과 해설</summary>

header 뒤에 이어지는 UTF-8 JSON payload의 byte 수를 signed 32-bit integer로 나타냅니다. header 자체의 4 byte는 길이에 포함하지 않으며, network byte order인 big-endian으로 기록합니다.

</details>

## 3. big-endian은 무엇인가요?

<details>
<summary>정답과 해설</summary>

정수의 가장 큰 자리 byte를 먼저 배치하는 순서입니다. `00 00 04 00`은 16진수 `0x00000400`, 즉 1,024입니다. 송신자와 수신자가 byte order에 합의하지 않으면 같은 4 byte를 다른 길이로 해석합니다.

</details>

## 4. payload 길이를 문자 수가 아니라 byte 수로 정한 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

wire에서는 byte를 주고받으며 UTF-8 문자는 1~4 byte가 될 수 있습니다. 한글 문자열의 `Length`와 UTF-8 byte 수는 다를 수 있으므로 실제 전송 경계는 byte 기준이어야 합니다.

</details>

## 5. `var`는 runtime에 형식이 정해지는 변수인가요?

<details>
<summary>정답과 해설</summary>

아닙니다. compiler가 오른쪽 식으로 정확한 정적 형식을 compile-time에 결정합니다. `var readResult = await reader.ReadAsync(...)`의 형식은 `ReadResult`로 고정됩니다.

</details>

## 6. `TelemetryReading`을 `record`로 만든 핵심 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

같은 event ID뿐 아니라 장치, 시각, 온도까지 같은 재전송을 값 equality로 비교하기 쉽습니다. 다만 record 자체가 완전한 불변성을 보장하지는 않으므로 private 생성자와 get-only property도 함께 사용합니다.

</details>

## 7. `string?`와 postfix `!`는 무엇이 다른가요?

<details>
<summary>정답과 해설</summary>

`string?`는 값이 null일 수 있다는 계약을 nullable 분석에 알립니다. postfix `!`는 compiler 경고만 억제하며 runtime null check나 값 생성을 하지 않습니다. 근거 없는 `!`는 실제 오류를 숨깁니다.

</details>

## 8. `where T : notnull`은 왜 사용하나요?

<details>
<summary>정답과 해설</summary>

`Result<T>`의 성공 상태가 null 값을 갖지 않도록 generic 형식 계약을 좁힙니다. 성공인데 값이 없고 실패인데 오류가 없는 모순을 줄이는 데 도움이 됩니다.

</details>

## 9. `ReadOnlySequence<byte>`가 배열 하나와 다른 점은 무엇인가요?

<details>
<summary>정답과 해설</summary>

논리적으로 연속된 byte가 실제 memory에서는 여러 segment에 나뉠 수 있습니다. 첫 segment에 4-byte header가 모두 있다는 보장이 없으므로 `FirstSpan[..4]` 같은 가정 대신 `SequenceReader`처럼 segment를 이해하는 API를 사용해야 합니다.

</details>

## 10. `SequenceReader<byte>`가 `ref struct`인 것이 왜 중요한가요?

<details>
<summary>정답과 해설</summary>

stack과 참조 수명 규칙이 강해 heap에 임의 보관하거나 `await` 경계를 넘을 수 없습니다. 따라서 buffer를 해석하는 동기 decoder와 비동기 read loop를 분리하게 하며, 잘못된 memory 수명 연장을 줄입니다.

</details>

## 11. header 4 byte만 왔고 payload가 부족할 때 header를 소비하면 안 되는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

다음 read에서 나머지 payload가 왔을 때 그 길이를 다시 알 수 없기 때문입니다. 로컬 cursor로 먼저 검사하고 frame 전체가 완성됐을 때만 호출자의 원본 buffer를 다음 위치로 이동해야 합니다.

</details>

## 12. `Decode(ref ReadOnlySequence<byte> buffer, ...)`의 `ref`는 무엇을 허용하나요?

<details>
<summary>정답과 해설</summary>

method가 호출자 변수 자체를 새 slice로 바꿀 수 있게 합니다. 성공했을 때만 `buffer = buffer.Slice(reader.Position)`으로 소비를 commit하고, `NeedMoreData`에서는 원래 sequence를 유지합니다.

</details>

## 13. 왜 0, 음수, 최대 초과 길이를 payload가 오기 전에 거절하나요?

<details>
<summary>정답과 해설</summary>

현재 protocol에서 허용하지 않는 길이는 byte를 더 받아도 유효해질 수 없습니다. 특히 거대한 선언 길이는 큰 allocation이나 끝없는 대기를 유도할 수 있으므로 header만으로 즉시 거절하는 것이 자원 고갈 방어입니다.

</details>

## 14. 정확히 최대 1,024 byte는 성공하고 1,025 byte는 실패해야 하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

계약이 포함 범위 `1..1,024`이기 때문입니다. `>=`와 `>`를 잘못 선택하는 off-by-one 오류를 막기 위해 exact maximum과 maximum+1을 각각 test해야 합니다.

</details>

## 15. payload를 `byte[]`로 복사하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

`PipeReader.ReadAsync`가 준 sequence는 `AdvanceTo` 뒤에 더 이상 사용할 수 없습니다. 제한된 payload를 독립 배열로 복사하면 parser와 Application이 pipe buffer 수명에서 분리됩니다. allocation 비용과 명확한 ownership을 교환한 설계입니다.

</details>

## 16. 복사를 없애면 무조건 더 좋은가요?

<details>
<summary>정답과 해설</summary>

아닙니다. handler가 끝날 때까지 pipe buffer를 잡아야 하고, 비동기 처리·예외·취소 때 lease 반환 책임이 복잡해집니다. 처리량, allocation뿐 아니라 peak memory, backpressure, tail latency, API 안전성을 함께 측정해야 합니다.

</details>

## 17. `PipeReader.AdvanceTo(consumed, examined)`의 두 위치는 무엇이 다른가요?

<details>
<summary>정답과 해설</summary>

`consumed` 전 byte는 더 이상 필요 없어 반환해도 되는 범위이고, `examined` 전 byte는 현재 read에서 확인한 범위입니다. 부분 frame의 시작은 보존하면서 현재 끝까지 검사했다고 표시해야 새 data를 기다릴 수 있습니다.

</details>

## 18. `consumed`를 buffer 끝으로 잘못 지정하면 어떤 일이 생기나요?

<details>
<summary>정답과 해설</summary>

아직 완성되지 않은 header나 payload까지 버립니다. 다음 read에서 이어지는 byte와 연결할 앞부분이 사라져 frame을 복원할 수 없습니다.

</details>

## 19. `examined`를 부분 frame 시작에만 두고 반복하면 어떤 문제가 생길 수 있나요?

<details>
<summary>정답과 해설</summary>

이미 확인한 같은 data만으로 `ReadAsync`가 다시 완료되어 busy loop가 생길 수 있습니다. 현재 받은 끝까지 검사했다면 `examined`를 buffer 끝에 두어 새 data가 올 때까지 기다리게 합니다.

</details>

## 20. `IsCompleted == true`이면 buffer를 보지 않고 바로 끝내도 되나요?

<details>
<summary>정답과 해설</summary>

안 됩니다. 마지막 read가 data와 completion을 함께 반환할 수 있습니다. 먼저 완전한 frame을 모두 처리하고, 남은 tail이 없을 때만 정상 EOF로 끝냅니다.

</details>

## 21. 잘린 header와 잘린 payload를 왜 다른 오류 code로 구분하나요?

<details>
<summary>정답과 해설</summary>

둘 다 EOF 실패지만 원인과 송신자 bug 위치가 다릅니다. `frame.truncated_header`는 4 byte도 완성되지 않았고, `frame.truncated_payload`는 길이를 알지만 선언한 body가 덜 왔습니다. 안정적인 code는 운영 진단과 test를 구체적으로 만듭니다.

</details>

## 22. `IAsyncEnumerable<Result<TelemetryReading>>`은 무엇을 표현하나요?

<details>
<summary>정답과 해설</summary>

stream 전체를 기다려 목록 하나를 만드는 대신 frame 결과가 준비될 때마다 비동기로 하나씩 전달합니다. `Result`는 각 frame의 성공 또는 예상 입력 오류를 나타내고, sequence는 입력 순서를 보존합니다.

</details>

## 23. `yield return` 뒤 method가 끝나나요?

<details>
<summary>정답과 해설</summary>

아닙니다. 현재 값을 호출자에게 전달하며 실행 상태를 보존합니다. 호출자가 다음 값을 요청하면 그다음 줄부터 계속합니다. `yield break`는 iterator를 끝냅니다.

</details>

## 24. `finally`에서 `CompleteAsync`를 호출하는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

정상 종료뿐 아니라 예외, 취소, 소비자의 조기 중단에서도 PipeReader가 관리하는 buffer와 내부 자원을 정리하기 위해서입니다. lifecycle을 소유한 Adapter가 완료 책임도 집니다.

</details>

## 25. malformed JSON과 500℃는 왜 다른 오류 종류인가요?

<details>
<summary>정답과 해설</summary>

malformed JSON은 payload 표현 자체를 읽을 수 없어 Protocol 오류입니다. 길이 prefix 덕분에 다음 frame 경계는 알지만, 이 예제는 같은 sender/session의 wire 계약을 더 이상 신뢰하지 않는 엄격한 fail-fast 정책으로 중단합니다. 500℃는 JSON 구조를 읽었지만 Domain 범위만 어긴 Business 오류이므로 해당 frame을 거절하고 계속합니다.

</details>

## 26. 모든 `Exception`을 catch해 `Result`로 바꾸면 왜 위험한가요?

<details>
<summary>정답과 해설</summary>

programmer bug, 메모리 문제, Repository 장애, cancellation까지 정상 입력 실패처럼 숨길 수 있습니다. 이 예제는 예상하는 `JsonException`만 protocol 실패로 바꾸고 그 밖의 장애는 위로 전파합니다.

</details>

## 27. 취소를 Business 오류로 report에 넣지 않는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

취소는 telemetry 값이 업무 규칙을 어긴 것이 아니라 호출자가 작업을 멈춘 제어 흐름입니다. `OperationCanceledException`을 유지해야 상위 host가 취소와 실패를 올바르게 구분하고 retry·종료 정책을 적용할 수 있습니다.

</details>

## 28. 같은 `eventId`의 같은 내용과 다른 내용은 어떻게 다른가요?

<details>
<summary>정답과 해설</summary>

같은 내용은 응답 유실 뒤 재전송처럼 안전한 duplicate이므로 저장을 생략해도 됩니다. 같은 ID인데 장치·시각·온도가 다르면 멱등 key를 잘못 재사용한 conflict이며 조용히 덮어쓰면 data를 잃습니다.

</details>

## 29. `ContainsKey` 다음 `Add` 대신 `TryAdd`를 쓰는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

확인과 추가를 두 단계로 나누면 그 사이에 다른 thread가 같은 key를 추가할 수 있습니다. `ConcurrentDictionary.TryAdd`는 key가 없을 때 추가하는 결정을 하나의 원자적 연산으로 수행합니다. 다만 이 원자성은 현재 process 안에 한정됩니다.

</details>

## 30. Repository Port가 주는 핵심 이점은 무엇인가요?

<details>
<summary>정답과 해설</summary>

Application Service가 `ConcurrentDictionary`, SQL, Redis 같은 구체 저장 기술을 모르게 합니다. 생성자에 다른 Adapter나 test double을 넣을 수 있어 저장 기술 교체와 단위 검사가 쉬워집니다.

</details>

## 31. `IFrameDecoder`와 `ITelemetryPayloadParser`를 나눈 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

frame 경계를 찾는 byte protocol 규칙과 payload 의미를 해석하는 JSON/Domain 규칙은 서로 다른 변화 축입니다. 분리하면 newline framing 또는 다른 payload 형식을 Application Service 수정 없이 교체하고 각각 독립 검사할 수 있습니다.

</details>

## 32. 이것이 Strategy 패턴인 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

같은 interface 뒤에 교체 가능한 동작 규칙을 캡슐화하고 호출자는 구체 알고리즘을 모릅니다. `LengthPrefixedFrameDecoder` 대신 다른 `IFrameDecoder` 구현을 주입할 수 있습니다.

</details>

## 33. DI container가 없는데도 의존성 주입이라고 부를 수 있나요?

<details>
<summary>정답과 해설</summary>

그렇습니다. DI의 핵심은 객체가 필요한 의존성을 내부에서 직접 만들지 않고 생성자를 통해 밖에서 받는 것입니다. container는 이 조립을 자동화하는 선택 도구입니다.

</details>

## 34. Composition Root를 `Program` 한곳에 둔 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

어떤 Port에 어떤 Adapter와 Strategy를 연결하는지 한곳에서 보이게 합니다. 구체 기술 의존이 Domain/Application 안쪽으로 새지 않고, 운영과 test 조립을 다르게 만들기 쉽습니다.

</details>

## 35. `System.IO.Pipelines`를 Infrastructure에 가둔 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

PipeReader의 buffer 수명과 cursor 규칙은 transport 세부사항입니다. 이를 안쪽 계층에 노출하면 모든 업무 코드가 `AdvanceTo`와 sequence 수명을 알아야 합니다. Adapter가 그 책임을 끝내고 검증된 Domain 결과만 내보내면 결합도가 낮아집니다.

</details>

## 36. `ChunkedReadStream`이 실제 network test를 완전히 대신하나요?

<details>
<summary>정답과 해설</summary>

아닙니다. 부분 read를 결정적으로 재현하는 빠른 학습 fixture입니다. 실제 socket에서는 timeout, half-close, 연결 reset, 동시성, OS buffer, TLS, backpressure도 검사해야 합니다. test double의 보장 범위를 과장하면 안 됩니다.

</details>

## 37. metric label에 `eventId`나 `deviceId`를 넣으면 왜 위험한가요?

<details>
<summary>정답과 해설</summary>

값 종류가 매우 많아 time series 수가 폭증하는 높은 카디널리티 문제가 생깁니다. 비용과 query 성능이 악화되고 식별 정보가 노출될 수 있습니다. 결과 종류처럼 제한된 값만 label로 쓰고 개별 ID는 정책에 맞는 log/trace에서 다룹니다.

</details>

## 38. 현재 메모리 Repository를 여러 process 운영에 그대로 쓰면 안 되는 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

각 process가 서로 다른 dictionary를 가지므로 전역 중복을 막지 못하고 재시작하면 data가 사라집니다. 운영에서는 shared persistent storage, unique constraint나 compare-and-set, transaction, 장애 후 retry 의미가 필요합니다.

</details>

## 39. 최신 C# 15 Preview 문법을 실행 코드에 넣지 않은 이유는 무엇인가요?

<details>
<summary>정답과 해설</summary>

현재 설치된 안정 SDK 10.0.301과 C# 14로 누구나 동일하게 build하도록 하기 위해서입니다. Preview 기능은 변경될 수 있고 .NET 11 prerelease SDK가 필요하므로 설명과 실행 가능한 stable 예제를 분리했습니다.

</details>

## 40. 운영 gateway로 확장할 때 가장 먼저 다시 정할 계약은 무엇인가요?

<details>
<summary>정답과 해설</summary>

하나의 정답은 없지만 protocol version/인증·무결성, frame·연결 자원 상한과 timeout, persistent idempotency, backpressure, restart/checkpoint, 관측 가능성과 개인정보 정책을 명시해야 합니다. 코드 최적화 전에 실패와 ownership 계약부터 정하는 것이 중요합니다.

</details>

## 41. 정상 frame을 저장한 뒤 protocol 오류가 나면 앞선 저장도 rollback되나요?

<details>
<summary>정답과 해설</summary>

아닙니다. 현재 Application Service는 stream 전체 transaction이 아니라 frame별 증분 처리를 사용합니다. protocol 오류 전에 끝난 저장은 유지되고 전체 결과는 실패입니다. 호출자가 같은 stream을 다시 보내면 앞선 frame은 `eventId` 멱등성으로 duplicate가 되며, 전부 rollback이 필요하다면 staging과 transaction/checkpoint를 별도로 설계해야 합니다.

</details>
