// file-scoped namespace는 이 파일의 모든 형식을 같은 이름 공간에 넣으면서 중첩 중괄호를 줄입니다.
namespace OptimisticConcurrencyExercise;

// 저장 Adapter의 조건부 갱신 결과를 세 상태로 제한하면 bool보다 실패 원인을 분명하게 전달할 수 있습니다.
public enum ArticleSaveStatus
{
    Saved,
    NotFound,
    Conflict,
}

// Repository의 compare-and-swap 결과입니다. Application은 구체 DB 예외 대신 이 안정된 계약에 의존합니다.
public sealed class ArticleSaveAttempt
{
    private readonly KnowledgeArticle? _currentArticle;

    /// <summary>
    /// 조건부 저장 결과와, 결과 해석에 필요한 최신 문서 스냅샷을 보관합니다.
    /// status는 저장 상태, currentArticle은 성공 또는 충돌 시점의 최신 문서이며 NotFound에서는 null입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private ArticleSaveAttempt(ArticleSaveStatus status, KnowledgeArticle? currentArticle)
    {
        Status = status;
        _currentArticle = currentArticle;
    }

    public ArticleSaveStatus Status { get; }

    public KnowledgeArticle CurrentArticle
    {
        get
        {
            if (_currentArticle is null)
            {
                throw new InvalidOperationException("NotFound 저장 결과에는 현재 문서가 없습니다.");
            }

            return _currentArticle;
        }
    }

    /// <summary>
    /// compare-and-swap에 성공한 결과를 만듭니다.
    /// savedArticle은 Repository에 실제로 반영된 최신 불변 스냅샷입니다.
    /// 반환값은 Saved 상태의 ArticleSaveAttempt입니다.
    /// </summary>
    public static ArticleSaveAttempt Saved(KnowledgeArticle savedArticle)
    {
        ArgumentNullException.ThrowIfNull(savedArticle);
        return new ArticleSaveAttempt(ArticleSaveStatus.Saved, savedArticle);
    }

    /// <summary>
    /// 대상 문서가 저장 직전 사라진 결과를 만듭니다.
    /// 매개변수는 없습니다.
    /// 반환값은 현재 문서가 없는 NotFound 상태입니다.
    /// </summary>
    public static ArticleSaveAttempt NotFound()
    {
        return new ArticleSaveAttempt(ArticleSaveStatus.NotFound, currentArticle: null);
    }

    /// <summary>
    /// 다른 작성자가 먼저 저장하여 버전이 달라진 결과를 만듭니다.
    /// currentArticle은 충돌 뒤 Repository에서 관측한 최신 문서입니다.
    /// 반환값은 최신 스냅샷을 가진 Conflict 상태입니다.
    /// </summary>
    public static ArticleSaveAttempt Conflict(KnowledgeArticle currentArticle)
    {
        ArgumentNullException.ThrowIfNull(currentArticle);
        return new ArticleSaveAttempt(ArticleSaveStatus.Conflict, currentArticle);
    }
}

// Application이 ConcurrentDictionary나 EF Core를 직접 모르도록 저장 기술을 Port 뒤에 둡니다. 이것이 DIP와 테스트 대역 교체의 핵심입니다.
public interface IArticleRepository
{
    /// <summary>
    /// 문서 ID로 현재 불변 스냅샷을 조회합니다.
    /// articleId는 조회할 문서 키, cancellationToken은 저장소 대기를 중단할 신호입니다.
    /// 반환값은 현재 KnowledgeArticle이며 문서가 없으면 null입니다.
    /// </summary>
    Task<KnowledgeArticle?> GetAsync(string articleId, CancellationToken cancellationToken);

    /// <summary>
    /// Repository의 현재 버전이 expectedVersion일 때만 후보 스냅샷을 원자적으로 저장합니다.
    /// candidate는 Version이 1 증가한 후보, expectedVersion은 작성자가 읽었던 버전, cancellationToken은 저장 전 중단 신호입니다.
    /// 반환값은 Saved, NotFound, Conflict 중 하나인 조건부 저장 결과입니다.
    /// </summary>
    Task<ArticleSaveAttempt> TrySaveAsync(
        KnowledgeArticle candidate,
        int expectedVersion,
        CancellationToken cancellationToken);
}

// 수정 종류마다 업무 규칙을 교체할 수 있는 Strategy Port입니다. Application Service의 if/switch 확산을 줄여 OCP를 돕습니다.
public interface IArticleRevisionPolicy
{
    RevisionAction Action { get; }

    /// <summary>
    /// 현재 문서와 검증된 요청에 해당 수정 종류의 업무 규칙을 적용합니다.
    /// current는 읽은 문서 스냅샷, revision은 같은 버전을 대상으로 한 새 내용입니다.
    /// 반환값은 Version이 1 증가한 저장 후보 또는 예상 가능한 정책 실패입니다.
    /// </summary>
    Result<KnowledgeArticle> Apply(KnowledgeArticle current, ArticleRevision revision);
}

// 내용 저장 Strategy는 공개 상태를 바꾸지 않고 새 버전만 만듭니다.
public sealed class EditContentPolicy : IArticleRevisionPolicy
{
    public RevisionAction Action => RevisionAction.Edit;

    /// <summary>
    /// 제목과 본문을 교체하되 현재 Draft/Published 상태는 유지합니다.
    /// current는 수정 전 스냅샷, revision은 검증된 새 내용과 예상 버전입니다.
    /// 반환값은 Version이 1 증가한 후보 또는 Published 상태의 공통 본문 불변식 실패입니다.
    /// </summary>
    public Result<KnowledgeArticle> Apply(KnowledgeArticle current, ArticleRevision revision)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(revision);

        return current.CreateNextRevision(revision, current.Status);
    }
}

// 발행 Strategy는 내용 규칙을 확인한 뒤 상태를 Published로 전환합니다.
public sealed class PublishArticlePolicy : IArticleRevisionPolicy
{
    public RevisionAction Action => RevisionAction.Publish;

    /// <summary>
    /// 충분한 설명이 있는 문서를 새 버전으로 저장하고 Published 상태로 전환합니다.
    /// current는 발행 전 스냅샷, revision은 검증된 새 내용과 예상 버전입니다.
    /// 반환값은 발행 후보 또는 본문이 너무 짧다는 정책 실패입니다.
    /// </summary>
    public Result<KnowledgeArticle> Apply(KnowledgeArticle current, ArticleRevision revision)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(revision);

        // Strategy는 다음 상태를 선택하고, 발행 상태가 지켜야 할 본문 길이 불변식은 Domain aggregate가 모든 경로에 공통 적용합니다.
        return current.CreateNextRevision(revision, ArticleStatus.Published);
    }
}

// 입력 검증, 조회, Strategy 적용, 조건부 저장 순서를 조정하는 Application Service입니다.
public sealed class ArticleRevisionService
{
    private readonly IArticleRepository _repository;
    private readonly IReadOnlyDictionary<RevisionAction, IArticleRevisionPolicy> _policies;

    /// <summary>
    /// 문서 Repository와 수정 종류별 Strategy를 주입받아 서비스를 초기화합니다.
    /// repository는 저장 Port, policies는 모든 RevisionAction을 정확히 하나씩 처리할 구현 목록입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없으며 누락·중복 Strategy는 시작 시 구성 예외로 막습니다.
    /// </summary>
    public ArticleRevisionService(
        IArticleRepository repository,
        IEnumerable<IArticleRevisionPolicy> policies)
    {
        // ??는 왼쪽 값이 null이면 오른쪽을 선택하는 null-coalescing 연산자입니다. 필수 의존성 누락을 조립 시점에 막습니다.
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        ArgumentNullException.ThrowIfNull(policies);

        // LINQ ToArray는 지연 실행 가능한 입력을 한 번만 열거해 고정하고, Any는 그 배열에 null 항목이 하나라도 있는지 검사합니다.
        IArticleRevisionPolicy[] materialized = policies.ToArray();
        if (materialized.Any(policy => policy is null))
        {
            throw new ArgumentException("Revision Strategy 목록에는 null을 넣을 수 없습니다.", nameof(policies));
        }

        // GroupBy는 Action이 같은 Strategy를 묶습니다. `policy => policy.Action` lambda는 각 항목에서 묶음 키를 고르는 짧은 함수입니다.
        // 중복 구성은 실행 중 임의 선택하지 않고 시작 시 실패시킵니다.
        RevisionAction[] duplicateActions = materialized
            .GroupBy(policy => policy.Action)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateActions.Length > 0)
        {
            throw new ArgumentException(
                $"중복 Revision Strategy: {string.Join(", ", duplicateActions)}",
                nameof(policies));
        }

        // ToDictionary는 Action을 키로 삼아 분기문 없이 Strategy를 찾는 읽기 전용 lookup을 만듭니다.
        _policies = materialized.ToDictionary(policy => policy.Action);

        RevisionAction[] missingActions = Enum
            .GetValues<RevisionAction>()
            .Where(action => !_policies.ContainsKey(action))
            .ToArray();

        if (missingActions.Length > 0)
        {
            throw new ArgumentException(
                $"누락 Revision Strategy: {string.Join(", ", missingActions)}",
                nameof(policies));
        }
    }

    /// <summary>
    /// 외부 입력을 검증하고 최신 문서를 읽어 정책을 적용한 뒤 예상 버전이 같을 때만 저장합니다.
    /// articleId/title/body/action은 수정 내용, expectedVersion은 작성자가 읽었던 버전, cancellationToken은 저장소 대기 중단 신호입니다.
    /// 반환값은 적용된 버전 영수증 또는 검증·조회·정책·동시성 실패 Problem입니다.
    /// </summary>
    // async/await는 실제 DB Adapter의 I/O를 기다릴 때 스레드를 붙잡지 않게 하며, 이 메모리 Adapter에서도 같은 계약을 유지합니다.
    public async Task<Result<RevisionReceipt>> ReviseAsync(
        string? articleId,
        string? title,
        string? body,
        RevisionAction action,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        Result<ArticleRevision> created = ArticleRevision.Create(
            articleId,
            title,
            body,
            action,
            expectedVersion);

        if (!created.IsSuccess)
        {
            return Result<RevisionReceipt>.Failure(created.Problem);
        }

        ArticleRevision revision = created.Value;
        KnowledgeArticle? current = await _repository.GetAsync(revision.ArticleId, cancellationToken);

        // is null pattern은 문서 부재를 명시합니다. null을 예외나 빈 가짜 문서로 바꾸지 않습니다.
        if (current is null)
        {
            return Result<RevisionReceipt>.Failure(
                new Problem("article.not_found", "수정할 문서를 찾을 수 없습니다."));
        }

        if (current.Version != revision.ExpectedVersion)
        {
            return Conflict(revision.ExpectedVersion, current.Version);
        }

        IArticleRevisionPolicy policy = _policies[revision.Action];
        Result<KnowledgeArticle> candidate = policy.Apply(current, revision);
        if (!candidate.IsSuccess)
        {
            return Result<RevisionReceipt>.Failure(candidate.Problem);
        }

        // 조회 뒤 다른 작성자가 저장할 수 있으므로 실제 안전성은 Repository의 원자적 조건부 쓰기에서 완성됩니다.
        ArticleSaveAttempt saveAttempt = await _repository.TrySaveAsync(
            candidate.Value,
            revision.ExpectedVersion,
            cancellationToken);

        return MapSaveAttempt(revision, saveAttempt);
    }

    /// <summary>
    /// Repository의 기술 중립 저장 상태를 사용자에게 돌려줄 Application Result로 변환합니다.
    /// revision은 원 요청 버전, saveAttempt는 원자적 저장 결과입니다.
    /// 반환값은 RevisionReceipt 성공 또는 삭제·동시성 충돌 실패입니다.
    /// </summary>
    private static Result<RevisionReceipt> MapSaveAttempt(
        ArticleRevision revision,
        ArticleSaveAttempt saveAttempt)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(saveAttempt);

        // switch expression은 저장 상태별 결과를 빠짐없이 한 값으로 매핑합니다. _는 손상된 Adapter 상태를 구성 오류로 드러냅니다.
        return saveAttempt.Status switch
        {
            ArticleSaveStatus.Saved => Result<RevisionReceipt>.Success(
                new RevisionReceipt(
                    saveAttempt.CurrentArticle.Id,
                    revision.ExpectedVersion,
                    saveAttempt.CurrentArticle.Version,
                    saveAttempt.CurrentArticle.Status)),
            ArticleSaveStatus.NotFound => Result<RevisionReceipt>.Failure(
                new Problem("article.not_found", "저장 직전 문서가 사라졌습니다.")),
            ArticleSaveStatus.Conflict => Conflict(
                revision.ExpectedVersion,
                saveAttempt.CurrentArticle.Version),
            _ => throw new InvalidOperationException("알 수 없는 Repository 저장 상태입니다."),
        };
    }

    /// <summary>
    /// 기대 버전과 실제 최신 버전을 모두 담은 동시성 충돌 Result를 만듭니다.
    /// expectedVersion은 작성자가 편집한 기준, currentVersion은 Repository의 최신 버전입니다.
    /// 반환값은 자동 덮어쓰지 말고 재조회·비교하라는 article.version_conflict 실패입니다.
    /// </summary>
    private static Result<RevisionReceipt> Conflict(int expectedVersion, int currentVersion)
    {
        return Result<RevisionReceipt>.Failure(
            new Problem(
                "article.version_conflict",
                $"편집 기준은 v{expectedVersion}이지만 현재 문서는 v{currentVersion}입니다. 최신 내용을 다시 읽고 변경을 재적용하세요.",
                currentVersion));
    }
}
