using GlobalMeetingApi.Application;
using GlobalMeetingApi.Domain;
using GlobalMeetingApi.Infrastructure;
using GlobalMeetingApi.Presentation;

namespace GlobalMeetingApi.SelfTesting;

/// <summary>
/// 별도 test framework 없이도 시간대 경계와 저장 규칙을 한 명령으로 검증하는 학습용 실행기입니다.
/// </summary>
public static class SelfTestRunner
{
    /// <summary>
    /// 정상 변환, DST 공백·중복, offset 형식, UTC 충돌, 취소 전파를 차례로 검사합니다.
    /// </summary>
    /// <returns>모든 검사가 통과하면 0, 첫 실패가 예외로 드러나면 프로세스가 비정상 종료됩니다.</returns>
    public static async Task<int> RunAsync()
    {
        var passed = 0;
        var resolver = new TimeZoneInfoLocalTimeResolver(new RequireExplicitOffsetPolicy());
        var repository = new InMemoryMeetingRepository();
        var fixedNow = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
        var service = new ScheduleMeetingService(
            resolver,
            repository,
            new FixedTimeProvider(fixedNow));

        var parsedPositive = RequireSuccess(UtcOffsetParser.Parse("+09:00"), "양수 offset 파싱");
        // ref는 Check가 같은 passed 변수를 직접 증가시켜 호출자에게 변경을 돌려주는 참조 전달 문법입니다.
        Check(parsedPositive.Value == TimeSpan.FromHours(9), "+09:00 파싱", ref passed);

        var invalidOffset = UtcOffsetParser.Parse("09:00");
        Check(
            !invalidOffset.IsSuccess && invalidOffset.Error.Code == "utc_offset.invalid_format",
            "offset 부호 누락 거부",
            ref passed);

        var seoulInput = RequireSuccess(
            LocalMeetingTime.Create(
                new DateOnly(2026, 10, 23),
                new TimeOnly(9, 0),
                "Asia/Seoul",
                null),
            "서울 입력 생성");
        var seoul = RequireSuccess(resolver.Resolve(seoulInput), "서울 시각 변환");
        Check(
            seoul.UtcInstant == new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero),
            "서울 09:00을 UTC 00:00으로 변환",
            ref passed);

        var invalidSpringInput = RequireSuccess(
            LocalMeetingTime.Create(
                new DateOnly(2026, 3, 8),
                new TimeOnly(2, 30),
                "America/New_York",
                null),
            "DST 공백 입력 생성");
        var invalidSpring = resolver.Resolve(invalidSpringInput);
        Check(
            !invalidSpring.IsSuccess && invalidSpring.Error.Code == "local_time.invalid",
            "DST 시작의 존재하지 않는 02:30 거부",
            ref passed);

        var ambiguousWithoutOffsetInput = RequireSuccess(
            LocalMeetingTime.Create(
                new DateOnly(2026, 11, 1),
                new TimeOnly(1, 30),
                "America/New_York",
                null),
            "DST 중복 입력 생성");
        var ambiguousWithoutOffset = resolver.Resolve(ambiguousWithoutOffsetInput);
        Check(
            !ambiguousWithoutOffset.IsSuccess
                && ambiguousWithoutOffset.Error.Code == "local_time.ambiguous"
                && ambiguousWithoutOffset.Error.AllowedValues?.Count == 2,
            "DST 종료 중복 시각에 두 offset 선택 요구",
            ref passed);

        var daylightInput = RequireSuccess(
            LocalMeetingTime.Create(
                new DateOnly(2026, 11, 1),
                new TimeOnly(1, 30),
                "America/New_York",
                TimeSpan.FromHours(-4)),
            "첫 번째 01:30 입력 생성");
        var daylight = RequireSuccess(resolver.Resolve(daylightInput), "첫 번째 01:30 변환");
        Check(
            daylight.UtcInstant == new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero),
            "-04:00의 01:30을 UTC 05:30으로 변환",
            ref passed);

        var standardInput = RequireSuccess(
            LocalMeetingTime.Create(
                new DateOnly(2026, 11, 1),
                new TimeOnly(1, 30),
                "America/New_York",
                TimeSpan.FromHours(-5)),
            "두 번째 01:30 입력 생성");
        var standard = RequireSuccess(resolver.Resolve(standardInput), "두 번째 01:30 변환");
        Check(
            standard.UtcInstant == new DateTimeOffset(2026, 11, 1, 6, 30, 0, TimeSpan.Zero),
            "-05:00의 01:30을 UTC 06:30으로 변환",
            ref passed);

        var impossibleOffsetInput = RequireSuccess(
            LocalMeetingTime.Create(
                new DateOnly(2026, 11, 1),
                new TimeOnly(1, 30),
                "America/New_York",
                TimeSpan.FromHours(-6)),
            "잘못된 중복 offset 입력 생성");
        var impossibleOffset = resolver.Resolve(impossibleOffsetInput);
        Check(
            !impossibleOffset.IsSuccess
                && impossibleOffset.Error.Code == "utc_offset.not_valid_for_local_time",
            "시간대 후보에 없는 -06:00 거부",
            ref passed);

        var minimumDateInput = RequireSuccess(
            LocalMeetingTime.Create(
                DateOnly.MinValue,
                TimeOnly.MinValue,
                "Etc/GMT-14",
                null),
            "최소 날짜 입력 생성");
        var minimumDate = resolver.Resolve(minimumDateInput);
        Check(
            !minimumDate.IsSuccess && minimumDate.Error.Code == "local_time.out_of_range",
            "offset 적용 뒤 UTC 최소 범위 초과를 안전하게 거부",
            ref passed);

        var first = await service.ScheduleAsync(
            CreateCommand(
                "MEET-DST-001",
                "USER-100",
                new DateOnly(2026, 11, 1),
                new TimeOnly(1, 30),
                "America/New_York",
                TimeSpan.FromHours(-4),
                60),
            CancellationToken.None);
        Check(first.IsSuccess, "첫 회의 저장", ref passed);

        var sameInstantDifferentZone = await service.ScheduleAsync(
            CreateCommand(
                "MEET-UTC-002",
                "USER-100",
                new DateOnly(2026, 11, 1),
                new TimeOnly(5, 45),
                "Etc/UTC",
                null,
                30),
            CancellationToken.None);
        Check(
            !sameInstantDifferentZone.IsSuccess
                && sameInstantDifferentZone.Error.Code == "organizer.time_conflict",
            "표시 시간대가 달라도 UTC 구간 충돌 거부",
            ref passed);

        var adjacent = await service.ScheduleAsync(
            CreateCommand(
                "MEET-UTC-003",
                "USER-100",
                new DateOnly(2026, 11, 1),
                new TimeOnly(6, 30),
                "Etc/UTC",
                null,
                30),
            CancellationToken.None);
        Check(adjacent.IsSuccess, "앞 회의 종료와 맞닿은 다음 회의 허용", ref passed);

        const int competingRequestCount = 24;
        var readyCount = 0;

        // TaskCompletionSource는 직접 완료 신호를 보낼 수 있는 비동기 관문입니다.
        // 모든 작업이 준비될 때까지 기다렸다가 한꺼번에 출발시켜 실제 경쟁 상황을 재현합니다.
        var allReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Range와 Select는 24개 경쟁 명령을 만들고, Task.Run은 각 명령을 ThreadPool 작업으로 예약합니다.
        var competingTasks = Enumerable.Range(1, competingRequestCount)
            .Select(index => Task.Run(async () =>
            {
                // Interlocked는 여러 thread가 readyCount를 동시에 바꿔도 증가 결과가 사라지지 않게 합니다.
                if (Interlocked.Increment(ref readyCount) == competingRequestCount)
                {
                    allReady.TrySetResult(true);
                }

                // await는 thread를 붙잡지 않고 시작 신호를 기다렸다가, 신호 뒤 예약 요청을 계속합니다.
                await startSignal.Task;
                return await service.ScheduleAsync(
                    CreateCommand(
                        $"MEET-RACE-{index:00}",
                        "USER-RACE",
                        new DateOnly(2026, 12, 1),
                        new TimeOnly(9, 0),
                        "Etc/UTC",
                        null,
                        60),
                    CancellationToken.None);
            }))
            .ToArray();

        try
        {
            // WaitAsync의 제한 시간은 준비 단계가 고장 났을 때 self-test가 영원히 멈추는 일을 막습니다.
            await allReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            // 준비 확인이 실패해도 대기 작업을 풀어 시작 신호에서 무기한 기다리는 작업을 남기지 않습니다.
            startSignal.TrySetResult(true);
        }

        var competingResults = await Task.WhenAll(competingTasks);
        Check(
            competingResults.Count(result => result.IsSuccess) == 1
                && competingResults.Count(result =>
                    !result.IsSuccess
                    && result.Error.Code == "organizer.time_conflict") == 23,
            "24개 경쟁 요청에서 정확히 1개만 원자적으로 저장",
            ref passed);

        var duplicate = await service.ScheduleAsync(
            CreateCommand(
                "MEET-DST-001",
                "USER-200",
                new DateOnly(2026, 11, 2),
                new TimeOnly(9, 0),
                "Asia/Seoul",
                null,
                30),
            CancellationToken.None);
        Check(
            !duplicate.IsSuccess && duplicate.Error.Code == "meeting_id.duplicate",
            "회의 ID 중복 거부",
            ref passed);

        var tooSoon = await service.ScheduleAsync(
            CreateCommand(
                "MEET-SOON-001",
                "USER-300",
                new DateOnly(2026, 10, 22),
                new TimeOnly(0, 15),
                "Etc/UTC",
                null,
                30),
            CancellationToken.None);
        Check(
            !tooSoon.IsSuccess && tooSoon.Error.Code == "meeting.too_soon",
            "고정 시계 기준 30분 미만 시작 거부",
            ref passed);

        var found = await service.GetAsync(" meet-dst-001 ", CancellationToken.None);
        Check(
            found.IsSuccess && found.Value.MeetingId == "MEET-DST-001",
            "정규화된 ID 조회",
            ref passed);

        // using var는 현재 메서드가 끝날 때 CancellationTokenSource.Dispose를 자동 호출합니다.
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancellationPropagated = false;
        try
        {
            await service.GetAsync("MEET-DST-001", cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            cancellationPropagated = true;
        }

        Check(cancellationPropagated, "취소를 업무 오류로 바꾸지 않고 전파", ref passed);

        Console.WriteLine($"[SELF-TEST] {passed}/17 checks passed");
        return 0;
    }

    /// <summary>
    /// 반복되는 정상 명령 필드를 한곳에 모아 각 검사의 시간대 차이만 보이게 합니다.
    /// </summary>
    /// <param name="meetingId">검사마다 구분되는 회의 ID입니다.</param>
    /// <param name="organizerId">UTC 충돌을 묶을 주최자 ID입니다.</param>
    /// <param name="date">지역 달력 날짜입니다.</param>
    /// <param name="time">지역 벽시계 시각입니다.</param>
    /// <param name="timeZoneId">IANA 시간대 ID입니다.</param>
    /// <param name="offset">중복 시각에서 선택할 offset입니다.</param>
    /// <param name="durationMinutes">회의 길이입니다.</param>
    /// <returns>Application Service에 전달할 명령을 반환합니다.</returns>
    private static ScheduleMeetingCommand CreateCommand(
        string meetingId,
        string organizerId,
        DateOnly date,
        TimeOnly time,
        string timeZoneId,
        TimeSpan? offset,
        int durationMinutes)
    {
        return new ScheduleMeetingCommand(
            meetingId,
            organizerId,
            "글로벌 일정 실습",
            date,
            time,
            timeZoneId,
            offset,
            durationMinutes);
    }

    /// <summary>
    /// 성공이어야 하는 준비 단계의 값을 꺼내고 실패면 검사 이름과 함께 즉시 중단합니다.
    /// </summary>
    /// <typeparam name="T">Result가 성공할 때 담는 reference type입니다.</typeparam>
    /// <param name="result">성공을 기대하는 Result입니다.</param>
    /// <param name="label">실패 메시지에 표시할 준비 단계 이름입니다.</param>
    /// <returns>성공 Result의 null이 아닌 값을 반환합니다.</returns>
    private static T RequireSuccess<T>(Result<T> result, string label)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException(
                $"{label} 실패: {result.Error.Code} - {result.Error.Message}");
    }

    /// <summary>
    /// 조건이 참이면 통과 수를 늘리고 거짓이면 이해하기 쉬운 예외로 자체 검증을 멈춥니다.
    /// </summary>
    /// <param name="condition">검증하려는 기대 조건입니다.</param>
    /// <param name="name">콘솔과 예외에 표시할 검사 이름입니다.</param>
    /// <param name="passed">통과한 검사 수를 호출자에게 되돌려 줄 참조 매개변수입니다.</param>
    /// <returns>값을 만들지 않고 성공 시 통과 수만 변경합니다.</returns>
    private static void Check(bool condition, string name, ref int passed)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"[FAIL] {name}");
        }

        passed++;
        Console.WriteLine($"[PASS] {name}");
    }

    /// <summary>
    /// 테스트가 실행 날짜와 무관하도록 항상 같은 UTC 현재 시각을 제공하는 Test Double입니다.
    /// </summary>
    /// <param name="utcNow">모든 GetUtcNow 호출이 반환할 고정 UTC 순간입니다.</param>
    // primary constructor는 고정 시각을 짧게 받아 override 메서드에서 재사용하게 합니다.
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <summary>
        /// 생성 시 받은 고정 UTC 순간을 반환합니다.
        /// </summary>
        /// <returns>자체 검증 전체에서 변하지 않는 UTC 현재 시각을 반환합니다.</returns>
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
