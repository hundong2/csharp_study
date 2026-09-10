// file-scoped namespace는 이 파일의 모든 형식을 같은 이름 공간에 넣으면서 중첩 중괄호를 줄입니다.
namespace OptimisticConcurrencyExercise;

internal static class Program
{
    /// <summary>
    /// 예제 의존성을 조립하고 낙관적 동시성 데모 또는 자체 테스트를 실행합니다.
    /// args는 --self-test 같은 명령행 옵션이며, 반환값 0은 성공이고 1은 데모 검증 실패입니다.
    /// </summary>
    // async Main은 Repository I/O를 기다리는 동안 스레드를 막지 않는 실제 애플리케이션 진입 형태를 보여 줍니다.
    private static async Task<int> Main(string[] args)
    {
        // Contains는 명령행 배열에 옵션이 있는지 찾는 LINQ 메서드이며 대소문자 차이는 무시합니다.
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return await SelfTests.RunAsync();
        }

        KnowledgeArticle seed = CreateSeed();

        // new(...)는 왼쪽 형식에서 생성할 형식을 추론하는 target-typed new이고, [seed]는 항목 하나를 담는 collection expression입니다.
        InMemoryArticleRepository repository = new([seed]);

        // 이곳은 Composition Root입니다. 구체 Adapter와 Strategy를 골라 Application Service에 생성자 주입합니다.
        // [] collection expression은 여러 Strategy를 읽기 쉬운 배열로 만드는 안정 C# 문법입니다.
        IArticleRevisionPolicy[] policies =
        [
            new EditContentPolicy(),
            new PublishArticlePolicy(),
        ];

        ArticleRevisionService service = new(repository, policies);

        // tuple은 데모에만 쓰는 작은 입력 묶음입니다. 핵심 업무 입력은 검증과 불변식이 있는 ArticleRevision으로 변환됩니다.
        (string Actor, string? Title, string? Body, RevisionAction Action, int ExpectedVersion)[] requests =
        [
            (
                "alice",
                "안전한 배포 전 점검",
                "담당자는 변경 목록, 데이터 마이그레이션, 되돌리기 절차를 차례로 확인합니다.",
                RevisionAction.Edit,
                1),
            (
                "bob-stale",
                "운영 배포 체크리스트",
                "배포 전에 승인 기록과 관측 지표를 확인하고, 실패하면 즉시 되돌리기 절차를 실행합니다.",
                RevisionAction.Publish,
                1),
            (
                "bob-retry",
                "운영 배포 체크리스트",
                "최신 내용을 다시 읽은 뒤 승인 기록, 데이터 마이그레이션, 관측 지표, 되돌리기 절차를 모두 확인합니다.",
                RevisionAction.Publish,
                2),
            (
                "guest",
                "   ",
                "제목이 비어 있으므로 저장소까지 도달하지 않는 잘못된 입력입니다.",
                RevisionAction.Edit,
                3),
        ];

        // List<T>는 실행 결과 수가 입력 수에 따라 달라질 때 항목을 차례로 추가하기 좋은 가변 컬렉션입니다.
        List<(string Actor, Result<RevisionReceipt> Outcome)> outcomes = [];

        // foreach는 요청을 선언된 순서로 실행하여 stale version → 명시적 재조회/재적용 흐름을 재현 가능하게 보여 줍니다.
        foreach ((string actor, string? title, string? body, RevisionAction action, int expectedVersion) in requests)
        {
            Result<RevisionReceipt> outcome = await service.ReviseAsync(
                seed.Id,
                title,
                body,
                action,
                expectedVersion,
                CancellationToken.None);

            outcomes.Add((actor, outcome));
            PrintOutcome(actor, action, expectedVersion, outcome);
        }

        KnowledgeArticle? finalArticle = await repository.GetAsync(seed.Id, CancellationToken.None);
        if (finalArticle is null)
        {
            Console.WriteLine("[ERROR] 최종 문서를 찾을 수 없습니다.");
            return 1;
        }

        Console.WriteLine(
            $"[FINAL] {finalArticle.Id} v{finalArticle.Version} {finalArticle.Status} \"{finalArticle.Title}\"");

        // Count의 lambda는 각 결과를 조건식으로 검사합니다. 실패 Result에서는 먼저 !IsSuccess를 확인해 Problem 접근을 안전하게 합니다.
        int applied = outcomes.Count(item => item.Outcome.IsSuccess);
        int conflicts = outcomes.Count(
            item => !item.Outcome.IsSuccess && item.Outcome.Problem.Code == "article.version_conflict");
        int rejected = outcomes.Count - applied - conflicts;

        Console.WriteLine($"[SUMMARY] applied={applied}, conflicts={conflicts}, rejected={rejected}");

        bool isExpected =
            finalArticle.Version == 3 &&
            finalArticle.Status == ArticleStatus.Published &&
            applied == 2 &&
            conflicts == 1 &&
            rejected == 1;

        // 삼항 연산자 ?:는 최종 검증 조건에 따라 프로세스 종료 코드 두 값 중 하나를 고릅니다.
        return isExpected ? 0 : 1;
    }

    /// <summary>
    /// 데모에서 공동 편집할 버전 1 초안 문서를 만듭니다.
    /// 매개변수는 없습니다.
    /// 반환값은 팩터리 검증을 통과한 KnowledgeArticle이며 준비 데이터가 잘못되면 구성 예외를 던집니다.
    /// </summary>
    private static KnowledgeArticle CreateSeed()
    {
        Result<KnowledgeArticle> created = KnowledgeArticle.CreateDraft(
            "KB-001",
            "배포 절차",
            "배포 전에 변경 목록을 확인하고 문제가 생기면 되돌리기 절차를 실행합니다.");

        if (!created.IsSuccess)
        {
            throw new InvalidOperationException($"데모 문서 생성 실패: {created.Problem.Code}");
        }

        return created.Value;
    }

    /// <summary>
    /// 한 수정 요청의 성공 또는 실패를 초보자가 버전 흐름을 추적할 수 있는 한 줄로 출력합니다.
    /// actor는 편집자 이름, action은 수정 종류, expectedVersion은 편집 기준, outcome은 서비스 결과입니다.
    /// 반환값은 없으며 콘솔에 APPLIED, CONFLICT, REJECTED 중 하나를 기록합니다.
    /// </summary>
    private static void PrintOutcome(
        string actor,
        RevisionAction action,
        int expectedVersion,
        Result<RevisionReceipt> outcome)
    {
        if (outcome.IsSuccess)
        {
            RevisionReceipt receipt = outcome.Value;
            Console.WriteLine(
                $"[APPLIED] {actor} {action} v{receipt.PreviousVersion}->v{receipt.CurrentVersion} {receipt.Status}");
            return;
        }

        Problem problem = outcome.Problem;
        // `is int currentVersion`은 nullable int에 실제 값이 있을 때 검사와 변수 추출을 함께 하는 type pattern입니다.
        if (problem.CurrentVersion is int currentVersion)
        {
            Console.WriteLine(
                $"[CONFLICT] {actor} {problem.Code} expected=v{expectedVersion} actual=v{currentVersion}");
            return;
        }

        Console.WriteLine($"[REJECTED] {actor} {problem.Code}");
    }
}
