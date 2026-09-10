using System.Collections.Concurrent;

// file-scoped namespace는 이 파일의 모든 형식을 같은 이름 공간에 넣으면서 중첩 중괄호를 줄입니다.
namespace OptimisticConcurrencyExercise;

// 이 Adapter는 ConcurrentDictionary.TryUpdate를 이용해 DB의 `UPDATE ... WHERE Version = @expected` 조건부 쓰기를 메모리에서 흉내 냅니다.
// ConcurrentDictionary는 process-local이므로 여러 서버가 공유하는 운영 저장소를 대신하지는 않습니다.
public sealed class InMemoryArticleRepository : IArticleRepository
{
    private readonly ConcurrentDictionary<string, KnowledgeArticle> _articles =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 시작 문서들을 독립 키로 보관하는 메모리 Repository를 만듭니다.
    /// seed는 서로 다른 Id를 가진 유효한 불변 문서 모음입니다.
    /// 생성자는 상태를 초기화하므로 반환값은 없으며 null 항목이나 중복 Id는 구성 예외로 막습니다.
    /// </summary>
    public InMemoryArticleRepository(IEnumerable<KnowledgeArticle> seed)
    {
        ArgumentNullException.ThrowIfNull(seed);

        foreach (KnowledgeArticle article in seed)
        {
            ArgumentNullException.ThrowIfNull(article);

            if (!_articles.TryAdd(article.Id, article))
            {
                throw new ArgumentException($"중복 문서 ID: {article.Id}", nameof(seed));
            }
        }
    }

    /// <summary>
    /// 현재 dictionary에 저장된 불변 문서 스냅샷을 읽습니다.
    /// articleId는 조회할 문서 키, cancellationToken은 조회 전에 중단할 신호입니다.
    /// 반환값은 찾은 KnowledgeArticle이며 없으면 null인 이미 완료된 Task입니다.
    /// </summary>
    public Task<KnowledgeArticle?> GetAsync(string articleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(articleId);
        cancellationToken.ThrowIfCancellationRequested();

        // out은 조회 성공 시 값을 article 변수로 돌려주는 문법입니다. 찾지 못하면 nullable article은 null입니다.
        _articles.TryGetValue(articleId, out KnowledgeArticle? article);
        return Task.FromResult(article);
    }

    /// <summary>
    /// 현재 문서 버전이 expectedVersion과 같을 때만 candidate로 원자 교체합니다.
    /// candidate는 다음 버전 스냅샷, expectedVersion은 조회 당시 버전, cancellationToken은 교체 직전 중단 신호입니다.
    /// 반환값은 성공, 문서 부재, 또는 최신 스냅샷을 포함한 충돌 결과입니다.
    /// </summary>
    public Task<ArticleSaveAttempt> TrySaveAsync(
        KnowledgeArticle candidate,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (expectedVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "예상 버전은 1 이상이어야 합니다.");
        }

        int requiredCandidateVersion = checked(expectedVersion + 1);
        if (candidate.Version != requiredCandidateVersion)
        {
            throw new InvalidOperationException(
                $"저장 후보 버전은 예상 버전보다 정확히 1 커야 합니다. expected={expectedVersion}, candidate={candidate.Version}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_articles.TryGetValue(candidate.Id, out KnowledgeArticle? current))
        {
            return Task.FromResult(ArticleSaveAttempt.NotFound());
        }

        if (current.Version != expectedVersion)
        {
            return Task.FromResult(ArticleSaveAttempt.Conflict(current));
        }

        // 취소 확인은 원자 쓰기 바로 전에 둡니다. 쓰기 성공 뒤 취소를 던지면 호출자가 저장 실패로 오해하여 중복 적용할 수 있습니다.
        cancellationToken.ThrowIfCancellationRequested();

        // TryUpdate는 key의 현재 값이 방금 읽은 current와 같을 때만 candidate로 바꾸는 compare-and-swap입니다.
        // 다른 작성자가 사이에 저장했다면 false가 되어 그 작성자의 값을 덮어쓰지 않습니다.
        if (_articles.TryUpdate(candidate.Id, candidate, current))
        {
            return Task.FromResult(ArticleSaveAttempt.Saved(candidate));
        }

        // 이 예제에는 삭제 기능이 없지만 Port 계약은 운영 Adapter의 조회-저장 사이 삭제 경쟁도 정직하게 표현합니다.
        if (!_articles.TryGetValue(candidate.Id, out KnowledgeArticle? latest))
        {
            return Task.FromResult(ArticleSaveAttempt.NotFound());
        }

        return Task.FromResult(ArticleSaveAttempt.Conflict(latest));
    }
}
