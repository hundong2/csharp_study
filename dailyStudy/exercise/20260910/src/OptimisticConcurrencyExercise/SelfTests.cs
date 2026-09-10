// file-scoped namespace는 이 파일의 모든 형식을 같은 이름 공간에 넣으면서 중첩 중괄호를 줄입니다.
namespace OptimisticConcurrencyExercise;

// 외부 테스트 패키지 없이 핵심 불변식과 실제 경쟁을 실행하는 작은 자체 테스트 러너입니다.
internal static class SelfTests
{
    private const string PublishableBody =
        "최신 문서를 다시 읽고 변경 차이를 검토한 뒤 승인 기록, 관측 지표, 되돌리기 절차를 모두 확인합니다.";

    /// <summary>
    /// 모든 자체 테스트를 순서대로 실행하고 각 결과를 콘솔에 표시합니다.
    /// 매개변수는 없습니다.
    /// 반환값은 전부 통과하면 0, 하나라도 실패하면 1인 프로세스 종료 코드입니다.
    /// </summary>
    public static async Task<int> RunAsync()
    {
        // Func<Task>는 나중에 실행할 비동기 테스트 메서드를 값으로 보관하는 delegate 형식입니다.
        // tuple 배열은 테스트 이름과 실행 함수를 가볍게 한 쌍으로 묶습니다.
        (string Name, Func<Task> Body)[] tests =
        [
            ("잘못된 입력은 Repository 전에 거부한다", InvalidInputStopsBeforeRepositoryAsync),
            ("편집은 원본을 바꾸지 않고 버전을 1 올린다", EditCreatesNewSnapshotAsync),
            ("발행 Strategy는 Published 상태를 만든다", PublishStrategyChangesStatusAsync),
            ("짧은 발행 본문은 저장하지 않는다", ShortPublishDoesNotSaveAsync),
            ("발행 뒤 일반 편집도 발행 불변식을 우회하지 못한다", PublishedEditCannotBypassBodyRuleAsync),
            ("이미 오래된 버전은 저장 전에 충돌한다", StaleVersionStopsBeforeSaveAsync),
            ("Repository compare-and-swap은 승자를 보존한다", RepositoryCompareAndSwapPreservesWinnerAsync),
            ("동시 편집 두 건 중 하나만 같은 버전을 저장한다", ConcurrentEditorsYieldOneConflictAsync),
            ("충돌 뒤 재조회하고 명시적으로 재적용할 수 있다", ReloadThenReapplySucceedsAsync),
            ("없는 문서는 NotFound Result를 돌려준다", MissingArticleReturnsFailureAsync),
            ("이미 취소된 요청은 상태를 바꾸지 않는다", CancellationLeavesStateUnchangedAsync),
            ("CAS 전 검사에서 관측된 취소는 상태를 바꾸지 않는다", CancellationObservedBeforeCompareAndSwapLeavesStateUnchangedAsync),
            ("커밋 뒤 도착한 취소는 성공을 실패로 뒤집지 않는다", CancellationAfterCommitKeepsSuccessAsync),
            ("Strategy 누락·중복·null은 시작 시 실패한다", BadStrategyConfigurationFailsFastAsync),
        ];

        int passed = 0;

        foreach ((string name, Func<Task> body) in tests)
        {
            try
            {
                await body();
                passed++;
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[FAIL] {name}: {exception.GetType().Name} - {exception.Message}");
            }
        }

        Console.WriteLine($"self-test {passed}/{tests.Length} 통과");
        return passed == tests.Length ? 0 : 1;
    }

    /// <summary>
    /// null·공백·정의되지 않은 enum·잘못된 버전이 실패하고 저장소를 호출하지 않는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task InvalidInputStopsBeforeRepositoryAsync()
    {
        KnowledgeArticle seed = CreateSeed();

        // var는 오른쪽 생성식에서 정확한 형식을 컴파일러가 추론하게 합니다. 형식이 명확하고 반복이 긴 지역 변수에만 사용했습니다.
        var repository = new CountingRepository(new InMemoryArticleRepository([seed]));
        ArticleRevisionService service = CreateService(repository);

        // collection expression 안의 tuple은 여러 잘못된 요청을 반복 검증하는 간결한 테스트 데이터입니다.
        (string? Id, string? Title, string? Body, RevisionAction Action, int Version, string Code)[] invalidInputs =
        [
            (null, "제목", "본문", RevisionAction.Edit, 1, "article.id_required"),
            (seed.Id, "   ", "본문", RevisionAction.Edit, 1, "article.title_required"),
            (seed.Id, "제목", null, RevisionAction.Edit, 1, "article.body_required"),
            (seed.Id, "제목", "본문", (RevisionAction)999, 1, "article.action_invalid"),
            (seed.Id, "제목", "본문", RevisionAction.Edit, 0, "article.version_invalid"),
        ];

        foreach ((string? id, string? title, string? body, RevisionAction action, int version, string code) in invalidInputs)
        {
            Result<RevisionReceipt> outcome = await service.ReviseAsync(
                id,
                title,
                body,
                action,
                version,
                CancellationToken.None);

            Assert(!outcome.IsSuccess, "잘못된 요청이 Application Service를 통과했습니다.");
            Assert(outcome.Problem.Code == code, $"검증 오류 코드가 달라졌습니다: {code}");
        }

        Assert(repository.GetCalls == 0, "검증 실패는 Repository 조회를 호출하면 안 됩니다.");
        Assert(repository.SaveCalls == 0, "검증 실패는 Repository 저장을 호출하면 안 됩니다.");
    }

    /// <summary>
    /// Edit Strategy가 새 스냅샷을 저장하고 처음 읽은 record는 그대로 남기는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task EditCreatesNewSnapshotAsync()
    {
        KnowledgeArticle original = CreateSeed();
        var repository = new InMemoryArticleRepository([original]);
        ArticleRevisionService service = CreateService(repository);

        Result<RevisionReceipt> outcome = await service.ReviseAsync(
            original.Id,
            "새 배포 절차",
            "수정된 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1,
            CancellationToken.None);

        Assert(outcome.IsSuccess, "유효한 편집은 성공해야 합니다.");
        Assert(outcome.Value.PreviousVersion == 1, "영수증의 이전 버전이 잘못됐습니다.");
        Assert(outcome.Value.CurrentVersion == 2, "영수증의 현재 버전이 잘못됐습니다.");
        Assert(outcome.Value.Status == ArticleStatus.Draft, "Edit는 초안 상태를 유지해야 합니다.");

        KnowledgeArticle current = await GetRequiredAsync(repository, original.Id);
        Assert(current.Title == "새 배포 절차", "새 제목이 저장되지 않았습니다.");
        Assert(current.Version == 2, "저장된 문서 버전은 2여야 합니다.");
        Assert(original.Title == "배포 절차", "record 원본 스냅샷이 변경됐습니다.");
        Assert(original.Version == 1, "원본 버전은 1로 남아야 합니다.");
    }

    /// <summary>
    /// Publish Strategy가 충분한 본문을 받아 버전 증가와 상태 전환을 함께 만드는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task PublishStrategyChangesStatusAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var repository = new InMemoryArticleRepository([seed]);
        ArticleRevisionService service = CreateService(repository);

        Result<RevisionReceipt> outcome = await service.ReviseAsync(
            seed.Id,
            "운영 배포 체크리스트",
            PublishableBody,
            RevisionAction.Publish,
            expectedVersion: 1,
            CancellationToken.None);

        Assert(outcome.IsSuccess, "충분한 본문의 발행은 성공해야 합니다.");
        Assert(outcome.Value.Status == ArticleStatus.Published, "발행 결과 상태가 Published가 아닙니다.");

        KnowledgeArticle current = await GetRequiredAsync(repository, seed.Id);
        Assert(current.Status == ArticleStatus.Published, "Repository 문서가 Published로 바뀌지 않았습니다.");
        Assert(current.Version == 2, "발행도 버전을 정확히 1 올려야 합니다.");
    }

    /// <summary>
    /// Publish Strategy의 예상 가능한 업무 실패가 Result로 돌아오고 저장은 생기지 않는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task ShortPublishDoesNotSaveAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var repository = new CountingRepository(new InMemoryArticleRepository([seed]));
        ArticleRevisionService service = CreateService(repository);

        Result<RevisionReceipt> outcome = await service.ReviseAsync(
            seed.Id,
            "짧은 글",
            "아직 설명이 부족합니다.",
            RevisionAction.Publish,
            expectedVersion: 1,
            CancellationToken.None);

        Assert(!outcome.IsSuccess, "짧은 본문 발행은 실패해야 합니다.");
        Assert(
            outcome.Problem.Code == "article.body_too_short_to_publish",
            "발행 정책 오류 코드가 달라졌습니다.");
        Assert(repository.GetCalls == 1, "정책은 현재 상태를 읽은 뒤 적용되어야 합니다.");
        Assert(repository.SaveCalls == 0, "정책 실패는 저장을 시도하면 안 됩니다.");

        KnowledgeArticle current = await GetRequiredAsync(repository, seed.Id);
        Assert(current.Version == 1, "정책 실패 뒤 버전이 바뀌었습니다.");
    }

    /// <summary>
    /// Published 문서를 Edit Strategy로 수정해도 aggregate의 최소 본문 길이를 우회하지 못하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task PublishedEditCannotBypassBodyRuleAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var repository = new CountingRepository(new InMemoryArticleRepository([seed]));
        ArticleRevisionService service = CreateService(repository);

        Result<RevisionReceipt> published = await service.ReviseAsync(
            seed.Id,
            "발행된 문서",
            PublishableBody,
            RevisionAction.Publish,
            expectedVersion: 1,
            CancellationToken.None);
        Result<RevisionReceipt> shortened = await service.ReviseAsync(
            seed.Id,
            "짧아진 문서",
            "너무 짧은 본문",
            RevisionAction.Edit,
            expectedVersion: 2,
            CancellationToken.None);

        Assert(published.IsSuccess, "회귀 테스트 준비를 위한 발행이 실패했습니다.");
        Assert(!shortened.IsSuccess, "Published 문서의 짧은 Edit는 실패해야 합니다.");
        Assert(
            shortened.Problem.Code == "article.body_too_short_to_publish",
            "Edit 경로에도 같은 발행 불변식 오류가 필요합니다.");
        Assert(repository.SaveCalls == 1, "불변식 실패는 두 번째 저장을 호출하면 안 됩니다.");

        KnowledgeArticle current = await GetRequiredAsync(repository, seed.Id);
        Assert(current.Version == 2, "거부된 Edit가 Published 문서 버전을 올렸습니다.");
        Assert(current.Body == PublishableBody, "거부된 Edit가 Published 본문을 덮어썼습니다.");
    }

    /// <summary>
    /// 이미 최신 버전이 올라간 뒤 같은 expectedVersion 요청이 preflight에서 충돌하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task StaleVersionStopsBeforeSaveAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var repository = new CountingRepository(new InMemoryArticleRepository([seed]));
        ArticleRevisionService service = CreateService(repository);

        Result<RevisionReceipt> first = await service.ReviseAsync(
            seed.Id,
            "첫 번째 편집",
            "첫 번째 작성자가 저장한 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1,
            CancellationToken.None);

        Result<RevisionReceipt> stale = await service.ReviseAsync(
            seed.Id,
            "오래된 편집",
            "두 번째 작성자가 오래된 화면에서 만든 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1,
            CancellationToken.None);

        Assert(first.IsSuccess, "첫 편집 준비가 실패했습니다.");
        Assert(!stale.IsSuccess, "오래된 버전 요청은 충돌해야 합니다.");
        Assert(stale.Problem.Code == "article.version_conflict", "동시성 오류 코드가 달라졌습니다.");
        Assert(stale.Problem.CurrentVersion == 2, "호출자에게 최신 버전 2를 알려야 합니다.");
        Assert(repository.SaveCalls == 1, "명백히 오래된 요청은 조건부 저장까지 호출하지 않아야 합니다.");
    }

    /// <summary>
    /// 같은 버전에서 만든 두 후보를 직접 저장해 두 번째 CAS가 첫 번째 값을 덮어쓰지 않는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task RepositoryCompareAndSwapPreservesWinnerAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var repository = new InMemoryArticleRepository([seed]);
        var policy = new EditContentPolicy();

        ArticleRevision firstRevision = CreateRevision(
            seed.Id,
            "승자 제목",
            "먼저 저장할 문서 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1);
        ArticleRevision secondRevision = CreateRevision(
            seed.Id,
            "패자 제목",
            "나중에 같은 버전으로 저장할 문서 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1);

        KnowledgeArticle firstCandidate = policy.Apply(seed, firstRevision).Value;
        KnowledgeArticle secondCandidate = policy.Apply(seed, secondRevision).Value;

        ArticleSaveAttempt first = await repository.TrySaveAsync(firstCandidate, 1, CancellationToken.None);
        ArticleSaveAttempt second = await repository.TrySaveAsync(secondCandidate, 1, CancellationToken.None);

        Assert(first.Status == ArticleSaveStatus.Saved, "첫 CAS는 성공해야 합니다.");
        Assert(second.Status == ArticleSaveStatus.Conflict, "같은 예상 버전의 두 번째 CAS는 충돌해야 합니다.");
        Assert(second.CurrentArticle.Version == 2, "충돌 결과는 최신 버전 2를 담아야 합니다.");

        KnowledgeArticle current = await GetRequiredAsync(repository, seed.Id);
        Assert(current.Title == "승자 제목", "충돌한 후보가 승자의 내용을 덮어썼습니다.");
        Assert(current.Version == 2, "충돌은 버전을 추가로 올리면 안 됩니다.");
    }

    /// <summary>
    /// 두 서비스 호출이 모두 버전 1을 읽게 만든 뒤 실제 저장 경쟁에서 정확히 하나만 성공하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task ConcurrentEditorsYieldOneConflictAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var inner = new InMemoryArticleRepository([seed]);
        var coordinated = new CoordinatedReadRepository(inner, readersToRelease: 2);
        ArticleRevisionService service = CreateService(coordinated);

        Task<Result<RevisionReceipt>> alice = service.ReviseAsync(
            seed.Id,
            "Alice 제목",
            "Alice가 같은 버전에서 작성한 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1,
            CancellationToken.None);
        Task<Result<RevisionReceipt>> bob = service.ReviseAsync(
            seed.Id,
            "Bob 제목",
            "Bob이 같은 버전에서 작성한 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1,
            CancellationToken.None);

        // WhenAll은 두 작업을 모두 관찰합니다. WaitAsync의 5초는 버그가 있을 때 테스트가 영원히 멈추지 않는 안전장치입니다.
        Result<RevisionReceipt>[] outcomes = await Task
            .WhenAll(alice, bob)
            .WaitAsync(TimeSpan.FromSeconds(5));

        int successes = outcomes.Count(outcome => outcome.IsSuccess);
        int conflicts = outcomes.Count(
            outcome => !outcome.IsSuccess && outcome.Problem.Code == "article.version_conflict");

        Assert(successes == 1, "동시 편집 중 정확히 한 건만 성공해야 합니다.");
        Assert(conflicts == 1, "나머지 한 건은 동시성 충돌이어야 합니다.");

        KnowledgeArticle current = await GetRequiredAsync(inner, seed.Id);
        Assert(current.Version == 2, "동시 경쟁 뒤 버전은 한 번만 증가해야 합니다.");
        // `is A or B`는 값이 두 constant pattern 중 하나와 맞는지 읽기 쉽게 표현하는 논리 패턴입니다.
        Assert(
            current.Title is "Alice 제목" or "Bob 제목",
            "최종 제목은 실제 승자 중 하나여야 합니다.");
    }

    /// <summary>
    /// 충돌한 변경을 자동 덮어쓰지 않고 최신 문서를 다시 읽어 사용자가 재적용하면 성공하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task ReloadThenReapplySucceedsAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var repository = new InMemoryArticleRepository([seed]);
        ArticleRevisionService service = CreateService(repository);

        Result<RevisionReceipt> first = await service.ReviseAsync(
            seed.Id,
            "먼저 저장된 제목",
            "먼저 저장된 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1,
            CancellationToken.None);
        Result<RevisionReceipt> stale = await service.ReviseAsync(
            seed.Id,
            "재적용할 제목",
            PublishableBody,
            RevisionAction.Publish,
            expectedVersion: 1,
            CancellationToken.None);

        Assert(first.IsSuccess, "첫 저장이 실패했습니다.");
        Assert(!stale.IsSuccess, "오래된 발행은 충돌해야 합니다.");

        KnowledgeArticle latest = await GetRequiredAsync(repository, seed.Id);
        Result<RevisionReceipt> reapplied = await service.ReviseAsync(
            latest.Id,
            "재적용할 제목",
            PublishableBody,
            RevisionAction.Publish,
            latest.Version,
            CancellationToken.None);

        Assert(reapplied.IsSuccess, "최신 버전에 명시적으로 재적용한 변경은 성공해야 합니다.");
        Assert(reapplied.Value.CurrentVersion == 3, "재적용 뒤 버전은 3이어야 합니다.");

        KnowledgeArticle finalArticle = await GetRequiredAsync(repository, seed.Id);
        Assert(finalArticle.Status == ArticleStatus.Published, "재적용한 발행 상태가 저장되지 않았습니다.");
        Assert(finalArticle.Title == "재적용할 제목", "재적용한 제목이 저장되지 않았습니다.");
    }

    /// <summary>
    /// 존재하지 않는 키가 예외가 아닌 예상 가능한 NotFound Result가 되는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task MissingArticleReturnsFailureAsync()
    {
        var repository = new InMemoryArticleRepository([]);
        ArticleRevisionService service = CreateService(repository);

        Result<RevisionReceipt> outcome = await service.ReviseAsync(
            "KB-MISSING",
            "새 제목",
            "존재하지 않는 문서를 수정하려는 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1,
            CancellationToken.None);

        Assert(!outcome.IsSuccess, "없는 문서 수정은 실패해야 합니다.");
        Assert(outcome.Problem.Code == "article.not_found", "NotFound 오류 코드가 달라졌습니다.");
    }

    /// <summary>
    /// 호출자 취소가 일반 실패 Result로 삼켜지지 않고 OperationCanceledException으로 전파되는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task CancellationLeavesStateUnchangedAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var repository = new InMemoryArticleRepository([seed]);
        ArticleRevisionService service = CreateService(repository);

        // using 선언은 테스트가 끝날 때 CancellationTokenSource의 native 대기 자원을 정리합니다.
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await AssertThrowsAsync<OperationCanceledException>(
            async () =>
            {
                await service.ReviseAsync(
                    seed.Id,
                    "취소된 제목",
                    "취소된 요청의 본문입니다.",
                    RevisionAction.Edit,
                    expectedVersion: 1,
                    cancellation.Token);
            },
            "취소된 요청은 OperationCanceledException을 던져야 합니다.");

        KnowledgeArticle current = await GetRequiredAsync(repository, seed.Id);
        Assert(current.Version == 1, "취소된 요청이 문서 버전을 바꿨습니다.");
        Assert(current.Title == seed.Title, "취소된 요청이 문서 내용을 바꿨습니다.");
    }

    /// <summary>
    /// 조회는 끝났지만 원자적 저장 직전에 취소되면 Repository가 쓰기 전에 중단하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task CancellationObservedBeforeCompareAndSwapLeavesStateUnchangedAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var inner = new InMemoryArticleRepository([seed]);
        using CancellationTokenSource cancellation = new();
        var repository = new CancelBeforeSaveRepository(inner, cancellation);
        ArticleRevisionService service = CreateService(repository);

        await AssertThrowsAsync<OperationCanceledException>(
            async () =>
            {
                await service.ReviseAsync(
                    seed.Id,
                    "CAS 전 취소",
                    "조회 뒤 저장 직전에 취소되는 요청의 본문입니다.",
                    RevisionAction.Edit,
                    expectedVersion: 1,
                    cancellation.Token);
            },
            "CAS 전에 취소된 요청은 OperationCanceledException을 던져야 합니다.");

        KnowledgeArticle current = await GetRequiredAsync(inner, seed.Id);
        Assert(current.Version == 1, "CAS 전 취소가 문서 버전을 바꿨습니다.");
        Assert(current.Title == seed.Title, "CAS 전 취소가 문서 내용을 바꿨습니다.");
    }

    /// <summary>
    /// 원자적 저장 성공 직후 취소 신호가 와도 이미 커밋된 성공을 취소 실패로 거짓 보고하지 않는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task CancellationAfterCommitKeepsSuccessAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var inner = new InMemoryArticleRepository([seed]);
        using CancellationTokenSource cancellation = new();
        var repository = new CancelAfterSaveRepository(inner, cancellation);
        ArticleRevisionService service = CreateService(repository);

        Result<RevisionReceipt> outcome = await service.ReviseAsync(
            seed.Id,
            "커밋된 제목",
            "저장 성공 직후 취소가 도착하는 요청의 본문입니다.",
            RevisionAction.Edit,
            expectedVersion: 1,
            cancellation.Token);

        Assert(cancellation.IsCancellationRequested, "테스트 대역이 저장 뒤 취소 신호를 보내지 않았습니다.");
        Assert(outcome.IsSuccess, "이미 저장된 결과를 취소 실패로 뒤집으면 안 됩니다.");
        Assert(outcome.Value.CurrentVersion == 2, "커밋 결과 버전이 잘못됐습니다.");

        KnowledgeArticle current = await GetRequiredAsync(inner, seed.Id);
        Assert(current.Title == "커밋된 제목", "성공으로 보고한 문서 내용이 저장되지 않았습니다.");
        Assert(current.Version == 2, "성공으로 보고한 문서 버전이 저장되지 않았습니다.");
    }

    /// <summary>
    /// Strategy 목록이 모든 action을 하나씩 제공하지 않으면 Composition Root 오류가 즉시 드러나는지 확인합니다.
    /// 매개변수는 없고 반환값은 완료된 Task입니다.
    /// </summary>
    private static Task BadStrategyConfigurationFailsFastAsync()
    {
        KnowledgeArticle seed = CreateSeed();
        var repository = new InMemoryArticleRepository([seed]);

        // `_ =` discard assignment는 생성된 서비스를 보관하지 않고, 생성 과정의 구성 검증만 실행하겠다는 뜻입니다.
        AssertThrows<ArgumentException>(
            () => _ = new ArticleRevisionService(
                repository,
                [new EditContentPolicy()]),
            "Publish Strategy 누락은 구성 예외여야 합니다.");

        AssertThrows<ArgumentException>(
            () => _ = new ArticleRevisionService(
                repository,
                [new EditContentPolicy(), new EditContentPolicy(), new PublishArticlePolicy()]),
            "Edit Strategy 중복은 구성 예외여야 합니다.");

        // null!은 컴파일러 경고만 의도적으로 억제해 런타임의 잘못된 DI 목록을 재현합니다. 운영 코드에서는 만들지 않아야 합니다.
        IArticleRevisionPolicy nullPolicy = null!;
        AssertThrows<ArgumentException>(
            () => _ = new ArticleRevisionService(
                repository,
                [new EditContentPolicy(), new PublishArticlePolicy(), nullPolicy]),
            "null Strategy 항목은 이름이 분명한 구성 예외여야 합니다.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// 반복 테스트에서 사용할 버전 1 초안 문서를 안전한 팩터리로 만듭니다.
    /// 매개변수는 없습니다.
    /// 반환값은 검증된 KnowledgeArticle이며 준비 데이터 오류는 테스트 구성 예외로 바꿉니다.
    /// </summary>
    private static KnowledgeArticle CreateSeed()
    {
        Result<KnowledgeArticle> created = KnowledgeArticle.CreateDraft(
            "KB-001",
            "배포 절차",
            "배포 전에 변경 목록을 확인하고 문제가 생기면 되돌리기 절차를 실행합니다.");

        Assert(created.IsSuccess, "테스트 문서 준비가 실패했습니다.");
        return created.Value;
    }

    /// <summary>
    /// 테스트용 Repository에 표준 Edit/Publish Strategy가 연결된 Application Service를 만듭니다.
    /// repository는 테스트 대상 또는 호출 횟수·순서를 제어하는 대역입니다.
    /// 반환값은 완전히 구성된 ArticleRevisionService입니다.
    /// </summary>
    private static ArticleRevisionService CreateService(IArticleRepository repository)
    {
        return new ArticleRevisionService(
            repository,
            [new EditContentPolicy(), new PublishArticlePolicy()]);
    }

    /// <summary>
    /// 테스트 후보를 만들 검증된 ArticleRevision을 생성합니다.
    /// articleId/title/body/action/expectedVersion은 ArticleRevision.Create에 전달할 수정 요청 값입니다.
    /// 반환값은 유효한 수정 요청이며 준비 데이터가 잘못되면 테스트를 실패시킵니다.
    /// </summary>
    private static ArticleRevision CreateRevision(
        string articleId,
        string title,
        string body,
        RevisionAction action,
        int expectedVersion)
    {
        Result<ArticleRevision> created = ArticleRevision.Create(
            articleId,
            title,
            body,
            action,
            expectedVersion);

        Assert(created.IsSuccess, "테스트 수정 요청 준비가 실패했습니다.");
        return created.Value;
    }

    /// <summary>
    /// Repository에서 반드시 존재해야 하는 문서를 읽고 null이면 테스트를 실패시킵니다.
    /// repository는 조회할 저장소, articleId는 대상 문서 키입니다.
    /// 반환값은 null이 아닌 최신 KnowledgeArticle입니다.
    /// </summary>
    private static async Task<KnowledgeArticle> GetRequiredAsync(
        IArticleRepository repository,
        string articleId)
    {
        KnowledgeArticle? article = await repository.GetAsync(articleId, CancellationToken.None);
        Assert(article is not null, $"테스트 문서를 찾지 못했습니다: {articleId}");
        return article!;
    }

    /// <summary>
    /// 비동기 동작이 기대한 예외 형식을 던지는지 확인합니다.
    /// action은 실행할 비동기 함수, message는 예외가 없을 때 사용할 실패 설명입니다.
    /// TException은 기대하는 Exception 파생 형식이며 반환값은 실제로 잡은 예외입니다.
    /// </summary>
    // where 제약은 TException 자리에 Exception 파생 형식만 올 수 있게 해 catch에서 안전하게 사용하도록 합니다.
    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 동기 동작이 기대한 예외 형식을 던지는지 확인합니다.
    /// action은 실행할 함수, message는 예외가 없을 때 사용할 실패 설명입니다.
    /// TException은 기대하는 Exception 파생 형식이며 반환값은 실제로 잡은 예외입니다.
    /// </summary>
    // 같은 generic 제약을 동기 예외 도우미에도 적용해 잘못된 형식 인수를 컴파일 시점에 막습니다.
    private static TException AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 조건이 거짓이면 자체 테스트 실패를 명확한 예외로 바꿉니다.
    /// condition은 반드시 참이어야 할 조건, message는 실패 원인 설명입니다.
    /// 반환값은 없으며 조건이 거짓일 때 InvalidOperationException을 던집니다.
    /// </summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    // 이 Decorator는 Application Service가 검증·정책 실패에서 Repository를 호출하지 않는지 수치로 관찰합니다.
    private sealed class CountingRepository : IArticleRepository
    {
        private readonly IArticleRepository _inner;
        private int _getCalls;
        private int _saveCalls;

        /// <summary>
        /// 실제 동작을 위임할 Repository를 받아 호출 횟수 측정 Decorator를 만듭니다.
        /// inner는 조회와 저장을 실제 처리할 Repository입니다.
        /// 생성자는 상태를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public CountingRepository(IArticleRepository inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        // Volatile.Read는 여러 Task가 갱신한 정수를 최신 메모리 값으로 읽도록 합니다.
        public int GetCalls => Volatile.Read(ref _getCalls);

        public int SaveCalls => Volatile.Read(ref _saveCalls);

        /// <summary>
        /// 조회 호출 수를 원자적으로 올린 뒤 내부 Repository에 그대로 위임합니다.
        /// articleId는 문서 키, cancellationToken은 조회 중단 신호입니다.
        /// 반환값은 내부 Repository의 현재 문서 Task입니다.
        /// </summary>
        public Task<KnowledgeArticle?> GetAsync(string articleId, CancellationToken cancellationToken)
        {
            // Interlocked.Increment는 동시 호출에서도 증가를 잃지 않는 원자 연산입니다.
            Interlocked.Increment(ref _getCalls);
            return _inner.GetAsync(articleId, cancellationToken);
        }

        /// <summary>
        /// 저장 호출 수를 원자적으로 올린 뒤 내부 Repository의 조건부 저장에 위임합니다.
        /// candidate는 저장 후보, expectedVersion은 읽은 버전, cancellationToken은 저장 중단 신호입니다.
        /// 반환값은 내부 Repository의 ArticleSaveAttempt Task입니다.
        /// </summary>
        public Task<ArticleSaveAttempt> TrySaveAsync(
            KnowledgeArticle candidate,
            int expectedVersion,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _saveCalls);
            return _inner.TrySaveAsync(candidate, expectedVersion, cancellationToken);
        }
    }

    // 이 Decorator는 처음 N개 조회가 모두 스냅샷을 잡은 뒤에만 호출자를 풀어 실제 stale-read 경쟁을 결정적으로 만듭니다.
    private sealed class CoordinatedReadRepository : IArticleRepository
    {
        private readonly IArticleRepository _inner;
        private readonly int _readersToRelease;

        // RunContinuationsAsynchronously는 마지막 Reader가 SetResult한 호출 스택에서 다른 Reader의 후속 코드를 즉시 실행하지 않게 해 재진입을 피합니다.
        private readonly TaskCompletionSource<bool> _allReadersReady =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _readCount;

        /// <summary>
        /// 지정한 수의 첫 조회를 한 gate에서 만나게 하는 Repository Decorator를 만듭니다.
        /// inner는 실제 저장소, readersToRelease는 같은 초기 스냅샷을 잡아야 하는 양수 호출 수입니다.
        /// 생성자는 동시성 제어 상태를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public CoordinatedReadRepository(IArticleRepository inner, int readersToRelease)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

            if (readersToRelease <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(readersToRelease));
            }

            _readersToRelease = readersToRelease;
        }

        /// <summary>
        /// 먼저 내부 스냅샷을 읽고, 목표 수의 조회가 모두 도착할 때까지 첫 호출들을 비동기로 대기시킵니다.
        /// articleId는 문서 키, cancellationToken은 gate와 조회 대기를 중단할 신호입니다.
        /// 반환값은 gate 전에 읽어 둔 KnowledgeArticle 스냅샷입니다.
        /// </summary>
        public async Task<KnowledgeArticle?> GetAsync(
            string articleId,
            CancellationToken cancellationToken)
        {
            KnowledgeArticle? snapshot = await _inner.GetAsync(articleId, cancellationToken);
            int readNumber = Interlocked.Increment(ref _readCount);

            if (readNumber <= _readersToRelease)
            {
                if (readNumber == _readersToRelease)
                {
                    _allReadersReady.TrySetResult(true);
                }

                // TaskCompletionSource는 실제 시간 지연 없이 테스트가 원하는 순서에 도달했을 때만 gate를 엽니다.
                await _allReadersReady.Task.WaitAsync(cancellationToken);
            }

            return snapshot;
        }

        /// <summary>
        /// 조회 gate 뒤의 저장은 내부 Repository의 실제 compare-and-swap에 그대로 맡깁니다.
        /// candidate는 저장 후보, expectedVersion은 읽은 버전, cancellationToken은 저장 중단 신호입니다.
        /// 반환값은 내부 Repository의 ArticleSaveAttempt Task입니다.
        /// </summary>
        public Task<ArticleSaveAttempt> TrySaveAsync(
            KnowledgeArticle candidate,
            int expectedVersion,
            CancellationToken cancellationToken)
        {
            return _inner.TrySaveAsync(candidate, expectedVersion, cancellationToken);
        }
    }

    // 이 Decorator는 조회가 끝난 뒤 내부 CAS를 호출하기 직전에 취소 신호를 넣어 경계 경쟁을 재현합니다.
    private sealed class CancelBeforeSaveRepository : IArticleRepository
    {
        private readonly IArticleRepository _inner;
        private readonly CancellationTokenSource _cancellation;

        /// <summary>
        /// 조건부 저장 직전에 취소할 Repository Decorator를 만듭니다.
        /// inner는 실제 저장소, cancellation은 Application 호출에도 전달된 중단 신호 원본입니다.
        /// 생성자는 상태를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public CancelBeforeSaveRepository(
            IArticleRepository inner,
            CancellationTokenSource cancellation)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        /// <summary>
        /// 조회는 취소를 넣지 않고 내부 Repository에 그대로 위임합니다.
        /// articleId는 문서 키, cancellationToken은 조회 중단 신호입니다.
        /// 반환값은 내부 Repository의 현재 문서 Task입니다.
        /// </summary>
        public Task<KnowledgeArticle?> GetAsync(string articleId, CancellationToken cancellationToken)
        {
            return _inner.GetAsync(articleId, cancellationToken);
        }

        /// <summary>
        /// 취소 신호를 먼저 보낸 뒤 같은 token으로 내부 조건부 저장을 호출합니다.
        /// candidate는 저장 후보, expectedVersion은 읽은 버전, cancellationToken은 방금 취소할 신호입니다.
        /// 정상 반환값은 없으며 내부 Repository가 OperationCanceledException을 던집니다.
        /// </summary>
        public Task<ArticleSaveAttempt> TrySaveAsync(
            KnowledgeArticle candidate,
            int expectedVersion,
            CancellationToken cancellationToken)
        {
            _cancellation.Cancel();
            return _inner.TrySaveAsync(candidate, expectedVersion, cancellationToken);
        }
    }

    // 이 Decorator는 CAS 성공 뒤 취소를 넣어 Application이 이미 커밋된 성공을 정직하게 반환하는지 확인합니다.
    private sealed class CancelAfterSaveRepository : IArticleRepository
    {
        private readonly IArticleRepository _inner;
        private readonly CancellationTokenSource _cancellation;

        /// <summary>
        /// 조건부 저장 완료 직후 취소할 Repository Decorator를 만듭니다.
        /// inner는 실제 저장소, cancellation은 Application 호출에도 전달된 중단 신호 원본입니다.
        /// 생성자는 상태를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public CancelAfterSaveRepository(
            IArticleRepository inner,
            CancellationTokenSource cancellation)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        /// <summary>
        /// 조회는 취소를 넣지 않고 내부 Repository에 그대로 위임합니다.
        /// articleId는 문서 키, cancellationToken은 조회 중단 신호입니다.
        /// 반환값은 내부 Repository의 현재 문서 Task입니다.
        /// </summary>
        public Task<KnowledgeArticle?> GetAsync(string articleId, CancellationToken cancellationToken)
        {
            return _inner.GetAsync(articleId, cancellationToken);
        }

        /// <summary>
        /// 내부 조건부 저장 결과를 받은 뒤 취소 신호를 보내고 그 저장 결과를 그대로 반환합니다.
        /// candidate는 저장 후보, expectedVersion은 읽은 버전, cancellationToken은 저장 전까지 유효한 중단 신호입니다.
        /// 반환값은 취소 뒤에도 보존되는 실제 ArticleSaveAttempt입니다.
        /// </summary>
        public async Task<ArticleSaveAttempt> TrySaveAsync(
            KnowledgeArticle candidate,
            int expectedVersion,
            CancellationToken cancellationToken)
        {
            ArticleSaveAttempt result = await _inner.TrySaveAsync(
                candidate,
                expectedVersion,
                cancellationToken);
            _cancellation.Cancel();
            return result;
        }
    }
}
