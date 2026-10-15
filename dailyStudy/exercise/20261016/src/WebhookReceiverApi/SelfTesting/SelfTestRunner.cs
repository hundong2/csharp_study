using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WebhookReceiverApi.Application;
using WebhookReceiverApi.Application.Ports;
using WebhookReceiverApi.Domain;
using WebhookReceiverApi.Infrastructure;

namespace WebhookReceiverApi.SelfTesting;

/// <summary>
/// 외부 테스트 패키지 없이 핵심 보안 불변식과 동시 재전송 방지를 실행 가능한 예제로 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    private const string TestSecret = "local-training-secret-0123456789abcdef";
    private const string ValidBody =
        "{\"type\":\"payment.succeeded\",\"data\":{\"paymentId\":\"pay-1001\",\"amount\":12900,\"currency\":\"KRW\"}}";

    /// <summary>
    /// 모든 자체 테스트를 실행하고 프로세스가 사용할 종료 코드를 정합니다.
    /// </summary>
    /// <returns>모든 검증이 통과하면 0, 하나라도 실패하면 1인 Task를 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        // var는 오른쪽 new 식에서 TestContext라는 정적 형식을 추론합니다.
        var tests = new TestContext();
        try
        {
            await RunScenariosAsync(tests);
            Console.WriteLine($"SELF-TEST PASSED: {tests.PassedCount} assertions");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SELF-TEST FAILED: {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 서명 canonicalization, 시간창, JSON/Domain 검증, 원자적 replay 저장의 성공·실패 경로를 차례로 확인합니다.
    /// </summary>
    /// <param name="tests">통과 개수를 세고 실패 시 즉시 예외를 만드는 검증 도우미입니다.</param>
    /// <returns>모든 비동기 시나리오가 끝났음을 나타내는 Task를 반환합니다.</returns>
    private static async Task RunScenariosAsync(TestContext tests)
    {
        var now = new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(now);
        var verifier = CreateVerifier(TestSecret, clock);
        var parser = new SystemTextJsonWebhookPayloadParser();
        var repository = new InMemoryWebhookReceiptRepository();
        var service = new WebhookReceiverApplicationService(verifier, parser, repository);
        var timestamp = now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var body = Encoding.UTF8.GetBytes(ValidBody);

        var knownSignature = CreateSignature(
            TestSecret,
            timestamp,
            "evt-vector-001",
            body);
        tests.Equal(
            "v1=c3910bba7746b75df3d30d17e1541455087ef27f6983bbe0d1397f6fa4d2d378",
            knownSignature,
            "고정 입력의 HMAC known vector가 변하지 않는다.");

        var accepted = await service.ReceiveAsync(
            CreateCommand(TestSecret, timestamp, "evt-happy-001", body),
            CancellationToken.None);
        tests.True(accepted.IsSuccess, "올바른 서명과 payload는 접수된다.");
        tests.Equal("evt-happy-001", accepted.Value!.EventId.Value, "접수 결과가 event ID를 보존한다.");
        tests.Equal("pay-1001", accepted.Value.PaymentEvent.PaymentId, "접수 결과가 paymentId를 보존한다.");
        tests.Equal(12_900m, accepted.Value.PaymentEvent.Amount, "접수 결과가 decimal amount를 보존한다.");
        tests.Equal("KRW", accepted.Value.PaymentEvent.Currency, "접수 결과가 통화 코드를 보존한다.");
        tests.Equal(now, accepted.Value.SignedAtUtc, "서명 Unix seconds를 UTC 시각으로 변환한다.");
        tests.Equal(now, accepted.Value.ReceivedAtUtc, "TimeProvider 시각을 수신 시각으로 저장한다.");

        var found = await service.FindAsync("evt-happy-001", CancellationToken.None);
        tests.True(found.IsSuccess, "접수한 event ID를 Repository에서 조회할 수 있다.");
        tests.Equal(accepted.Value, found.Value, "조회 결과는 저장한 불변 receipt와 같다.");

        var replay = await service.ReceiveAsync(
            CreateCommand(TestSecret, timestamp, "evt-happy-001", body),
            CancellationToken.None);
        AssertError(tests, replay, "webhook.replayed", "같은 event ID 재전송은 거절된다.");

        var tamperedBody = Encoding.UTF8.GetBytes(
            ValidBody.Replace("12900", "12901", StringComparison.Ordinal));
        var tampered = await service.ReceiveAsync(
            new ReceiveWebhookCommand(
                "evt-tampered-001",
                timestamp,
                CreateSignature(TestSecret, timestamp, "evt-tampered-001", body),
                tamperedBody),
            CancellationToken.None);
        AssertError(tests, tampered, "webhook.unauthorized", "서명 뒤 body 한 바이트가 바뀌면 인증에 실패한다.");

        var signedEventId = "evt-original-001";
        var changedEventId = await service.ReceiveAsync(
            new ReceiveWebhookCommand(
                "evt-changed-001",
                timestamp,
                CreateSignature(TestSecret, timestamp, signedEventId, body),
                body),
            CancellationToken.None);
        AssertError(tests, changedEventId, "webhook.unauthorized", "event ID 변경도 HMAC이 감지한다.");

        var changedTimestampText = (now + TimeSpan.FromSeconds(1)).ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var changedTimestamp = await service.ReceiveAsync(
            new ReceiveWebhookCommand(
                "evt-changed-timestamp-001",
                changedTimestampText,
                CreateSignature(
                    TestSecret,
                    timestamp,
                    "evt-changed-timestamp-001",
                    body),
                body),
            CancellationToken.None);
        AssertError(tests, changedTimestamp, "webhook.unauthorized", "서명 뒤 timestamp 변경도 HMAC이 감지한다.");

        var differentlySpacedBody = Encoding.UTF8.GetBytes(
            "{ \"type\":\"payment.succeeded\",\"data\":{\"paymentId\":\"pay-1001\",\"amount\":12900,\"currency\":\"KRW\"}}");
        var spacingChanged = await service.ReceiveAsync(
            new ReceiveWebhookCommand(
                "evt-spacing-001",
                timestamp,
                CreateSignature(TestSecret, timestamp, "evt-spacing-001", body),
                differentlySpacedBody),
            CancellationToken.None);
        AssertError(tests, spacingChanged, "webhook.unauthorized", "JSON 의미가 같아도 raw byte 공백이 바뀌면 인증에 실패한다.");

        var wrongSecret = await service.ReceiveAsync(
            CreateCommand(
                "different-training-secret-0123456789abc",
                timestamp,
                "evt-wrong-key-001",
                body),
            CancellationToken.None);
        AssertError(tests, wrongSecret, "webhook.unauthorized", "다른 공유 secret으로 만든 HMAC은 거절된다.");

        var malformedSignature = await service.ReceiveAsync(
            new ReceiveWebhookCommand("evt-bad-signature-001", timestamp, "v1=abc", body),
            CancellationToken.None);
        AssertError(tests, malformedSignature, "webhook.unauthorized", "길이가 잘못된 signature는 안전한 공통 오류로 거절된다.");

        var uppercaseSignatureText = CreateSignature(
            TestSecret,
            timestamp,
            "evt-uppercase-001",
            body).ToUpperInvariant();
        var uppercaseSignature = await service.ReceiveAsync(
            new ReceiveWebhookCommand("evt-uppercase-001", timestamp, uppercaseSignatureText, body),
            CancellationToken.None);
        AssertError(tests, uppercaseSignature, "webhook.unauthorized", "signature는 v1과 lowercase hex 정규형만 허용한다.");

        var nonCanonicalTimestamp = await service.ReceiveAsync(
            CreateCommand(TestSecret, $"0{timestamp}", "evt-leading-zero-001", body),
            CancellationToken.None);
        AssertError(tests, nonCanonicalTimestamp, "webhook.timestamp.invalid", "앞에 0이 붙은 비정규 timestamp를 거절한다.");

        var oldTimestamp = (now - TimeSpan.FromSeconds(301)).ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var stale = await service.ReceiveAsync(
            CreateCommand(TestSecret, oldTimestamp, "evt-stale-001", body),
            CancellationToken.None);
        AssertError(tests, stale, "webhook.timestamp.out_of_window", "301초 오래된 요청은 시간창 밖이다.");

        var oldBoundaryTimestamp = (now - TimeSpan.FromSeconds(300)).ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var oldBoundary = await service.ReceiveAsync(
            CreateCommand(TestSecret, oldBoundaryTimestamp, "evt-old-boundary-001", body),
            CancellationToken.None);
        tests.True(oldBoundary.IsSuccess, "정확히 300초 오래된 요청은 허용 경계에 포함된다.");

        clock.SetUtcNow(now.AddMilliseconds(999));
        var subSecondBoundary = await service.ReceiveAsync(
            CreateCommand(TestSecret, oldBoundaryTimestamp, "evt-subsecond-boundary-001", body),
            CancellationToken.None);
        tests.True(
            subSecondBoundary.IsSuccess,
            "현재 시각에 millisecond가 있어도 Unix seconds 기준 300초 경계는 허용한다.");
        clock.SetUtcNow(now);

        var futureTimestamp = (now + TimeSpan.FromSeconds(301)).ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var future = await service.ReceiveAsync(
            CreateCommand(TestSecret, futureTimestamp, "evt-future-001", body),
            CancellationToken.None);
        AssertError(tests, future, "webhook.timestamp.out_of_window", "301초 미래 요청은 시간창 밖이다.");

        var futureBoundaryTimestamp = (now + TimeSpan.FromSeconds(300)).ToUnixTimeSeconds()
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var futureBoundary = await service.ReceiveAsync(
            CreateCommand(TestSecret, futureBoundaryTimestamp, "evt-future-boundary-001", body),
            CancellationToken.None);
        tests.True(futureBoundary.IsSuccess, "정확히 300초 미래 요청은 허용 경계에 포함된다.");

        var recoverableEventId = "evt-invalid-then-valid-001";
        var invalidFirst = await service.ReceiveAsync(
            new ReceiveWebhookCommand(recoverableEventId, timestamp, "v1=" + new string('0', 64), body),
            CancellationToken.None);
        AssertError(tests, invalidFirst, "webhook.unauthorized", "잘못된 서명은 접수되지 않는다.");
        var validAfterInvalid = await service.ReceiveAsync(
            CreateCommand(TestSecret, timestamp, recoverableEventId, body),
            CancellationToken.None);
        tests.True(validAfterInvalid.IsSuccess, "잘못된 서명은 replay 저장소를 오염시키지 않는다.");

        var malformedJsonBody = Encoding.UTF8.GetBytes("{");
        var malformedJson = await service.ReceiveAsync(
            CreateCommand(TestSecret, timestamp, "evt-json-001", malformedJsonBody),
            CancellationToken.None);
        AssertError(tests, malformedJson, "webhook.payload.invalid_json", "유효하게 서명돼도 깨진 JSON은 거절된다.");

        var missingFieldBody = Encoding.UTF8.GetBytes(
            "{\"type\":\"payment.succeeded\",\"data\":{\"paymentId\":\"pay-1001\",\"amount\":12900}}");
        var missingField = await service.ReceiveAsync(
            CreateCommand(TestSecret, timestamp, "evt-missing-field-001", missingFieldBody),
            CancellationToken.None);
        AssertError(tests, missingField, "webhook.payload.invalid_json", "필수 currency 누락은 JSON 계약 오류다.");

        var extraFieldBody = Encoding.UTF8.GetBytes(
            "{\"type\":\"payment.succeeded\",\"data\":{\"paymentId\":\"pay-1001\",\"amount\":12900,\"currency\":\"KRW\"},\"unexpected\":true}");
        var extraField = await service.ReceiveAsync(
            CreateCommand(TestSecret, timestamp, "evt-extra-field-001", extraFieldBody),
            CancellationToken.None);
        AssertError(tests, extraField, "webhook.payload.invalid_json", "모르는 JSON 필드는 계약 드리프트로 거절된다.");

        var duplicatePropertyBody = Encoding.UTF8.GetBytes(
            "{\"type\":\"payment.succeeded\",\"type\":\"payment.failed\",\"data\":{\"paymentId\":\"pay-1001\",\"amount\":12900,\"currency\":\"KRW\"}}");
        var duplicateProperty = await service.ReceiveAsync(
            CreateCommand(TestSecret, timestamp, "evt-duplicate-property-001", duplicatePropertyBody),
            CancellationToken.None);
        AssertError(tests, duplicateProperty, "webhook.payload.invalid_json", "중복 JSON 속성의 애매한 해석을 거절한다.");

        await AssertDomainErrorAsync(
            tests,
            service,
            timestamp,
            "evt-type-001",
            "{\"type\":\"refund.created\",\"data\":{\"paymentId\":\"pay-1001\",\"amount\":12900,\"currency\":\"KRW\"}}",
            "webhook.type.unsupported",
            "지원하지 않는 event type은 Domain 오류다.");
        await AssertDomainErrorAsync(
            tests,
            service,
            timestamp,
            "evt-amount-001",
            "{\"type\":\"payment.succeeded\",\"data\":{\"paymentId\":\"pay-1001\",\"amount\":0,\"currency\":\"KRW\"}}",
            "payment.amount.invalid",
            "0원 결제는 Domain 오류다.");
        await AssertDomainErrorAsync(
            tests,
            service,
            timestamp,
            "evt-currency-001",
            "{\"type\":\"payment.succeeded\",\"data\":{\"paymentId\":\"pay-1001\",\"amount\":12900,\"currency\":\"krw\"}}",
            "payment.currency.invalid",
            "소문자 통화 코드는 Domain 오류다.");

        var failedBody = Encoding.UTF8.GetBytes(
            "{\"type\":\"payment.failed\",\"data\":{\"paymentId\":\"pay-failed-1\",\"amount\":5000,\"currency\":\"KRW\"}}");
        var failedEvent = await service.ReceiveAsync(
            CreateCommand(TestSecret, timestamp, "evt-failed-001", failedBody),
            CancellationToken.None);
        tests.True(failedEvent.IsSuccess, "payment.failed도 명시적으로 지원한다.");
        tests.Equal(
            WebhookEventKind.PaymentFailed,
            failedEvent.Value!.PaymentEvent.Kind,
            "payment.failed 문자열을 올바른 enum으로 바꾼다.");

        var concurrentEventId = "evt-concurrent-001";
        var concurrentCommand = CreateCommand(
            TestSecret,
            timestamp,
            concurrentEventId,
            body);
        // TaskCompletionSource는 모든 작업이 같은 시작 신호를 기다리게 해 원자성 경쟁을 재현합니다.
        var startGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        // => lambda는 각 번호를 같은 시작 gate를 기다리는 비동기 요청 하나로 바꿉니다.
        var attempts = Enumerable.Range(0, 32)
            .Select(
                async _ =>
                {
                    await startGate.Task;
                    return await service.ReceiveAsync(
                        concurrentCommand,
                        CancellationToken.None);
                })
            .ToArray();
        startGate.SetResult();
        var concurrentResults = await Task.WhenAll(attempts);
        tests.Equal(1, concurrentResults.Count(result => result.IsSuccess), "동시 32개 요청 중 정확히 하나만 저장된다.");
        tests.Equal(
            31,
            concurrentResults.Count(result => result.Error?.Code == "webhook.replayed"),
            "동시 요청의 나머지 31개는 replay 오류다.");

        var cancelledInvalidBody = Encoding.UTF8.GetBytes("{");
        var cancelledInvalidCommand = CreateCommand(
            TestSecret,
            timestamp,
            "evt-cancelled-invalid-001",
            cancelledInvalidBody);
        using var earlyCancellationSource = new CancellationTokenSource();
        earlyCancellationSource.Cancel();
        var earlyCancellationObserved = false;
        try
        {
            await service.ReceiveAsync(
                cancelledInvalidCommand,
                earlyCancellationSource.Token);
        }
        catch (OperationCanceledException)
        {
            earlyCancellationObserved = true;
        }

        tests.True(
            earlyCancellationObserved,
            "이미 취소된 요청은 잘못된 JSON을 파싱하기 전에 취소를 우선 관찰한다.");
        var invalidAfterCancellation = await service.ReceiveAsync(
            cancelledInvalidCommand,
            CancellationToken.None);
        AssertError(
            tests,
            invalidAfterCancellation,
            "webhook.payload.invalid_json",
            "취소된 잘못된 요청도 event ID를 점유하지 않는다.");

        var cancelledEventId = "evt-cancelled-001";
        var cancelledCommand = CreateCommand(TestSecret, timestamp, cancelledEventId, body);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var cancellationObserved = false;
        try
        {
            await service.ReceiveAsync(cancelledCommand, cancellationSource.Token);
        }
        catch (OperationCanceledException)
        {
            cancellationObserved = true;
        }

        tests.True(cancellationObserved, "취소 신호를 Repository까지 전달한다.");
        var acceptedAfterCancellation = await service.ReceiveAsync(
            cancelledCommand,
            CancellationToken.None);
        tests.True(acceptedAfterCancellation.IsSuccess, "취소된 저장은 event ID를 점유하지 않는다.");

        var missing = await service.FindAsync("evt-missing-001", CancellationToken.None);
        AssertError(tests, missing, "webhook.not_found", "없는 receipt 조회는 명시적 오류다.");
        var invalidId = await service.FindAsync("bad id", CancellationToken.None);
        AssertError(tests, invalidId, "webhook.event_id.invalid", "공백이 든 event ID는 조회 전에 거절된다.");
    }

    /// <summary>
    /// 주어진 JSON을 정확히 서명해 Application Service의 Domain 오류를 검증합니다.
    /// </summary>
    /// <param name="tests">assertion을 기록할 검증 도우미입니다.</param>
    /// <param name="service">검증할 Application Service입니다.</param>
    /// <param name="timestamp">허용 시간창 안의 Unix timestamp 문자열입니다.</param>
    /// <param name="eventId">각 시나리오가 독립적으로 사용할 event ID입니다.</param>
    /// <param name="json">서명하고 전송할 exact JSON 문자열입니다.</param>
    /// <param name="expectedCode">예상하는 Domain 오류 코드입니다.</param>
    /// <param name="message">성공 시 출력할 초보자용 설명입니다.</param>
    /// <returns>비동기 접수와 검증이 끝났음을 나타내는 Task를 반환합니다.</returns>
    private static async Task AssertDomainErrorAsync(
        TestContext tests,
        WebhookReceiverApplicationService service,
        string timestamp,
        string eventId,
        string json,
        string expectedCode,
        string message)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var result = await service.ReceiveAsync(
            CreateCommand(TestSecret, timestamp, eventId, body),
            CancellationToken.None);
        AssertError(tests, result, expectedCode, message);
    }

    /// <summary>
    /// 테스트 secret과 수동 시계로 실제 HMAC Adapter를 만듭니다.
    /// </summary>
    /// <param name="secret">UTF-8 기준 32바이트 이상인 테스트 전용 공유 키입니다.</param>
    /// <param name="clock">결정적인 현재 시각을 제공할 수동 시계입니다.</param>
    /// <returns>운영 코드와 같은 HMAC-SHA256 검증기를 반환합니다.</returns>
    private static IWebhookSignatureVerifier CreateVerifier(
        string secret,
        TimeProvider clock)
    {
        return new HmacSha256WebhookSignatureVerifier(
            Options.Create(
                new WebhookSecurityOptions
                {
                    Secret = secret
                }),
            clock);
    }

    /// <summary>
    /// 테스트 요청의 canonical bytes를 서명해 실제 HTTP 헤더와 같은 문자열을 만듭니다.
    /// </summary>
    /// <param name="secret">공급자와 수신기가 공유한다고 가정한 테스트 키입니다.</param>
    /// <param name="timestamp">서명할 Unix timestamp 문자열입니다.</param>
    /// <param name="eventId">서명 범위에 포함할 event ID입니다.</param>
    /// <param name="rawBody">재직렬화하지 않을 exact body 바이트입니다.</param>
    /// <returns>v1= 접두사와 lowercase 16진수 HMAC이 결합된 값을 반환합니다.</returns>
    private static string CreateSignature(
        string secret,
        string timestamp,
        string eventId,
        ReadOnlySpan<byte> rawBody)
    {
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.{eventId}.");
        var canonical = new byte[prefix.Length + rawBody.Length];
        prefix.CopyTo(canonical, 0);
        rawBody.CopyTo(canonical.AsSpan(prefix.Length));
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), canonical);
        return $"v1={Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    /// <summary>
    /// 정확한 서명을 포함한 Application 명령을 반복 없이 만듭니다.
    /// </summary>
    /// <param name="secret">서명에 사용할 테스트 키입니다.</param>
    /// <param name="timestamp">서명과 명령에 함께 넣을 Unix timestamp입니다.</param>
    /// <param name="eventId">서명과 명령에 함께 넣을 event ID입니다.</param>
    /// <param name="rawBody">서명과 명령에 함께 넣을 exact body입니다.</param>
    /// <returns>서로 일치하는 헤더와 body를 가진 수신 명령을 반환합니다.</returns>
    private static ReceiveWebhookCommand CreateCommand(
        string secret,
        string timestamp,
        string eventId,
        byte[] rawBody)
    {
        return new ReceiveWebhookCommand(
            eventId,
            timestamp,
            CreateSignature(secret, timestamp, eventId, rawBody),
            rawBody);
    }

    /// <summary>
    /// 실패 결과의 오류 코드가 시나리오 계약과 같은지 확인합니다.
    /// </summary>
    /// <typeparam name="T">실패 결과가 성공했을 때 가졌을 값의 형식입니다.</typeparam>
    /// <param name="tests">assertion을 기록할 검증 도우미입니다.</param>
    /// <param name="result">실패할 것으로 예상한 실제 결과입니다.</param>
    /// <param name="expectedCode">기대하는 안정적인 오류 코드입니다.</param>
    /// <param name="message">성공 시 출력할 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
    private static void AssertError<T>(
        TestContext tests,
        Result<T> result,
        string expectedCode,
        string message)
    {
        tests.True(!result.IsSuccess, $"{message} 결과는 실패다.");
        tests.Equal(expectedCode, result.Error?.Code, message);
    }

    /// <summary>
    /// 자체 테스트 assertion 수를 세고 실패 위치를 즉시 알려 주는 작은 테스트 도우미입니다.
    /// </summary>
    private sealed class TestContext
    {
        /// <summary>현재까지 성공한 assertion의 개수입니다.</summary>
        public int PassedCount { get; private set; }

        /// <summary>
        /// 조건이 참인지 검사하고 통과 메시지를 출력합니다.
        /// </summary>
        /// <param name="condition">참이어야 하는 테스트 조건입니다.</param>
        /// <param name="message">검증하려는 동작을 설명하는 문장입니다.</param>
        /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
        public void True(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }

            PassedCount++;
            Console.WriteLine($"[PASS] {message}");
        }

        /// <summary>
        /// 예상 값과 실제 값이 기본 equality 규칙으로 같은지 검사합니다.
        /// </summary>
        /// <typeparam name="T">비교할 두 값의 공통 형식입니다.</typeparam>
        /// <param name="expected">시나리오가 기대하는 값입니다.</param>
        /// <param name="actual">운영 코드가 만든 실제 값입니다.</param>
        /// <param name="message">검증하려는 동작을 설명하는 문장입니다.</param>
        /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
        public void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    $"{message} expected={expected}, actual={actual}");
            }

            PassedCount++;
            Console.WriteLine($"[PASS] {message}");
        }
    }
}
