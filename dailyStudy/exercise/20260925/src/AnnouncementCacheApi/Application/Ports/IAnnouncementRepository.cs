using AnnouncementCacheApi.Domain;

namespace AnnouncementCacheApi.Application.Ports;

/// <summary>
/// 원본 저장소가 몇 번째 읽기에서 어떤 값을 돌려주었는지 함께 운반하는 불변 관찰값입니다.
/// </summary>
/// <typeparam name="T">저장소가 읽어 온 값의 형식입니다.</typeparam>
/// <param name="OriginReadNumber">프로세스 시작 뒤 수행된 원본 읽기의 순번입니다.</param>
/// <param name="Value">해당 원본 읽기가 만든 값입니다.</param>
public sealed record RepositoryRead<T>(long OriginReadNumber, T Value);

/// <summary>
/// Application이 저장 기술을 몰라도 공지를 읽고 추가할 수 있게 하는 Repository Port입니다.
/// </summary>
// Port를 사이에 두는 설계는 저장소 교체가 유스케이스 코드에 번지지 않게 하는 SOLID의 의존성 역전입니다.
public interface IAnnouncementRepository
{
    /// <summary>
    /// 진단과 학습 출력에 사용할 누적 원본 읽기 횟수를 제공합니다.
    /// </summary>
    long ReadCount { get; }

    /// <summary>
    /// 전체 또는 특정 카테고리의 공지를 결정적인 순서로 읽습니다.
    /// </summary>
    /// <param name="normalizedCategory">null이면 전체, 값이 있으면 이미 정규화된 카테고리만 조회합니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료를 저장소 대기까지 전달하는 취소 신호입니다.</param>
    /// <returns>읽기 순번과 공지 스냅샷을 담아 완료되는 Task를 반환합니다.</returns>
    Task<RepositoryRead<IReadOnlyList<Announcement>>> ListAsync(
        string? normalizedCategory,
        CancellationToken cancellationToken);

    /// <summary>
    /// 식별자로 공지 한 건을 원본 저장소에서 찾습니다.
    /// </summary>
    /// <param name="id">찾을 공지의 비어 있지 않은 식별자입니다.</param>
    /// <param name="cancellationToken">조회 지연 중 작업을 중단할 취소 신호입니다.</param>
    /// <returns>읽기 순번과 공지 또는 찾지 못했음을 뜻하는 null을 담아 완료되는 Task를 반환합니다.</returns>
    Task<RepositoryRead<Announcement?>> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    /// <summary>
    /// 검증된 공지를 저장하며 같은 식별자의 덮어쓰기를 허용하지 않습니다.
    /// </summary>
    /// <param name="announcement">Domain 검증을 통과한 불변 공지입니다.</param>
    /// <param name="cancellationToken">저장 시작 전에 요청 중단을 확인할 취소 신호입니다.</param>
    /// <returns>저장된 공지 또는 중복 식별자 실패를 담은 완료 Task를 반환합니다.</returns>
    Task<Result<Announcement>> AddAsync(
        Announcement announcement,
        CancellationToken cancellationToken);
}
