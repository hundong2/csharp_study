using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using TelemetryStreamExercise.Application;
using TelemetryStreamExercise.Application.Ports;
using TelemetryStreamExercise.Domain;
using TelemetryStreamExercise.Infrastructure;
using TelemetryStreamExercise.Infrastructure.Protocol;

namespace TelemetryStreamExercise.SelfTesting;

/// <summary>
/// 부분 frame, 여러 frame, 잘못된 길이, EOF, JSON, Domain, 멱등성, 취소 계약을 자동 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    private const int DefaultMaximumFrameLength = 1_024;
    // target-typed new는 왼쪽 DateTimeOffset 형식이 분명해 생성자 형식 이름을 생략한 문법입니다.
    private static readonly DateTimeOffset BaseTime =
        new(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 서로 독립된 protocol·application self-test를 모두 실행하고 process exit code를 반환합니다.
    /// </summary>
    /// <returns>모든 검사가 통과하면 0, 하나라도 실패하면 1을 담은 Task를 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        // var는 오른쪽 new Checkbook()으로 형식이 분명해 compiler가 정적 형식을 추론하는 문법입니다.
        var checks = new Checkbook();
        TestBigEndianEncoding(checks);
        TestDecoderMaximumBoundary(checks);
        TestMultiSegmentDecoder(checks);
        await TestFragmentedHeaderAndPayloadAsync(checks);
        await TestMultipleFramesAsync(checks);
        await TestEmptyStreamAndExactEofAsync(checks);
        await TestInvalidLengthsAsync(checks);
        await TestTruncatedEofAsync(checks);
        await TestMalformedJsonAsync(checks);
        await TestProtocolFailurePreservesEarlierSideEffectAsync(checks);
        await TestMissingRequiredTemperatureAsync(checks);
        await TestMissingAndInvalidCapturedAtAsync(checks);
        await TestBusinessErrorContinuesAsync(checks);
        await TestIdempotentDuplicateAsync(checks);
        await TestWholeStreamReplayAsync(checks);
        await TestIdempotencyConflictAsync(checks);
        await TestConcurrentRepositoryWriteAsync(checks);
        await TestCancellationAsync(checks);
        await TestBufferedFrameCancellationAsync(checks);
        await TestPendingReadCancellationAsync(checks);
        await TestEarlyConsumerStopAsync(checks);
        return checks.Finish();
    }

    /// <summary>
    /// encoder가 문자열 글자 수가 아니라 UTF-8 byte 수를 4-byte big-endian으로 기록하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestBigEndianEncoding(Checkbook checks)
    {
        var json = CreateJson("evt-한글", "센서-서울", BaseTime, 20m);
        var frame = LengthPrefixedFrameEncoder.EncodeJson(json);
        var declaredLength = BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(0, sizeof(int)));
        var utf8Length = Encoding.UTF8.GetByteCount(json);

        checks.Equal(utf8Length, declaredLength, "길이 prefix는 UTF-8 byte 수를 기록한다");
        checks.Equal(utf8Length + sizeof(int), frame.Length, "frame은 4-byte header와 payload로 구성된다");
        checks.True(frame[0] == 0 && frame[1] == 0, "작은 길이는 big-endian 상위 byte가 먼저 0으로 온다");
    }

    /// <summary>
    /// decoder가 설정한 최대 길이는 허용하고 그보다 한 byte 큰 선언은 payload 할당 전에 거절하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestDecoderMaximumBoundary(Checkbook checks)
    {
        const int maximum = 8;
        var decoder = new LengthPrefixedFrameDecoder(maximum);
        var exactFrame = LengthPrefixedFrameEncoder.EncodePayload(new byte[maximum]);
        var exactBuffer = new ReadOnlySequence<byte>(exactFrame);
        var exactResult = decoder.Decode(ref exactBuffer, isCompleted: true);

        checks.Equal(
            Application.Ports.FrameDecodeStatus.FrameDecoded,
            exactResult.Status,
            "최대 길이와 정확히 같은 payload는 framing 단계에서 허용한다");
        checks.True(exactBuffer.IsEmpty, "완성 frame만 buffer에서 소비한다");

        var oversizedBuffer = new ReadOnlySequence<byte>(
            LengthPrefixedFrameEncoder.EncodeLengthPrefix(maximum + 1));
        var oversizedResult = decoder.Decode(ref oversizedBuffer, isCompleted: false);
        // ?.는 Error가 없으면 Code 접근 대신 null을 만드는 null-conditional 문법입니다.
        checks.Equal(
            "frame.length_too_large",
            oversizedResult.Error?.Code,
            "최대 길이 초과는 body를 기다리지 않고 즉시 거절한다");
    }

    /// <summary>
    /// header와 payload가 실제로 서로 다른 ReadOnlySequence segment에 놓여도 SequenceReader가 경계를 넘는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동기 검사를 누적하므로 반환값은 없습니다.</returns>
    private static void TestMultiSegmentDecoder(Checkbook checks)
    {
        var json = CreateJson("evt-segments", "센서-분할", BaseTime, 17m);
        var frame = LengthPrefixedFrameEncoder.EncodeJson(json);
        var sequence = CreateSequence(
            frame.AsMemory(0, 1),
            frame.AsMemory(1, 2),
            frame.AsMemory(3, 5),
            frame.AsMemory(8));
        var decoder = new LengthPrefixedFrameDecoder(DefaultMaximumFrameLength);
        var result = decoder.Decode(ref sequence, isCompleted: true);

        checks.Equal(
            Application.Ports.FrameDecodeStatus.FrameDecoded,
            result.Status,
            "multi-segment header와 payload를 한 frame으로 읽는다");
        checks.Equal(
            json,
            Encoding.UTF8.GetString(result.Payload.Span),
            "segment 경계와 관계없이 payload byte를 정확히 보존한다");
        checks.True(sequence.IsEmpty, "multi-segment 완성 frame을 정확히 소비한다");
    }

    /// <summary>
    /// 모든 read가 1 byte만 반환해 header, JSON token, 한글 UTF-8 문자가 갈라져도 한 frame을 복원하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>비동기 stream 처리가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestFragmentedHeaderAndPayloadAsync(Checkbook checks)
    {
        var bytes = EncodeFrames(CreateJson("evt-fragment", "센서-서울", BaseTime, 21.25m));
        var execution = await ExecuteAsync(
            bytes,
            DefaultMaximumFrameLength,
            CancellationToken.None,
            1);

        checks.True(execution.Result.IsSuccess, "1-byte partial read도 완전한 frame으로 복원한다");
        if (!execution.Result.IsSuccess)
        {
            return;
        }

        checks.Equal(1, execution.Result.Value.AcceptedCount, "조각난 정상 frame 한 건을 저장한다");
        checks.Equal(1, execution.Repository.Count, "조각난 입력을 중복 저장하지 않는다");
        checks.True(execution.ReadCount > 4, "header와 payload가 실제 여러 read로 나뉜다");
    }

    /// <summary>
    /// 한 stream에 연속된 여러 frame과 다음 frame의 partial byte가 있어도 순서대로 정확히 한 번씩 처리하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>비동기 stream 처리가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestMultipleFramesAsync(Checkbook checks)
    {
        var bytes = EncodeFrames(
            CreateJson("evt-multi-1", "sensor-a", BaseTime, 10m),
            CreateJson("evt-multi-2", "sensor-b", BaseTime.AddSeconds(1), 11m),
            CreateJson("evt-multi-3", "sensor-c", BaseTime.AddSeconds(2), 12m));
        var execution = await ExecuteAsync(
            bytes,
            DefaultMaximumFrameLength,
            CancellationToken.None,
            bytes.Length);

        checks.True(execution.Result.IsSuccess, "연속된 여러 frame stream을 성공 처리한다");
        if (!execution.Result.IsSuccess)
        {
            return;
        }

        checks.Equal(3, execution.Result.Value.AcceptedCount, "세 frame을 빠뜨리지 않고 저장한다");
        checks.Equal(3, execution.Repository.Count, "각 event ID는 정확히 한 번 존재한다");
        checks.Equal(0, execution.Result.Value.BusinessErrors.Length, "정상 다중 frame에는 business 오류가 없다");
        checks.Equal(1, execution.ReadCount, "세 frame byte가 한 번의 source read에 함께 도착한다");
    }

    /// <summary>
    /// byte가 전혀 없는 EOF와 payload 끝에 정확히 맞는 EOF를 정상 종료로 처리하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>두 비동기 stream 처리가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestEmptyStreamAndExactEofAsync(Checkbook checks)
    {
        var empty = await ExecuteAsync(
            [],
            DefaultMaximumFrameLength,
            CancellationToken.None,
            1);
        checks.True(empty.Result.IsSuccess, "빈 stream EOF는 빈 성공 report다");
        if (empty.Result.IsSuccess)
        {
            checks.Equal(0, empty.Result.Value.AcceptedCount, "빈 stream은 저장 0건이다");
        }

        var exact = await ExecuteAsync(
            EncodeFrames(CreateJson("evt-eof", "sensor-eof", BaseTime, 1m)),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            3);
        checks.True(exact.Result.IsSuccess, "완전한 payload 직후 EOF는 protocol 오류가 아니다");
        if (exact.Result.IsSuccess)
        {
            checks.Equal(1, exact.Result.Value.AcceptedCount, "EOF 직전 완성 frame도 처리한다");
        }
    }

    /// <summary>
    /// 0, 음수, 최대 초과, Int32 최대 길이를 각각 안정적인 protocol 오류로 거절하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>네 비동기 실패 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestInvalidLengthsAsync(Checkbook checks)
    {
        var zero = await ExecuteAsync(
            LengthPrefixedFrameEncoder.EncodeLengthPrefix(0),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            4);
        checks.Equal("frame.length_zero", ErrorCode(zero.Result), "길이 0을 거절한다");

        var negative = await ExecuteAsync(
            LengthPrefixedFrameEncoder.EncodeLengthPrefix(-1),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            2);
        checks.Equal("frame.length_negative", ErrorCode(negative.Result), "음수 길이를 거절한다");

        var oversized = await ExecuteAsync(
            LengthPrefixedFrameEncoder.EncodeLengthPrefix(DefaultMaximumFrameLength + 1),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            4);
        checks.Equal("frame.length_too_large", ErrorCode(oversized.Result), "최대보다 한 byte 큰 길이를 거절한다");

        var integerMaximum = await ExecuteAsync(
            LengthPrefixedFrameEncoder.EncodeLengthPrefix(int.MaxValue),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            4);
        checks.Equal("frame.length_too_large", ErrorCode(integerMaximum.Result), "Int32 최대 길이도 할당 전에 거절한다");
    }

    /// <summary>
    /// EOF에서 4-byte header나 선언한 payload가 덜 왔을 때 두 오류를 구분하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>두 비동기 EOF 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestTruncatedEofAsync(Checkbook checks)
    {
        for (var byteCount = 1; byteCount <= 3; byteCount++)
        {
            var headerBytes = new byte[byteCount];
            var partialHeader = await ExecuteAsync(
                headerBytes,
                DefaultMaximumFrameLength,
                CancellationToken.None,
                1);
            checks.Equal(
                "frame.truncated_header",
                ErrorCode(partialHeader.Result),
                $"EOF의 {byteCount}개 header byte를 truncated header로 보고한다");
        }

        var partialPayload = LengthPrefixedFrameEncoder.Concat(
            LengthPrefixedFrameEncoder.EncodeLengthPrefix(10),
            [0x7B, 0x7D]);
        var truncatedPayload = await ExecuteAsync(
            partialPayload,
            DefaultMaximumFrameLength,
            CancellationToken.None,
            2,
            1);
        checks.Equal(
            "frame.truncated_payload",
            ErrorCode(truncatedPayload.Result),
            "선언 길이보다 짧은 EOF를 truncated payload로 보고한다");
        checks.Equal(0, truncatedPayload.Repository.Count, "불완전 payload는 저장하지 않는다");
    }

    /// <summary>
    /// malformed JSON, JSON null, 알 수 없거나 중복된 field를 protocol 오류로 구분하고 뒤 frame을 처리하지 않는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>네 비동기 JSON 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestMalformedJsonAsync(Checkbook checks)
    {
        var validAfterFailure = CreateJson("evt-after", "sensor-after", BaseTime, 10m);
        var malformedBytes = EncodeFrames("{not-json", validAfterFailure);
        var malformed = await ExecuteAsync(
            malformedBytes,
            DefaultMaximumFrameLength,
            CancellationToken.None,
            64);
        checks.Equal("payload.malformed_json", ErrorCode(malformed.Result), "malformed JSON을 protocol 오류로 반환한다");
        checks.Equal(0, malformed.Repository.Count, "protocol 오류 뒤의 정상 frame은 fail-fast로 처리하지 않는다");

        var jsonNull = await ExecuteAsync(
            EncodeFrames("null"),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            4);
        checks.Equal("payload.json_null", ErrorCode(jsonNull.Result), "JSON null을 telemetry 객체로 받아들이지 않는다");

        var unknownField = await ExecuteAsync(
            EncodeFrames(
                "{\"eventId\":\"evt-x\",\"deviceId\":\"sensor-x\","
                + "\"capturedAt\":\"2026-10-26T00:00:00Z\",\"temperatureCelsius\":1,\"typo\":true}"),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            13);
        checks.Equal("payload.malformed_json", ErrorCode(unknownField.Result), "알 수 없는 JSON field를 조용히 무시하지 않는다");

        var duplicateProperty = await ExecuteAsync(
            EncodeFrames(
                "{\"eventId\":\"evt-first\",\"eventId\":\"evt-second\","
                + "\"deviceId\":\"sensor-x\",\"capturedAt\":\"2026-10-26T00:00:00Z\","
                + "\"temperatureCelsius\":1}"),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            29);
        checks.Equal(
            "payload.malformed_json",
            ErrorCode(duplicateProperty.Result),
            "중복 JSON property로 앞값과 뒷값 중 하나를 임의 선택하지 않는다");
    }

    /// <summary>
    /// 정상 frame 뒤 protocol 오류가 나면 앞선 저장은 유지되고 뒤 frame은 처리하지 않는 증분 처리 계약을 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>첫 실행과 전체 stream 재시도 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestProtocolFailurePreservesEarlierSideEffectAsync(Checkbook checks)
    {
        var repository = new InMemoryTelemetryRepository();
        var service = new TelemetryIngestService(
            new PipelinesTelemetryStreamReader(
                new LengthPrefixedFrameDecoder(DefaultMaximumFrameLength),
                new JsonTelemetryPayloadParser()),
            repository);
        var bytes = EncodeFrames(
            CreateJson("evt-before-protocol-error", "sensor-a", BaseTime, 10m),
            "{not-json",
            CreateJson("evt-after-protocol-error", "sensor-b", BaseTime.AddSeconds(1), 11m));

        await using var firstStream = new ChunkedReadStream(bytes, bytes.Length);
        var first = await service.IngestAsync(firstStream, CancellationToken.None);
        checks.Equal("payload.malformed_json", ErrorCode(first), "앞선 성공 뒤 protocol 오류를 호출자에게 반환한다");
        checks.Equal(1, repository.Count, "protocol 오류 전에 저장한 frame은 rollback하지 않는다");

        await using var replayStream = new ChunkedReadStream(bytes, bytes.Length);
        var replay = await service.IngestAsync(replayStream, CancellationToken.None);
        checks.Equal("payload.malformed_json", ErrorCode(replay), "전체 재시도도 같은 protocol 위치에서 결정적으로 실패한다");
        checks.Equal(1, repository.Count, "전체 재시도에서 앞선 frame은 duplicate가 되어 side effect를 늘리지 않는다");
    }

    /// <summary>
    /// 필수 temperatureCelsius property 누락을 유효한 0도로 오해하지 않고 Business 오류로 거절하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>비동기 누락 field 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestMissingRequiredTemperatureAsync(Checkbook checks)
    {
        var json = "{\"eventId\":\"evt-missing-temperature\",\"deviceId\":\"sensor-a\","
            + "\"capturedAt\":\"2026-10-26T00:00:00Z\"}";
        var execution = await ExecuteAsync(
            EncodeFrames(json),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            11);

        checks.True(execution.Result.IsSuccess, "필수 온도 누락은 framing 실패가 아니라 Business 오류다");
        if (!execution.Result.IsSuccess)
        {
            return;
        }

        checks.Equal(0, execution.Result.Value.AcceptedCount, "온도 누락 telemetry를 저장하지 않는다");
        checks.Equal(1, execution.Result.Value.BusinessErrors.Length, "온도 누락 오류 한 건을 report에 남긴다");
        checks.Equal(
            "telemetry.temperature_required",
            execution.Result.Value.BusinessErrors[0].Code,
            "온도 누락과 실제 0도를 구분한다");
    }

    /// <summary>
    /// capturedAt 누락과 명시적인 DateTimeOffset 최소값을 서로 다른 Business 오류로 구분하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>두 비동기 시각 field 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestMissingAndInvalidCapturedAtAsync(Checkbook checks)
    {
        var missingJson = "{\"eventId\":\"evt-missing-time\",\"deviceId\":\"sensor-a\","
            + "\"temperatureCelsius\":0}";
        var missing = await ExecuteAsync(
            EncodeFrames(missingJson),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            9);

        checks.True(missing.Result.IsSuccess, "capturedAt 누락은 framing 실패가 아니라 Business 오류다");
        if (missing.Result.IsSuccess)
        {
            checks.Equal(0, missing.Result.Value.AcceptedCount, "capturedAt 누락 telemetry를 저장하지 않는다");
            checks.Equal(
                "telemetry.captured_at_required",
                missing.Result.Value.BusinessErrors.Single().Code,
                "capturedAt 누락 전용 오류 코드를 반환한다");
        }

        var minimumJson = "{\"eventId\":\"evt-min-time\",\"deviceId\":\"sensor-a\","
            + "\"capturedAt\":\"0001-01-01T00:00:00+00:00\",\"temperatureCelsius\":0}";
        var minimum = await ExecuteAsync(
            EncodeFrames(minimumJson),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            7);

        checks.True(minimum.Result.IsSuccess, "명시적인 최소 시각도 Business 검증 결과로 처리한다");
        if (minimum.Result.IsSuccess)
        {
            checks.Equal(
                "telemetry.captured_at_invalid",
                minimum.Result.Value.BusinessErrors.Single().Code,
                "시각 누락과 명시적인 잘못된 값의 오류를 구분한다");
        }
    }

    /// <summary>
    /// 구조적으로 올바른 JSON의 Domain 오류는 report에 모으고 다음 정상 frame은 계속 저장하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>비동기 stream 처리가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestBusinessErrorContinuesAsync(Checkbook checks)
    {
        var bytes = EncodeFrames(
            CreateJson("evt-domain", "   ", BaseTime, 20m),
            CreateJson("evt-valid", "sensor-valid", BaseTime.AddSeconds(1), 22m));
        var execution = await ExecuteAsync(
            bytes,
            DefaultMaximumFrameLength,
            CancellationToken.None,
            5,
            1,
            9);

        checks.True(execution.Result.IsSuccess, "Domain 오류는 stream 전체 protocol 실패가 아니다");
        if (!execution.Result.IsSuccess)
        {
            return;
        }

        var report = execution.Result.Value;
        checks.Equal(1, report.AcceptedCount, "Domain 오류 다음 정상 frame을 계속 저장한다");
        checks.Equal(1, report.BusinessErrors.Length, "거절한 Domain frame 한 건을 report에 남긴다");
        checks.Equal(
            "telemetry.device_id_required",
            report.BusinessErrors[0].Code,
            "Domain 오류 코드는 validation 원인을 보존한다");
    }

    /// <summary>
    /// 같은 event ID와 같은 내용의 재전송을 한 번만 저장하고 duplicate로 세는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>비동기 멱등 저장 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestIdempotentDuplicateAsync(Checkbook checks)
    {
        var json = CreateJson("evt-duplicate", "sensor-a", BaseTime, 19m);
        var execution = await ExecuteAsync(
            EncodeFrames(json, json, json),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            1,
            31);

        checks.True(execution.Result.IsSuccess, "동일 재전송은 성공 report로 처리한다");
        if (!execution.Result.IsSuccess)
        {
            return;
        }

        checks.Equal(1, execution.Result.Value.AcceptedCount, "첫 event만 새로 저장한다");
        checks.Equal(2, execution.Result.Value.DuplicateCount, "뒤의 같은 두 event를 duplicate로 센다");
        checks.Equal(1, execution.Repository.Count, "동일 재전송 뒤에도 저장소에는 한 건만 있다");
    }

    /// <summary>
    /// 첫 실행이 완료된 뒤 같은 stream 전체를 재시도해도 Repository side effect가 한 번만 생기는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>두 번의 비동기 stream 처리가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestWholeStreamReplayAsync(Checkbook checks)
    {
        var repository = new InMemoryTelemetryRepository();
        var decoder = new LengthPrefixedFrameDecoder(DefaultMaximumFrameLength);
        var parser = new JsonTelemetryPayloadParser();
        var streamReader = new PipelinesTelemetryStreamReader(decoder, parser);
        var service = new TelemetryIngestService(streamReader, repository);
        var bytes = EncodeFrames(CreateJson("evt-replay", "sensor-replay", BaseTime, 18m));

        await using var firstStream = new ChunkedReadStream(bytes, 2, 1);
        var first = await service.IngestAsync(firstStream, CancellationToken.None);
        await using var replayStream = new ChunkedReadStream(bytes, 3, 1);
        var replay = await service.IngestAsync(replayStream, CancellationToken.None);

        checks.True(first.IsSuccess && replay.IsSuccess, "같은 stream 전체 재시도도 정상 완료한다");
        if (!first.IsSuccess || !replay.IsSuccess)
        {
            return;
        }

        checks.Equal(1, first.Value.AcceptedCount, "첫 실행은 event를 새로 저장한다");
        checks.Equal(0, replay.Value.AcceptedCount, "재실행은 같은 event를 다시 저장하지 않는다");
        checks.Equal(1, replay.Value.DuplicateCount, "재실행 event를 duplicate로 보고한다");
        checks.Equal(1, repository.Count, "전체 재시도 뒤에도 side effect는 한 번뿐이다");
    }

    /// <summary>
    /// 같은 event ID를 다른 내용으로 재사용하면 안전한 duplicate가 아니라 business 충돌인지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>비동기 충돌 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestIdempotencyConflictAsync(Checkbook checks)
    {
        var execution = await ExecuteAsync(
            EncodeFrames(
                CreateJson("evt-conflict", "sensor-a", BaseTime, 10m),
                CreateJson("evt-conflict", "sensor-a", BaseTime, 11m)),
            DefaultMaximumFrameLength,
            CancellationToken.None,
            17);

        checks.True(execution.Result.IsSuccess, "ID 내용 충돌은 protocol 실패가 아니다");
        if (!execution.Result.IsSuccess)
        {
            return;
        }

        var report = execution.Result.Value;
        checks.Equal(1, report.AcceptedCount, "충돌 전 첫 event는 저장한다");
        checks.Equal(0, report.DuplicateCount, "내용이 다른 event를 duplicate로 세지 않는다");
        checks.Equal(1, report.BusinessErrors.Length, "ID 내용 충돌 한 건을 거절한다");
        checks.Equal(
            "telemetry.idempotency_conflict",
            report.BusinessErrors[0].Code,
            "ID 재사용 충돌 코드를 보존한다");
        checks.Equal(1, execution.Repository.Count, "충돌 payload가 기존 값을 덮어쓰지 않는다");
    }

    /// <summary>
    /// 여러 Task가 같은 event를 동시에 저장하려 해도 원자적 TryAdd가 신규 한 건만 허용하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>동시 저장 시도들이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestConcurrentRepositoryWriteAsync(Checkbook checks)
    {
        var readingResult = TelemetryReading.Create(
            "evt-concurrent",
            "sensor-concurrent",
            BaseTime,
            15m);
        if (!readingResult.IsSuccess)
        {
            checks.True(false, "동시성 test fixture telemetry가 유효하다");
            return;
        }

        var repository = new InMemoryTelemetryRepository();
        // TaskCompletionSource는 모든 작업이 같은 시작 신호를 기다리게 해 TryAdd 경합을 더 선명하게 만듭니다.
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = Enumerable.Range(0, 32)
            .Select(async _ =>
            {
                await start.Task;
                return await repository.StoreIfAbsentAsync(
                    readingResult.Value,
                    CancellationToken.None);
            })
            .ToArray();
        start.SetResult(true);
        var statuses = await Task.WhenAll(writes);

        checks.Equal(1, statuses.Count(status => status == Application.Ports.RepositoryWriteStatus.Stored), "동시 저장 중 정확히 한 건만 Stored다");
        checks.Equal(31, statuses.Count(status => status == Application.Ports.RepositoryWriteStatus.Duplicate), "나머지 동시 재전송은 Duplicate다");
        checks.Equal(1, repository.Count, "동시 재전송 뒤 저장소에도 한 건만 남는다");
    }

    /// <summary>
    /// 호출자의 취소를 실패 Result로 바꾸지 않고 OperationCanceledException으로 그대로 전파하는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>취소 예외 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestCancellationAsync(Checkbook checks)
    {
        // using statement는 검사가 끝나면 CancellationTokenSource의 native 자원을 반드시 정리합니다.
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var bytes = EncodeFrames(CreateJson("evt-cancel", "sensor-cancel", BaseTime, 10m));

        // async lambda는 인수가 없고 취소 예외를 비동기로 관찰할 작은 함수를 값처럼 전달합니다.
        await checks.ThrowsAsync<OperationCanceledException>(
            async () =>
            {
                _ = await ExecuteAsync(
                    bytes,
                    DefaultMaximumFrameLength,
                    cancellation.Token,
                    1);
            },
            "취소는 Result 오류로 숨기지 않고 OperationCanceledException으로 전파한다");
    }

    /// <summary>
    /// 한 read buffer에 frame이 여러 개 있어도 첫 저장 뒤 취소되면 다음 payload를 parse하지 않는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>buffer 내부 frame 사이의 취소 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestBufferedFrameCancellationAsync(Checkbook checks)
    {
        using var cancellation = new CancellationTokenSource();
        var parser = new CountingPayloadParser(new JsonTelemetryPayloadParser());
        var repository = new CancelAfterFirstWriteRepository(
            new InMemoryTelemetryRepository(),
            cancellation);
        var service = new TelemetryIngestService(
            new PipelinesTelemetryStreamReader(
                new LengthPrefixedFrameDecoder(DefaultMaximumFrameLength),
                parser),
            repository);
        var bytes = EncodeFrames(
            CreateJson("evt-buffer-cancel-1", "sensor-a", BaseTime, 1m),
            CreateJson("evt-buffer-cancel-2", "sensor-b", BaseTime.AddSeconds(1), 2m));

        await using var stream = new ChunkedReadStream(bytes, bytes.Length);
        await checks.ThrowsAsync<OperationCanceledException>(
            async () => _ = await service.IngestAsync(stream, cancellation.Token),
            "한 read에 남은 frame 사이에서도 취소를 전파한다");
        checks.Equal(1, parser.ParseCount, "취소 뒤 다음 buffered payload를 parse하지 않는다");
        checks.Equal(1, repository.Count, "취소 전 완료한 첫 저장만 유지한다");
    }

    /// <summary>
    /// 아직 byte가 하나도 없어 ReadAsync가 대기 중인 순간의 취소도 즉시 전파되고 reader가 정리되는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>pending read 취소 검사가 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestPendingReadCancellationAsync(Checkbook checks)
    {
        var repository = new InMemoryTelemetryRepository();
        var streamReader = new PipelinesTelemetryStreamReader(
            new LengthPrefixedFrameDecoder(DefaultMaximumFrameLength),
            new JsonTelemetryPayloadParser());
        var service = new TelemetryIngestService(streamReader, repository);
        var pipe = new Pipe();
        using var cancellation = new CancellationTokenSource();
        await using var pendingStream = pipe.Reader.AsStream(leaveOpen: true);

        var pendingIngest = service.IngestAsync(pendingStream, cancellation.Token);
        cancellation.Cancel();
        await checks.ThrowsAsync<OperationCanceledException>(
            async () => _ = await pendingIngest,
            "대기 중인 Pipe read 취소를 OperationCanceledException으로 전파한다");
        await pipe.Writer.CompleteAsync();
        await pipe.Reader.CompleteAsync();
        checks.Equal(0, repository.Count, "pending read 취소는 저장 side effect를 만들지 않는다");
    }

    /// <summary>
    /// async sequence 소비자가 첫 값 뒤 break해도 iterator의 read별 finally와 CompleteAsync가 실행되어 종료되는지 검사합니다.
    /// </summary>
    /// <param name="checks">검사 성공과 실패를 누적할 assertion 도구입니다.</param>
    /// <returns>조기 async iterator disposal이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestEarlyConsumerStopAsync(Checkbook checks)
    {
        var reader = new PipelinesTelemetryStreamReader(
            new LengthPrefixedFrameDecoder(DefaultMaximumFrameLength),
            new JsonTelemetryPayloadParser());
        var bytes = EncodeFrames(
            CreateJson("evt-stop-1", "sensor-a", BaseTime, 1m),
            CreateJson("evt-stop-2", "sensor-b", BaseTime.AddSeconds(1), 2m));
        await using var stream = new ChunkedReadStream(bytes, bytes.Length);
        var observed = 0;

        await foreach (var item in reader.ReadAsync(stream, CancellationToken.None))
        {
            checks.True(item.IsSuccess, "조기 종료 전에 첫 frame을 정상 decode한다");
            observed++;
            break;
        }

        checks.Equal(1, observed, "소비자 break는 첫 결과만 관찰하고 iterator를 정리한다");
        checks.Equal(1, stream.ReadCount, "조기 종료 test는 한 read buffer 안에 다음 frame도 둔다");
    }

    /// <summary>
    /// test용 JSON 문자열들을 각각 frame으로 만든 뒤 한 stream byte 배열로 합칩니다.
    /// </summary>
    /// <param name="jsonPayloads">입력 순서대로 frame에 넣을 JSON 문자열들입니다.</param>
    /// <returns>모든 frame이 이어진 byte 배열을 반환합니다.</returns>
    private static byte[] EncodeFrames(params string[] jsonPayloads)
    {
        ArgumentNullException.ThrowIfNull(jsonPayloads);
        // Select에 method 이름을 전달하는 method group은 각 JSON을 frame으로 바꾸는 함수를 값처럼 넘깁니다.
        var frames = jsonPayloads.Select(LengthPrefixedFrameEncoder.EncodeJson).ToArray();
        return LengthPrefixedFrameEncoder.Concat(frames);
    }

    /// <summary>
    /// test 값을 serializer로 안전하게 escape해 transport JSON을 만듭니다.
    /// </summary>
    /// <param name="eventId">멱등 key로 사용할 event ID입니다.</param>
    /// <param name="deviceId">측정 장치 ID입니다.</param>
    /// <param name="capturedAt">offset을 포함한 측정 시각입니다.</param>
    /// <param name="temperatureCelsius">섭씨 온도입니다.</param>
    /// <returns>camelCase property를 가진 JSON 문자열을 반환합니다.</returns>
    private static string CreateJson(
        string eventId,
        string deviceId,
        DateTimeOffset capturedAt,
        decimal temperatureCelsius)
    {
        return JsonSerializer.Serialize(
            new TestContract(eventId, deviceId, capturedAt, temperatureCelsius),
            JsonOptions);
    }

    /// <summary>
    /// 새 Application graph와 ChunkedReadStream으로 한 test 입력을 끝까지 실행합니다.
    /// </summary>
    /// <param name="bytes">stream이 반환할 전체 frame byte입니다.</param>
    /// <param name="maximumFrameLength">decoder가 허용할 최대 payload byte입니다.</param>
    /// <param name="cancellationToken">use case와 stream read에 전달할 취소 신호입니다.</param>
    /// <param name="chunkSizes">read마다 순환하며 적용할 최대 chunk 크기들입니다.</param>
    /// <returns>Application 결과, Repository, 실제 read 횟수를 묶은 TestExecution을 반환합니다.</returns>
    private static async Task<TestExecution> ExecuteAsync(
        byte[] bytes,
        int maximumFrameLength,
        CancellationToken cancellationToken,
        params int[] chunkSizes)
    {
        var repository = new InMemoryTelemetryRepository();
        var decoder = new LengthPrefixedFrameDecoder(maximumFrameLength);
        var parser = new JsonTelemetryPayloadParser();
        var streamReader = new PipelinesTelemetryStreamReader(decoder, parser);
        var service = new TelemetryIngestService(streamReader, repository);

        await using var stream = new ChunkedReadStream(bytes, chunkSizes);
        var result = await service.IngestAsync(stream, cancellationToken);
        return new TestExecution(result, repository, stream.ReadCount);
    }

    /// <summary>
    /// 실패 Result의 code를 반환하고, 예상과 달리 성공이면 검사에서 구별할 sentinel을 반환합니다.
    /// </summary>
    /// <param name="result">오류 code를 확인할 Application 결과입니다.</param>
    /// <returns>실패 code 또는 성공을 나타내는 `&lt;success&gt;`를 반환합니다.</returns>
    private static string ErrorCode(Result<IngestReport> result)
    {
        return result.IsSuccess ? "<success>" : result.Error.Code;
    }

    /// <summary>
    /// 여러 ReadOnlyMemory 조각을 연결해 실제 multi-segment ReadOnlySequence를 만듭니다.
    /// </summary>
    /// <param name="segments">빈 조각 없이 순서대로 연결할 byte memory들입니다.</param>
    /// <returns>첫 조각부터 마지막 조각까지 이어진 ReadOnlySequence를 반환합니다.</returns>
    private static ReadOnlySequence<byte> CreateSequence(params ReadOnlyMemory<byte>[] segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Length == 0 || segments.Any(segment => segment.IsEmpty))
        {
            throw new ArgumentException("하나 이상의 비어 있지 않은 segment가 필요합니다.", nameof(segments));
        }

        var first = new ByteSequenceSegment(segments[0]);
        var last = first;
        foreach (var segment in segments.Skip(1))
        {
            last = last.Append(segment);
        }

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    /// <summary>
    /// test JSON serializer에 전달할 transport 값 묶음입니다.
    /// </summary>
    /// <param name="EventId">event ID입니다.</param>
    /// <param name="DeviceId">장치 ID입니다.</param>
    /// <param name="CapturedAt">측정 시각입니다.</param>
    /// <param name="TemperatureCelsius">섭씨 온도입니다.</param>
    private sealed record TestContract(
        string EventId,
        string DeviceId,
        DateTimeOffset CapturedAt,
        decimal TemperatureCelsius);

    /// <summary>
    /// 한 self-test 실행 뒤 함께 확인할 Application 결과와 Adapter 상태를 묶습니다.
    /// </summary>
    /// <param name="Result">Application Service가 반환한 성공 report 또는 protocol 오류입니다.</param>
    /// <param name="Repository">저장 건수를 확인할 메모리 Adapter입니다.</param>
    /// <param name="ReadCount">ChunkedReadStream이 실제 byte를 반환한 횟수입니다.</param>
    private sealed record TestExecution(
        Result<IngestReport> Result,
        InMemoryTelemetryRepository Repository,
        int ReadCount);

    /// <summary>
    /// 실제 parser에 위임하면서 parse 호출 횟수를 관찰하는 test decorator입니다.
    /// </summary>
    private sealed class CountingPayloadParser : ITelemetryPayloadParser
    {
        private readonly ITelemetryPayloadParser _inner;

        /// <summary>
        /// 관찰할 실제 parser를 받습니다.
        /// </summary>
        /// <param name="inner">payload를 실제로 해석할 parser입니다.</param>
        public CountingPayloadParser(ITelemetryPayloadParser inner)
        {
            _inner = inner;
        }

        /// <summary>
        /// 지금까지 실제 parser에 위임한 횟수입니다.
        /// </summary>
        public int ParseCount { get; private set; }

        /// <summary>
        /// 호출 횟수를 하나 기록한 뒤 실제 parser에 payload 해석을 위임합니다.
        /// </summary>
        /// <param name="payload">해석할 소유권 있는 JSON UTF-8 memory입니다.</param>
        /// <returns>실제 parser가 만든 Domain 성공 또는 입력 오류를 반환합니다.</returns>
        public Result<TelemetryReading> Parse(ReadOnlyMemory<byte> payload)
        {
            ParseCount++;
            return _inner.Parse(payload);
        }
    }

    /// <summary>
    /// 첫 저장을 마친 직후 취소 신호를 보내는 test 전용 Repository decorator입니다.
    /// </summary>
    private sealed class CancelAfterFirstWriteRepository : ITelemetryRepository
    {
        private readonly InMemoryTelemetryRepository _inner;
        private readonly CancellationTokenSource _cancellation;
        private int _writeCount;

        /// <summary>
        /// 실제 저장소와 첫 저장 뒤 취소할 신호 원본을 받습니다.
        /// </summary>
        /// <param name="inner">실제 멱등 저장을 수행할 메모리 Adapter입니다.</param>
        /// <param name="cancellation">첫 저장 뒤 취소할 token source입니다.</param>
        public CancelAfterFirstWriteRepository(
            InMemoryTelemetryRepository inner,
            CancellationTokenSource cancellation)
        {
            _inner = inner;
            _cancellation = cancellation;
        }

        /// <summary>
        /// 실제 저장소에 현재 남아 있는 서로 다른 event 수입니다.
        /// </summary>
        public int Count => _inner.Count;

        /// <summary>
        /// 실제 저장 결과를 받은 뒤 첫 쓰기라면 취소해 다음 buffered frame 처리를 멈춥니다.
        /// </summary>
        /// <param name="reading">저장할 검증된 telemetry입니다.</param>
        /// <param name="cancellationToken">취소 여부를 실제 저장소에도 전달하는 신호입니다.</param>
        /// <returns>실제 저장소가 결정한 Stored, Duplicate, Conflict를 반환합니다.</returns>
        public async ValueTask<RepositoryWriteStatus> StoreIfAbsentAsync(
            TelemetryReading reading,
            CancellationToken cancellationToken)
        {
            var status = await _inner.StoreIfAbsentAsync(reading, cancellationToken);
            _writeCount++;
            if (_writeCount == 1)
            {
                _cancellation.Cancel();
            }

            return status;
        }
    }

    /// <summary>
    /// self-test에서 여러 memory 조각을 ReadOnlySequence로 연결하기 위한 최소 segment node입니다.
    /// </summary>
    private sealed class ByteSequenceSegment : ReadOnlySequenceSegment<byte>
    {
        /// <summary>
        /// 첫 segment 또는 뒤에 붙일 segment의 memory를 보관합니다.
        /// </summary>
        /// <param name="memory">이 node가 나타낼 비어 있지 않은 byte memory입니다.</param>
        public ByteSequenceSegment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        /// <summary>
        /// 현재 node 뒤에 새 node를 연결하고 전체 sequence 안의 시작 offset을 계산합니다.
        /// </summary>
        /// <param name="memory">새 node가 나타낼 byte memory입니다.</param>
        /// <returns>뒤에 연결한 새 마지막 node를 반환합니다.</returns>
        public ByteSequenceSegment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new ByteSequenceSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = next;
            return next;
        }
    }
}
