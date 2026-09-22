using ConditionalCatalogApi.Application.Ports;
using ConditionalCatalogApi.Domain;

namespace ConditionalCatalogApi.Infrastructure;

/// <summary>
/// 학습과 자체 검증을 위해 Dictionary에 상품을 저장하는 Repository Adapter입니다.
/// 단일 lock 안에서 버전 확인과 교체를 함께 수행하여 한 프로세스 안의 compare-and-swap을 원자적으로 만듭니다.
/// </summary>
public sealed class InMemoryCatalogRepository : ICatalogRepository
{
    private readonly object _gate = new object();
    private readonly Dictionary<Guid, CatalogItem> _items = new Dictionary<Guid, CatalogItem>();

    /// <summary>
    /// 시작 상품들을 복사해 독립적인 메모리 저장소를 만듭니다.
    /// </summary>
    /// <param name="seedItems">저장소 시작 시 넣을 검증 완료 상품 목록입니다.</param>
    /// <returns>생성자는 값을 반환하지 않고 중복 없는 초기 상태를 준비합니다.</returns>
    public InMemoryCatalogRepository(IEnumerable<CatalogItem> seedItems)
    {
        ArgumentNullException.ThrowIfNull(seedItems);

        // foreach는 목록의 항목을 하나씩 방문합니다. 중복 ID를 조용히 덮어쓰지 않고 구성 오류로 드러내기 위해 사용합니다.
        foreach (var item in seedItems)
        {
            if (!_items.TryAdd(item.Id, item))
            {
                throw new ArgumentException($"중복된 초기 상품 ID입니다: {item.Id}", nameof(seedItems));
            }
        }
    }

    /// <summary>
    /// lock으로 Dictionary 읽기를 보호한 뒤 현재 불변 상품 snapshot을 찾습니다.
    /// </summary>
    /// <param name="id">조회할 상품 식별자입니다.</param>
    /// <param name="cancellationToken">조회 전에 중단을 확인할 토큰입니다.</param>
    /// <returns>상품 또는 null을 담은 이미 완료된 Task를 반환합니다.</returns>
    public Task<CatalogItem?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // lock은 같은 _gate를 쓰는 코드가 동시에 임계 구역을 바꾸지 못하게 합니다.
        // Dictionary는 자체적으로 thread-safe하지 않으므로 조회와 교체 모두 같은 gate로 보호합니다.
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // out var는 TryGetValue의 성공 여부와 찾은 값을 한 번의 Dictionary 조회에서 함께 받는 문법입니다.
            // 별도 ContainsKey 조회를 피하고, 상품이 없을 때 item이 null인 Repository 계약도 그대로 표현합니다.
            _items.TryGetValue(id, out var item);

            // Task.FromResult는 즉시 얻은 메모리 값을 비동기 Port 모양으로 감쌉니다.
            // 실제 DB Adapter는 같은 계약에서 진짜 비동기 I/O를 수행할 수 있습니다.
            return Task.FromResult(item);
        }
    }

    /// <summary>
    /// 기대 버전 확인과 새 snapshot 교체를 하나의 lock 안에서 수행합니다.
    /// </summary>
    /// <param name="replacement">검증을 통과하고 버전이 하나 증가한 대체 상품입니다.</param>
    /// <param name="expectedVersion">호출자가 읽었던 기존 버전입니다.</param>
    /// <param name="cancellationToken">교체 전에 중단을 확인할 토큰입니다.</param>
    /// <returns>갱신·없음·버전 충돌 결과를 담은 이미 완료된 Task를 반환합니다.</returns>
    public Task<ReplaceResult> TryReplaceAsync(
        CatalogItem replacement,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        cancellationToken.ThrowIfCancellationRequested();

        if (replacement.Version != checked(expectedVersion + 1))
        {
            throw new ArgumentException(
                "대체 상품 버전은 기대 버전보다 정확히 1 커야 합니다.",
                nameof(replacement));
        }

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_items.TryGetValue(replacement.Id, out var current))
            {
                return Task.FromResult(new ReplaceResult(ReplaceStatus.NotFound, Current: null));
            }

            if (current.Version != expectedVersion)
            {
                return Task.FromResult(new ReplaceResult(ReplaceStatus.VersionConflict, current));
            }

            _items[replacement.Id] = replacement;
            return Task.FromResult(new ReplaceResult(ReplaceStatus.Updated, replacement));
        }
    }
}
