using System.Security.Claims;
using DocumentAccessApi.Application;
using DocumentAccessApi.Application.Ports;
using DocumentAccessApi.Domain;
using DocumentAccessApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentAccessApi.SelfTesting;

/// <summary>
/// 외부 테스트 package 없이 핵심 Domain, Application, Authorization 경계를 빠르게 검증합니다.
/// </summary>
public static class SelfTestRunner
{
    private const int TotalChecks = 13;

    /// <summary>
    /// 등록된 실제 서비스들을 꺼내 성공·실패 권한 시나리오를 순서대로 확인합니다.
    /// </summary>
    /// <param name="services">Program의 Composition Root가 완성한 DI container입니다.</param>
    /// <returns>모든 검사가 통과하면 0, 하나라도 실패하면 1을 반환합니다.</returns>
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var passed = 0;

        try
        {
            var application = services.GetRequiredService<DocumentApplicationService>();
            var authorization = services.GetRequiredService<IAuthorizationService>();

            var invalid = ProjectDocument.Create(
                Guid.NewGuid(),
                title: " ",
                body: "본문",
                ownerId: "user-alice",
                DateTimeOffset.Parse("2026-09-22T00:00:00Z"));
            Expect(!invalid.IsSuccess && invalid.Error?.Code == "TITLE_INVALID", "빈 제목을 거절한다.");
            passed++;

            var alice = CreatePrincipal("user-alice", "Alice", DocumentRoles.Editor);
            var bob = CreatePrincipal("user-bob", "Bob", DocumentRoles.Viewer);
            var admin = CreatePrincipal("user-admin", "Admin", DocumentRoles.Admin);

            var aliceCreate = await authorization.AuthorizeAsync(alice, resource: null, DocumentPolicies.Creator);
            Expect(aliceCreate.Succeeded, "Editor는 문서 생성 정책을 통과한다.");
            passed++;

            var bobCreate = await authorization.AuthorizeAsync(bob, resource: null, DocumentPolicies.Creator);
            Expect(!bobCreate.Succeeded, "Viewer는 문서 생성 정책을 통과하지 못한다.");
            passed++;

            var subjectlessEditor = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, DocumentRoles.Editor)],
                DemoHeaderAuthenticationDefaults.Scheme));
            var subjectlessCreate = await authorization.AuthorizeAsync(
                subjectlessEditor,
                resource: null,
                DocumentPolicies.Creator);
            Expect(!subjectlessCreate.Succeeded, "Editor 역할만 있고 안정 ID가 없으면 생성 정책을 통과하지 못한다.");
            passed++;

            var created = await application.CreateAsync(
                "user-alice",
                new CreateDocumentCommand("배포 점검표", "운영 배포 전에 승인자를 확인합니다."),
                CancellationToken.None);
            Expect(created.IsSuccess, "유효한 명령은 문서를 생성한다.");
            passed++;

            var loaded = await application.FindAsync(created.Value.Id, CancellationToken.None);
            Expect(loaded == created.Value, "Repository에서 같은 문서를 다시 읽는다.");
            passed++;

            var ownerRead = await authorization.AuthorizeAsync(alice, created.Value, DocumentReadRequirement.Instance);
            Expect(ownerRead.Succeeded, "문서 소유자는 자신의 문서를 읽는다.");
            passed++;

            var strangerRead = await authorization.AuthorizeAsync(bob, created.Value, DocumentReadRequirement.Instance);
            Expect(!strangerRead.Succeeded, "다른 일반 사용자는 문서를 읽지 못한다.");
            passed++;

            var adminRead = await authorization.AuthorizeAsync(admin, created.Value, DocumentReadRequirement.Instance);
            Expect(adminRead.Succeeded, "Admin은 다른 사용자의 문서를 읽는다.");
            passed++;

            var subjectlessAdmin = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, DocumentRoles.Admin)],
                DemoHeaderAuthenticationDefaults.Scheme));
            var subjectlessRead = await authorization.AuthorizeAsync(
                subjectlessAdmin,
                created.Value,
                DocumentReadRequirement.Instance);
            Expect(!subjectlessRead.Succeeded, "Admin 역할만 있고 안정 ID가 없으면 fail-closed로 거절한다.");
            passed++;

            var unauthenticatedOwner = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user-alice")]));
            var unauthenticatedOwnerRead = await authorization.AuthorizeAsync(
                unauthenticatedOwner,
                created.Value,
                DocumentReadRequirement.Instance);
            Expect(!unauthenticatedOwnerRead.Succeeded, "owner claim이 있어도 인증되지 않은 identity는 거절한다.");
            passed++;

            // using var는 현재 메서드가 끝날 때 CancellationTokenSource를 자동 Dispose하는 문법입니다.
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await ExpectCancellationAsync(
                () => application.FindAsync(created.Value.Id, cancellation.Token),
                cancellation.Token,
                "취소 토큰이 Repository까지 전달된다.");
            passed++;

            var cancellationProbe = new DocumentApplicationService(
                new FailIfCalledDocumentRepository(),
                TimeProvider.System);
            await ExpectCancellationAsync(
                () => cancellationProbe.CreateAsync(
                    "user-alice",
                    new CreateDocumentCommand("취소될 문서", "저장되면 안 됩니다."),
                    cancellation.Token),
                cancellation.Token,
                "미리 취소된 생성은 Domain 생성과 Repository 저장 전에 중단된다.");
            passed++;

            Console.WriteLine($"자체 검증 통과: {passed}/{TotalChecks}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"자체 검증 실패 ({passed}/{TotalChecks} 통과): {exception.Message}");
            return 1;
        }
    }

    /// <summary>
    /// 테스트용 ClaimsPrincipal을 만들어 framework의 실제 정책 평가에 전달합니다.
    /// </summary>
    /// <param name="subjectId">NameIdentifier claim에 넣을 안정적인 사용자 ID입니다.</param>
    /// <param name="name">Name claim에 넣을 표시 이름입니다.</param>
    /// <param name="roles">Role claim으로 추가할 역할 목록입니다.</param>
    /// <returns>인증된 identity 하나를 가진 ClaimsPrincipal을 반환합니다.</returns>
    // params는 호출자가 역할을 0개 이상 쉼표로 나열하면 compiler가 string 배열로 모아 주는 문법입니다.
    private static ClaimsPrincipal CreatePrincipal(
        string subjectId,
        string name,
        params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subjectId),
            new(ClaimTypes.Name, name),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, DemoHeaderAuthenticationDefaults.Scheme));
    }

    /// <summary>
    /// 비동기 작업이 OperationCanceledException으로 끝나는지 확인합니다.
    /// </summary>
    /// <param name="action">취소되어야 하는 비동기 작업을 만드는 함수입니다.</param>
    /// <param name="expectedToken">예외가 그대로 품어야 하는 원래 취소 토큰입니다.</param>
    /// <param name="message">실패 시 어떤 계약이 깨졌는지 설명할 메시지입니다.</param>
    /// <returns>예상한 취소가 확인되면 완료되는 Task를 반환합니다.</returns>
    private static async Task ExpectCancellationAsync(
        Func<Task> action,
        CancellationToken expectedToken,
        string message)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException exception)
        {
            Expect(exception.CancellationToken == expectedToken, $"{message} 예외가 원래 토큰을 보존하지 않았습니다.");
            return;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 조건이 거짓이면 현재 자체 검증을 즉시 실패시킵니다.
    /// </summary>
    /// <param name="condition">반드시 참이어야 하는 검사 결과입니다.</param>
    /// <param name="message">거짓일 때 출력할 초보자 친화적 설명입니다.</param>
    /// <returns>반환값은 없으며 조건이 참이면 다음 검사로 진행합니다.</returns>
    private static void Expect(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 미리 취소된 생성이 Repository에 닿지 않는지 확인하기 위해, 호출되면 즉시 실패하는 테스트 대역입니다.
    /// </summary>
    private sealed class FailIfCalledDocumentRepository : IDocumentRepository
    {
        /// <summary>
        /// 취소 검사보다 저장이 먼저 실행되는 잘못된 흐름을 즉시 드러냅니다.
        /// </summary>
        /// <param name="document">호출되면 안 되는 저장 대상입니다.</param>
        /// <param name="cancellationToken">호출되면 안 되는 저장 취소 토큰입니다.</param>
        /// <returns>정상 흐름에서는 반환되지 않으며, 호출 즉시 예외를 던집니다.</returns>
        public Task AddAsync(ProjectDocument document, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("미리 취소된 생성이 Repository.AddAsync를 호출했습니다.");
        }

        /// <summary>
        /// 이 테스트에서는 조회를 사용하지 않으므로 잘못 호출되면 즉시 실패합니다.
        /// </summary>
        /// <param name="id">호출되면 안 되는 문서 식별자입니다.</param>
        /// <param name="cancellationToken">호출되면 안 되는 조회 취소 토큰입니다.</param>
        /// <returns>정상 흐름에서는 반환되지 않으며, 호출 즉시 예외를 던집니다.</returns>
        public Task<ProjectDocument?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("이 테스트는 Repository.FindAsync를 호출하지 않아야 합니다.");
        }
    }
}
