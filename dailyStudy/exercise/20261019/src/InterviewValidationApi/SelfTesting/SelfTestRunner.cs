using InterviewValidationApi.Application;
using InterviewValidationApi.Application.Ports;
using InterviewValidationApi.Domain;
using InterviewValidationApi.Infrastructure;

namespace InterviewValidationApi.SelfTesting;

/// <summary>
/// 별도 테스트 framework 없이 Domain, Application, Strategy, Repository 경계를 빠르게 회귀 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    private static int _passed;

    /// <summary>
    /// 모든 자체 테스트를 순서대로 실행하고 사람이 읽을 수 있는 PASS/FAIL 요약을 출력합니다.
    /// </summary>
    /// <returns>모두 통과하면 0, 하나라도 실패하면 1인 프로세스 종료 코드를 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        _passed = 0;
        try
        {
            await VerifyDomainBoundaryAsync();
            await VerifyTimeRulesAsync();
            await VerifyBufferStrategyAsync();
            await VerifyAtomicConflictAsync();
            await VerifyCancellationAsync();
            Console.WriteLine($"[SELF-TEST PASS] {_passed} assertions");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[SELF-TEST FAIL] {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    /// <summary>
    /// HTTP 자동 검증을 우회해도 Domain 팩터리가 핵심 불변식을 다시 지키는지 확인합니다.
    /// </summary>
    /// <returns>비동기 테스트 흐름을 나타내는 Task를 반환합니다.</returns>
    private static async Task VerifyDomainBoundaryAsync()
    {
        var context = CreateContext();
        var invalid = await context.Service.ScheduleAsync(
            ValidCommand() with { Mode = "remote", MeetingUrl = "http://unsafe.example.test" },
            CancellationToken.None);

        Assert(!invalid.IsSuccess, "HTTP를 우회한 비 HTTPS 원격 URL을 Domain이 거절한다");
        Assert(invalid.Error?.Code == "interview.invalid_meeting_url", "Domain 오류 code가 안정적이다");
        Assert(context.Repository.Count == 0, "Domain 오류는 Repository를 변경하지 않는다");

        var duplicate = await context.Service.ScheduleAsync(
            ValidCommand() with { InterviewerIds = ["DEV-01", "dev-01"] },
            CancellationToken.None);
        Assert(!duplicate.IsSuccess, "대소문자만 다른 중복 면접관을 거절한다");
        Assert(duplicate.Error?.Code == "interview.duplicate_interviewer", "중복 면접관 오류를 구분한다");
        Assert(context.Repository.Count == 0, "중복 면접관 요청도 저장되지 않는다");

        var normalized = await context.Service.ScheduleAsync(
            ValidCommand() with
            {
                CandidateName = "  홍길동  ",
                CandidateEmail = "  CANDIDATE@EXAMPLE.COM  ",
                InterviewerIds = ["qa-02", "dev-01"]
            },
            CancellationToken.None);
        Assert(normalized.IsSuccess, "유효한 요청은 성공한다");
        Assert(normalized.Value?.CandidateName == "홍길동", "지원자 이름의 양끝 공백을 제거한다");
        Assert(normalized.Value?.CandidateEmail == "candidate@example.com", "이메일을 소문자로 정규화한다");
        Assert(
            normalized.Value?.InterviewerIds.SequenceEqual(["DEV-01", "QA-02"]) == true,
            "면접관 ID를 정규화하고 안정적으로 정렬한다");

        IReadOnlyList<string> exposedInterviewers = normalized.Value!.InterviewerIds;
        var mutationBlocked = false;
        if (exposedInterviewers is IList<string> mutableView)
        {
            try
            {
                mutableView[0] = "MUTATED-01";
            }
            catch (NotSupportedException)
            {
                mutationBlocked = true;
            }
        }
        else
        {
            mutationBlocked = true;
        }

        Assert(mutationBlocked, "공개된 면접관 목록을 cast해도 저장 상태를 변경할 수 없다");
        var stillConflicts = await context.Service.ScheduleAsync(
            ValidCommand() with { CandidateEmail = "immutable-check@example.com" },
            CancellationToken.None);
        Assert(stillConflicts.Error?.Code == "interview.slot_conflict", "변경 시도 뒤에도 원래 면접관 충돌을 탐지한다");
        Assert(context.Repository.Count == 1, "불변 컬렉션 변경 시도로 저장 건수가 늘지 않는다");
    }

    /// <summary>
    /// 현재 시각에 의존하는 최소 15분·최대 180일 규칙이 경계에서 정확한지 확인합니다.
    /// </summary>
    /// <returns>비동기 테스트 흐름을 나타내는 Task를 반환합니다.</returns>
    private static async Task VerifyTimeRulesAsync()
    {
        var context = CreateContext();
        var tooSoon = await context.Service.ScheduleAsync(
            ValidCommand() with { StartsAtUtc = context.Clock.GetUtcNow().AddMinutes(14).AddSeconds(59) },
            CancellationToken.None);
        Assert(tooSoon.Error?.Code == "interview.too_soon", "15분 미만 일정은 거절한다");

        var exactMinimum = await context.Service.ScheduleAsync(
            ValidCommand() with
            {
                StartsAtUtc = context.Clock.GetUtcNow().AddMinutes(15),
                InterviewerIds = ["EDGE-01"]
            },
            CancellationToken.None);
        Assert(exactMinimum.IsSuccess, "정확히 15분 뒤 일정은 허용한다");

        var tooFar = await context.Service.ScheduleAsync(
            ValidCommand() with
            {
                StartsAtUtc = context.Clock.GetUtcNow().AddDays(180).AddSeconds(1),
                InterviewerIds = ["FAR-01"]
            },
            CancellationToken.None);
        Assert(tooFar.Error?.Code == "interview.too_far", "180일을 1초 넘긴 일정은 거절한다");

        var unrepresentableEnd = await context.Service.ScheduleAsync(
            ValidCommand() with
            {
                StartsAtUtc = DateTimeOffset.MaxValue,
                DurationMinutes = 180,
                InterviewerIds = ["MAX-01"]
            },
            CancellationToken.None);
        Assert(
            unrepresentableEnd.Error?.Code == "interview.invalid_time_range",
            "최대 시각에 duration을 더할 수 없으면 예외 대신 검증 오류를 반환한다");

        context.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert(
            context.Clock.GetUtcNow() == new DateTimeOffset(2026, 10, 19, 0, 1, 0, TimeSpan.Zero),
            "TimeProvider를 움직여 시간 규칙 테스트를 재현할 수 있다");
    }

    /// <summary>
    /// 원격 면접 앞뒤 5분 buffer가 충돌 경계에 실제로 반영되는지 확인합니다.
    /// </summary>
    /// <returns>비동기 테스트 흐름을 나타내는 Task를 반환합니다.</returns>
    private static async Task VerifyBufferStrategyAsync()
    {
        var context = CreateContext();
        var first = await context.Service.ScheduleAsync(ValidCommand(), CancellationToken.None);
        Assert(first.IsSuccess, "첫 원격 면접을 저장한다");

        var overlapsBuffer = await context.Service.ScheduleAsync(
            ValidCommand() with
            {
                CandidateEmail = "second@example.com",
                StartsAtUtc = ValidCommand().StartsAtUtc?.AddMinutes(69)
            },
            CancellationToken.None);
        Assert(overlapsBuffer.Error?.Code == "interview.slot_conflict", "종료 후 buffer와 1분 겹치면 충돌한다");

        var exactBoundary = await context.Service.ScheduleAsync(
            ValidCommand() with
            {
                CandidateEmail = "third@example.com",
                StartsAtUtc = ValidCommand().StartsAtUtc?.AddMinutes(70)
            },
            CancellationToken.None);
        Assert(exactBoundary.IsSuccess, "두 면접의 buffer 경계가 정확히 닿기만 하면 허용한다");

        var otherInterviewer = await context.Service.ScheduleAsync(
            ValidCommand() with
            {
                CandidateEmail = "fourth@example.com",
                InterviewerIds = ["OTHER-01"]
            },
            CancellationToken.None);
        Assert(otherInterviewer.IsSuccess, "같은 시각이어도 면접관이 다르면 허용한다");
        Assert(context.Repository.Count == 3, "성공한 일정 세 건만 저장한다");
    }

    /// <summary>
    /// 같은 슬롯의 병렬 요청이 몰려도 Repository 원자 구역에서 정확히 하나만 저장되는지 확인합니다.
    /// </summary>
    /// <returns>모든 병렬 요청 완료를 나타내는 Task를 반환합니다.</returns>
    private static async Task VerifyAtomicConflictAsync()
    {
        var context = CreateContext();
        // Task.Run은 여러 호출을 thread pool에 맡겨 충돌 검사와 저장의 경쟁 조건을 적극적으로 만듭니다.
        var tasks = Enumerable.Range(1, 24)
            .Select(
                number => Task.Run(
                    () => context.Service.ScheduleAsync(
                        ValidCommand() with { CandidateEmail = $"candidate{number}@example.com" },
                        CancellationToken.None)))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert(results.Count(result => result.IsSuccess) == 1, "병렬 요청 24개 중 정확히 하나만 성공한다");
        Assert(
            results.Count(result => result.Error?.Code == "interview.slot_conflict") == 23,
            "나머지 병렬 요청은 모두 명시적인 충돌 결과다");
        Assert(context.Repository.Count == 1, "원자적 Repository에는 한 일정만 남는다");
        Assert(results.Where(result => result.IsSuccess).Select(result => result.Value?.Id).Distinct().Count() == 1,
            "성공 결과는 하나의 일정 식별자만 만든다");
    }

    /// <summary>
    /// 이미 취소된 신호가 Application에서 Repository까지 정상적으로 중단되는지 확인합니다.
    /// </summary>
    /// <returns>취소 예외 관찰을 나타내는 Task를 반환합니다.</returns>
    private static async Task VerifyCancellationAsync()
    {
        var context = CreateContext();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var canceled = false;
        try
        {
            await context.Service.ScheduleAsync(ValidCommand(), source.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        Assert(canceled, "취소를 업무 오류 Result로 숨기지 않고 OperationCanceledException으로 전파한다");
        Assert(context.Repository.Count == 0, "취소된 요청은 저장되지 않는다");
    }

    /// <summary>
    /// 각 테스트가 독립된 시계와 Repository를 사용하도록 서비스 묶음을 새로 만듭니다.
    /// </summary>
    /// <returns>서비스, Repository, 수동 시계를 함께 가진 TestContext를 반환합니다.</returns>
    private static TestContext CreateContext()
    {
        var repository = new InMemoryInterviewRepository();
        var clock = new ManualTimeProvider(
            new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        IInterviewBufferPolicy[] policies =
        [
            new RemoteInterviewBufferPolicy(),
            new OnsiteInterviewBufferPolicy()
        ];
        var service = new ScheduleInterviewService(repository, policies, clock);
        return new TestContext(service, repository, clock);
    }

    /// <summary>
    /// 여러 테스트에서 재사용할 유효한 원격 면접 command를 만듭니다.
    /// </summary>
    /// <returns>고정 시각과 안전한 HTTPS URL을 가진 command를 반환합니다.</returns>
    private static ScheduleInterviewCommand ValidCommand()
    {
        return new ScheduleInterviewCommand(
            "홍길동",
            "candidate@example.com",
            new DateTimeOffset(2026, 10, 20, 1, 0, 0, TimeSpan.Zero),
            60,
            "remote",
            "https://meet.example.test/interviews/1001",
            null,
            ["DEV-01"]);
    }

    /// <summary>
    /// 조건이 거짓이면 즉시 테스트를 실패시키고, 참이면 PASS 수를 증가시킵니다.
    /// </summary>
    /// <param name="condition">반드시 참이어야 하는 검증 조건입니다.</param>
    /// <param name="message">실패 원인과 성공 항목을 설명하는 문장입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"검증 실패: {message}");
        }

        _passed++;
        Console.WriteLine($"  [PASS] {message}");
    }

    /// <summary>
    /// 자체 테스트에서 함께 움직이는 서비스와 Adapter, 시계를 한 값으로 묶습니다.
    /// </summary>
    /// <param name="Service">검증 대상 Application Service입니다.</param>
    /// <param name="Repository">저장 건수를 관찰할 메모리 Adapter입니다.</param>
    /// <param name="Clock">현재 시각을 고정하고 이동할 수 있는 시계입니다.</param>
    private sealed record TestContext(
        ScheduleInterviewService Service,
        InMemoryInterviewRepository Repository,
        ManualTimeProvider Clock);
}
