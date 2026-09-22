using ConditionalCatalogApi.Domain;

namespace ConditionalCatalogApi.Application.Ports;

/// <summary>
/// Application이 저장 기술을 몰라도 상품을 조회하고 원자적으로 교체할 수 있게 하는 Repository Port입니다.
/// 구현을 메모리에서 데이터베이스로 바꿔도 유스케이스가 바뀌지 않게 하므로 DIP와 테스트 용이성을 얻습니다.
/// </summary>
public interface ICatalogRepository
{
    /// <summary>
    /// 식별자로 현재 상품 상태를 조회합니다.
    /// </summary>
    /// <param name="id">찾을 상품의 식별자입니다.</param>
    /// <param name="cancellationToken">호출자가 더 이상 결과를 원하지 않을 때 중단을 알리는 토큰입니다.</param>
    /// <returns>상품이 있으면 현재 불변 snapshot, 없으면 null을 담아 완료되는 Task를 반환합니다.</returns>
    Task<CatalogItem?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// 저장된 버전이 기대 버전과 같을 때만 교체하여 읽기와 쓰기 사이의 lost update를 막습니다.
    /// </summary>
    /// <param name="replacement">Domain 검증을 통과하고 버전이 하나 증가한 새 상품 상태입니다.</param>
    /// <param name="expectedVersion">호출자가 읽었던 기존 버전입니다.</param>
    /// <param name="cancellationToken">저장 시도를 중단하라는 협력적 취소 토큰입니다.</param>
    /// <returns>갱신·없음·버전 충돌 상태와 저장소의 최신 snapshot을 담은 Task를 반환합니다.</returns>
    Task<ReplaceResult> TryReplaceAsync(
        CatalogItem replacement,
        long expectedVersion,
        CancellationToken cancellationToken);
}

/// <summary>
/// Repository의 원자적 교체 시도가 어떤 이유로 끝났는지 제한된 값으로 표현합니다.
/// enum을 쓰면 임의 문자열보다 누락 없는 switch 분기를 만들기 쉽습니다.
/// </summary>
public enum ReplaceStatus
{
    Updated,
    NotFound,
    VersionConflict,
}

/// <summary>
/// 원자적 교체 결과와 그 시점의 최신 상품 snapshot을 함께 전달합니다.
/// positional record의 괄호는 속성과 생성자 parameter를 함께 선언하며, 생성자는 반환값 없이 결과 인스턴스를 초기화합니다.
/// </summary>
/// <param name="Status">저장이 성공했는지, 상품이 없는지, 버전이 충돌했는지를 나타냅니다.</param>
/// <param name="Current">성공·충돌이면 최신 상품이고, 상품이 없으면 null입니다.</param>
public sealed record ReplaceResult(ReplaceStatus Status, CatalogItem? Current);
