using InvitationTokenApi.Application;
using InvitationTokenApi.Application.Ports;
using InvitationTokenApi.Domain;
using InvitationTokenApi.Infrastructure;
using InvitationTokenApi.Security;
using Microsoft.AspNetCore.DataProtection;

namespace InvitationTokenApi.SelfTesting;

/// <summary>
/// 외부 테스트 package 없이 Domain, 실제 codec, Application, 동시성 계약을 실행합니다.
/// </summary>
public static class SelfTestRunner
{
    private static int _passedAssertions;

    /// <summary>
    /// 결정적인 테스트 시계와 실제 Data Protection provider로 모든 회귀 검증을 실행합니다.
    /// </summary>
    /// <returns>모든 assertion이 통과하면 0, 하나라도 실패하면 1인 종료 코드가 든 Task를 반환합니다.</returns>
    public static async Task<int> RunAsync()
    {
        _passedAssertions = 0;

        try
        {
            TestDomainAndExpiryPolicy();
            TestRealTokenCodec();
            await TestApplicationServiceAsync();
            await TestAtomicConcurrentRedemptionAsync();
            Console.WriteLine($"SELF-TEST PASSED: {_passedAssertions} assertions");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SELF-TEST FAILED: {exception}");
            return 1;
        }
    }

    /// <summary>
    /// Domain 정규화, 불변 상태, 역할별 수명과 정확한 만료 경계를 검증합니다.
    /// </summary>
    /// <returns>검증만 수행하므로 반환값은 없으며 어긋나면 예외를 던집니다.</returns>
    private static void TestDomainAndExpiryPolicy()
    {
        var policy = CreateExpiryPolicy();
        var issuedAtUtc = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);

        Assert(
            policy.CalculateExpiration(ProjectRole.Member, issuedAtUtc) ==
            issuedAtUtc.AddHours(24),
            "Member 토큰 수명은 24시간이다.");
        Assert(
            policy.CalculateExpiration(ProjectRole.Admin, issuedAtUtc) ==
            issuedAtUtc.AddMinutes(15),
            "Admin 토큰 수명은 15분이다.");
        Assert(
            !policy.IsExpired(issuedAtUtc.AddMinutes(15), issuedAtUtc.AddMinutes(14)),
            "만료 시각 전에는 토큰이 유효하다.");
        Assert(
            policy.IsExpired(issuedAtUtc.AddMinutes(15), issuedAtUtc.AddMinutes(15)),
            "만료 시각과 정확히 같으면 만료다.");

        var role = ProjectInvitation.ParseRole(" admin ");
        Assert(role.IsSuccess && role.Value == ProjectRole.Admin,
            "role 입력은 공백과 대소문자를 정규화한다.");
        AssertError(
            ProjectInvitation.ParseRole("Owner"),
            "invitation.role.invalid",
            "허용 목록 밖 권한은 거절한다.");

        AssertError(
            ProjectInvitation.Create(
                "INVITE-ROLE",
                "PROJECT-1",
                "student@example.com",
                (ProjectRole)999,
                issuedAtUtc,
                issuedAtUtc.AddHours(1)),
            "invitation.role.invalid",
            "숫자 cast로 만든 정의되지 않은 enum 권한도 Domain 생성에서 거절한다.");

        var invitation = ProjectInvitation.Create(
            " invite-1 ",
            " project-1 ",
            " Student@Example.COM ",
            ProjectRole.Member,
            issuedAtUtc,
            issuedAtUtc.AddHours(24));
        Assert(invitation.IsSuccess, "유효한 초대를 생성한다.");
        Assert(invitation.Value!.InvitationId == "INVITE-1", "초대 ID를 대문자로 정규화한다.");
        Assert(invitation.Value.ProjectId == "PROJECT-1", "프로젝트 ID를 대문자로 정규화한다.");
        Assert(invitation.Value.Email == "student@example.com", "이메일을 소문자로 정규화한다.");
        Assert(invitation.Value.Status == InvitationStatus.Issued, "새 초대 상태는 Issued다.");
        AssertError(
            invitation.Value.Redeem(issuedAtUtc.AddTicks(-1)),
            "invitation.token.invalid_or_expired",
            "발급 시각보다 앞선 사용 시각은 Domain 불변식으로 거절한다.");

        AssertError(
            ProjectInvitation.Create(
                "x",
                "PROJECT-1",
                "student@example.com",
                ProjectRole.Member,
                issuedAtUtc,
                issuedAtUtc.AddHours(1)),
            "invitation.id.invalid",
            "짧은 초대 ID를 거절한다.");
        AssertError(
            ProjectInvitation.Create(
                "INVITE-2",
                "bad/project",
                "student@example.com",
                ProjectRole.Member,
                issuedAtUtc,
                issuedAtUtc.AddHours(1)),
            "invitation.project_id.invalid",
            "허용하지 않은 문자가 있는 프로젝트 ID를 거절한다.");
        AssertError(
            ProjectInvitation.Create(
                "INVITE-3",
                "PROJECT-1",
                "not-an-email",
                ProjectRole.Member,
                issuedAtUtc,
                issuedAtUtc.AddHours(1)),
            "invitation.email.invalid",
            "@가 없는 이메일을 거절한다.");
    }

    /// <summary>
    /// 실제 IDataProtector로 roundtrip, 변조, purpose 격리, 만료 실패를 검증합니다.
    /// </summary>
    /// <returns>검증만 수행하므로 반환값은 없으며 어긋나면 예외를 던집니다.</returns>
    private static void TestRealTokenCodec()
    {
        var policy = CreateExpiryPolicy();
        // Ephemeral provider도 실제 암호화·인증 codec을 쓰지만 테스트 종료 뒤 key를 남기지 않습니다.
        var provider = new EphemeralDataProtectionProvider();
        var invitationProtector = new DataProtectionInvitationTokenProtector(
            provider,
            Program.InvitationTokenPurpose,
            policy);
        var differentPurposeProtector = new DataProtectionInvitationTokenProtector(
            provider,
            "InvitationTokenApi.PasswordReset.v1",
            policy);
        var nowUtc = new DateTimeOffset(2026, 10, 14, 1, 0, 0, TimeSpan.Zero);
        var payload = new InvitationTokenPayload(
            1,
            "INV-CODEC",
            "PROJECT-CODEC",
            ProjectRole.Admin,
            nowUtc.AddMinutes(15));

        var token = invitationProtector.Protect(payload);
        Assert(
            !token.Contains('{') && !token.Contains('"'),
            "보호 토큰 문자열에는 JSON 객체·문자열 구분자가 직접 나타나지 않는다.");
        var roundTrip = invitationProtector.Unprotect(token, nowUtc);
        Assert(roundTrip.IsSuccess && roundTrip.Value == payload,
            "같은 purpose의 실제 codec은 payload를 왕복 복원한다.");

        var tampered = invitationProtector.Unprotect(Tamper(token), nowUtc);
        AssertError(
            tampered,
            "invitation.token.invalid_or_expired",
            "토큰 한 글자 변조를 탐지한다.");

        var wrongPurpose = differentPurposeProtector.Unprotect(token, nowUtc);
        AssertError(
            wrongPurpose,
            "invitation.token.invalid_or_expired",
            "같은 key provider라도 다른 purpose는 토큰을 열지 못한다.");

        var expired = invitationProtector.Unprotect(token, payload.ExpiresAtUtc);
        AssertError(
            expired,
            "invitation.token.invalid_or_expired",
            "실제 codec 경계에서 만료 시각의 토큰을 거절한다.");

        var oversized = invitationProtector.Unprotect(new string('A', 4097), nowUtc);
        AssertError(
            oversized,
            "invitation.token.invalid_or_expired",
            "4KB를 넘는 비정상 토큰을 복호화 전에 같은 안전한 오류로 거절한다.");
    }

    /// <summary>
    /// 생성·조회·중복·정상 사용·재사용·만료를 Application Service 전체 흐름으로 검증합니다.
    /// </summary>
    /// <returns>모든 비동기 시나리오가 끝났을 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestApplicationServiceAsync()
    {
        var harness = CreateHarness();
        var member = await harness.Service.CreateAsync(
            CreateCommand(" invite-service ", " member "),
            CancellationToken.None);
        Assert(member.IsSuccess, "Application Service가 Member 초대와 토큰을 만든다.");
        Assert(
            member.Value!.Invitation.ExpiresAtUtc == harness.Clock.GetUtcNow().AddHours(24),
            "Member 생성 흐름은 Strategy의 24시간 만료를 기록한다.");

        var found = await harness.Service.GetAsync("invite-service", CancellationToken.None);
        Assert(found.IsSuccess && found.Value!.InvitationId == "INVITE-SERVICE",
            "조회는 route ID를 저장소 key와 같은 형태로 정규화한다.");

        var duplicate = await harness.Service.CreateAsync(
            CreateCommand("INVITE-SERVICE", "Member"),
            CancellationToken.None);
        AssertError(duplicate, "invitation.duplicate", "같은 초대 ID 생성을 409 후보 오류로 거절한다.");

        var missing = await harness.Service.GetAsync("missing-id", CancellationToken.None);
        AssertError(missing, "invitation.not_found", "없는 초대 조회는 안정적인 not-found 오류다.");

        var invalid = await harness.Service.RedeemAsync(
            new RedeemInvitationCommand("not-a-protected-token"),
            CancellationToken.None);
        AssertError(invalid, "invitation.token.invalid_or_expired", "임의 문자열 토큰을 공통 400 후보 오류로 거절한다.");

        var redeemed = await harness.Service.RedeemAsync(
            new RedeemInvitationCommand(member.Value.Token),
            CancellationToken.None);
        Assert(redeemed.IsSuccess, "유효한 토큰은 처음 한 번 사용된다.");
        Assert(
            redeemed.Value!.RedeemedAtUtc == harness.Clock.GetUtcNow(),
            "사용 완료 시각은 주입한 TimeProvider의 현재 시각이다.");

        var replay = await harness.Service.RedeemAsync(
            new RedeemInvitationCommand(member.Value.Token),
            CancellationToken.None);
        AssertError(replay, "invitation.already_redeemed", "같은 토큰 재사용은 replay 충돌이다.");

        var admin = await harness.Service.CreateAsync(
            CreateCommand("INVITE-ADMIN", "Admin"),
            CancellationToken.None);
        Assert(admin.IsSuccess, "Admin 초대를 만든다.");
        Assert(
            admin.Value!.Invitation.ExpiresAtUtc == harness.Clock.GetUtcNow().AddMinutes(15),
            "Admin 생성 흐름은 Strategy의 짧은 15분 만료를 기록한다.");

        harness.Clock.Advance(TimeSpan.FromMinutes(15));
        var expired = await harness.Service.RedeemAsync(
            new RedeemInvitationCommand(admin.Value.Token),
            CancellationToken.None);
        AssertError(
            expired,
            "invitation.token.invalid_or_expired",
            "조정 가능한 시계가 만료 경계에 도달하면 토큰을 거절한다.");
    }

    /// <summary>
    /// 같은 토큰을 동시에 여러 번 보내도 Repository 임계 구역에서 하나만 성공하는지 검증합니다.
    /// </summary>
    /// <returns>모든 경쟁 요청이 끝났을 때 완료되는 Task를 반환합니다.</returns>
    private static async Task TestAtomicConcurrentRedemptionAsync()
    {
        var harness = CreateHarness();
        var created = await harness.Service.CreateAsync(
            CreateCommand("INVITE-RACE", "Member"),
            CancellationToken.None);
        Assert(created.IsSuccess, "동시 사용 검증용 초대를 만든다.");

        const int concurrentAttemptCount = 32;
        // using var는 검증이 끝나면 Barrier의 대기 handle을 반드시 정리합니다.
        using var startBarrier = new Barrier(concurrentAttemptCount + 1);
        // LongRunning은 32개 작업에 전용 thread를 요청하고, Barrier는 모두 service 호출 직전까지 도착한 뒤 한꺼번에 출발시킵니다.
        // Range가 0~31 입력을 만들고 Select가 각 입력을 작업으로 바꾸며 ToArray가 즉시 작업을 시작합니다. LINQ로 같은 생성 규칙을 한눈에 보이게 합니다.
        var attempts = Enumerable.Range(0, concurrentAttemptCount)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    startBarrier.SignalAndWait();
                    return harness.Service.RedeemAsync(
                            new RedeemInvitationCommand(created.Value!.Token),
                            CancellationToken.None)
                        .GetAwaiter()
                        .GetResult();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();
        startBarrier.SignalAndWait();
        var results = await Task.WhenAll(attempts);

        // Count의 lambda는 결과마다 조건을 검사해 성공과 replay 개수를 집계하므로 수동 counter를 잘못 갱신할 여지를 줄입니다.
        Assert(results.Count(result => result.IsSuccess) == 1,
            "32개 동시 redeem 중 정확히 하나만 성공한다.");
        Assert(
            results.Count(result =>
                !result.IsSuccess &&
                result.Error!.Code == "invitation.already_redeemed") == 31,
            "나머지 31개 동시 redeem은 모두 replay 충돌이다.");

        var stored = await harness.Service.GetAsync("INVITE-RACE", CancellationToken.None);
        Assert(
            stored.IsSuccess && stored.Value!.Status == InvitationStatus.Redeemed,
            "경쟁 뒤 저장된 snapshot은 Redeemed 상태 하나다.");
    }

    /// <summary>
    /// Member 24시간, Admin 15분 규칙을 가진 만료 Strategy를 만듭니다.
    /// </summary>
    /// <returns>운영 Composition Root와 같은 수명의 Strategy를 반환합니다.</returns>
    private static RoleBasedInvitationExpiryPolicy CreateExpiryPolicy()
    {
        return new RoleBasedInvitationExpiryPolicy(
            memberLifetime: TimeSpan.FromHours(24),
            adminLifetime: TimeSpan.FromMinutes(15));
    }

    /// <summary>
    /// 독립 저장소, 실제 codec, 조정 가능한 시계를 가진 Application 테스트 묶음을 만듭니다.
    /// </summary>
    /// <returns>테스트끼리 상태와 key가 섞이지 않는 새 Harness를 반환합니다.</returns>
    private static TestHarness CreateHarness()
    {
        var clock = new AdjustableTimeProvider(
            new DateTimeOffset(2026, 10, 14, 2, 0, 0, TimeSpan.Zero));
        IInvitationExpiryPolicy policy = CreateExpiryPolicy();
        var provider = new EphemeralDataProtectionProvider();
        var protector = new DataProtectionInvitationTokenProtector(
            provider,
            Program.InvitationTokenPurpose,
            policy);
        var service = new InvitationApplicationService(
            new InMemoryInvitationRepository(),
            protector,
            policy,
            clock);
        return new TestHarness(service, clock);
    }

    /// <summary>
    /// 반복 시나리오에서 같은 유효 입력을 쉽게 만들되 ID와 권한만 바꾸게 합니다.
    /// </summary>
    /// <param name="invitationId">시나리오끼리 구분할 초대 ID입니다.</param>
    /// <param name="role">Member 또는 Admin 권한 문자열입니다.</param>
    /// <returns>Application Service에 전달할 유효한 생성 명령을 반환합니다.</returns>
    private static CreateInvitationCommand CreateCommand(
        string invitationId,
        string role)
    {
        return new CreateInvitationCommand(
            invitationId,
            "project-alpha",
            "learner@example.com",
            role);
    }

    /// <summary>
    /// 보호 토큰 가운데 문자 하나를 바꿔 전송 중 변조를 재현합니다.
    /// </summary>
    /// <param name="token">실제 Data Protection이 만든 정상 토큰입니다.</param>
    /// <returns>길이는 같지만 인증 tag 검증에 실패해야 하는 문자열을 반환합니다.</returns>
    private static string Tamper(string token)
    {
        var characters = token.ToCharArray();
        var index = characters.Length / 2;
        characters[index] = characters[index] == 'A' ? 'B' : 'A';
        return new string(characters);
    }

    /// <summary>
    /// Result가 예상 오류 코드로 실패했는지 확인합니다.
    /// </summary>
    /// <typeparam name="T">검사할 Result의 성공 값 형식입니다.</typeparam>
    /// <param name="result">성공하면 안 되는 실제 결과입니다.</param>
    /// <param name="expectedCode">반드시 일치해야 하는 안정 오류 코드입니다.</param>
    /// <param name="message">성공 출력과 실패 예외에 사용할 학습용 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없습니다.</returns>
    private static void AssertError<T>(
        Result<T> result,
        string expectedCode,
        string message)
    {
        Assert(
            !result.IsSuccess && result.Error!.Code == expectedCode,
            message);
    }

    /// <summary>
    /// 조건이 참인지 검사하고 통과한 assertion 수를 누적합니다.
    /// </summary>
    /// <param name="condition">반드시 true여야 하는 계약 조건입니다.</param>
    /// <param name="message">성공 때도 보여 줄 이해 가능한 계약 설명입니다.</param>
    /// <returns>검증만 수행하므로 반환값은 없으며 false면 예외를 던집니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"검증 실패: {message}");
        }

        _passedAssertions++;
        Console.WriteLine($"  [PASS] {message}");
    }

    /// <summary>
    /// Application Service와 조정 가능한 시계를 함께 전달하는 테스트 전용 불변 묶음입니다.
    /// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
    /// </summary>
    /// <param name="Service">검증할 실제 Application Service입니다.</param>
    /// <param name="Clock">만료 시나리오에서 앞으로 이동할 테스트 시계입니다.</param>
    private sealed record TestHarness(
        InvitationApplicationService Service,
        AdjustableTimeProvider Clock);
}
