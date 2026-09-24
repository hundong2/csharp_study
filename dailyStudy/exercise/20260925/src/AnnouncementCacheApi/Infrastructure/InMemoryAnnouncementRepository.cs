using System.Collections.Concurrent;
using AnnouncementCacheApi.Application.Ports;
using AnnouncementCacheApi.Domain;

namespace AnnouncementCacheApi.Infrastructure;

/// <summary>
/// 프로세스 메모리에 공지를 보관하는 thread-safe Repository Adapter입니다.
/// </summary>
public sealed class InMemoryAnnouncementRepository : IAnnouncementRepository
{
    private static readonly TimeSpan DemonstrationReadDelay = TimeSpan.FromMilliseconds(120);
    private readonly ConcurrentDictionary<Guid, Announcement> _announcements;
    private readonly TimeSpan _readDelay;
    private long _readCount;

    /// <summary>
    /// Interlocked로 관리되는 누적 원본 읽기 횟수를 원자적으로 읽습니다.
    /// </summary>
    // =>는 중괄호와 return 대신 식 하나로 값을 돌려주는 expression-bodied member 문법입니다.
    // 짧은 읽기 전용 속성이라 동시성 안전한 읽기 의도를 바로 보이게 합니다.
    public long ReadCount => Interlocked.Read(ref _readCount);

    /// <summary>
    /// 고정 seed와 짧은 읽기 지연을 사용해 학습용 Repository를 만듭니다.
    /// </summary>
    /// <returns>생성자는 값을 반환하지 않고 고정 seed와 학습용 읽기 지연을 준비합니다.</returns>
    public InMemoryAnnouncementRepository()
        : this(CreateSeedAnnouncements(), DemonstrationReadDelay)
    {
    }

    /// <summary>
    /// 자체 테스트가 seed와 고정된 시뮬레이션 지연 시간을 바꿀 수 있는 Repository를 만듭니다.
    /// </summary>
    /// <param name="seedAnnouncements">처음부터 저장할 검증된 공지 스냅샷입니다.</param>
    /// <param name="readDelay">각 원본 읽기에 적용할 0 이상의 비동기 지연입니다.</param>
    /// <returns>생성자는 값을 반환하지 않고 주어진 seed와 지연으로 독립 Repository를 준비합니다.</returns>
    internal InMemoryAnnouncementRepository(
        IEnumerable<Announcement> seedAnnouncements,
        TimeSpan readDelay)
    {
        ArgumentNullException.ThrowIfNull(seedAnnouncements);
        if (readDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(readDelay), "읽기 지연은 음수일 수 없습니다.");
        }

        _announcements = new ConcurrentDictionary<Guid, Announcement>();
        foreach (var announcement in seedAnnouncements)
        {
            if (!_announcements.TryAdd(announcement.Id, announcement))
            {
                throw new ArgumentException(
                    $"seed 공지 식별자가 중복되었습니다: {announcement.Id}",
                    nameof(seedAnnouncements));
            }
        }

        _readDelay = readDelay;
    }

    /// <summary>
    /// 카테고리에 맞는 현재 공지를 복사해 게시 시각과 식별자로 정렬한 스냅샷을 반환합니다.
    /// </summary>
    /// <param name="normalizedCategory">null이면 전체, 값이 있으면 정확히 일치하는 카테고리입니다.</param>
    /// <param name="cancellationToken">인위적인 원본 읽기 지연과 후속 작업을 중단할 신호입니다.</param>
    /// <returns>이번 원본 읽기 순번과 안전하게 복사한 불변 목록을 담아 완료되는 Task를 반환합니다.</returns>
    public async Task<RepositoryRead<IReadOnlyList<Announcement>>> ListAsync(
        string? normalizedCategory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var readNumber = Interlocked.Increment(ref _readCount);

        // Task.Delay를 await하면 요청 스레드를 막지 않으면서 여러 요청이 겹치는 상황을 관찰하기 쉽게 만듭니다.
        await Task.Delay(_readDelay, cancellationToken);

        // LINQ Where는 원본을 바꾸지 않고 조건을 통과한 공지만 다음 단계로 흘려보냅니다.
        // OrderByDescending/ThenBy는 최신 시각 우선과 같은 시각의 Guid 순서를 명시합니다.
        // ToArray가 이 시점의 결과를 고정해 이후 저장이 이미 반환한 목록을 바꾸지 않게 합니다.
        // => lambda는 announcement 하나를 받아 필터 조건이나 정렬 key를 돌려주는 이름 없는 짧은 함수입니다.
        var snapshot = _announcements.Values
            .Where(announcement =>
                normalizedCategory is null ||
                string.Equals(
                    announcement.Category,
                    normalizedCategory,
                    StringComparison.Ordinal))
            .OrderByDescending(announcement => announcement.PublishedAtUtc)
            .ThenBy(announcement => announcement.Id)
            .ToArray();

        return new RepositoryRead<IReadOnlyList<Announcement>>(readNumber, snapshot);
    }

    /// <summary>
    /// 식별자로 공지를 찾되 목록과 마찬가지로 실제 원본 읽기 지연과 순번을 기록합니다.
    /// </summary>
    /// <param name="id">ConcurrentDictionary에서 찾을 공지 식별자입니다.</param>
    /// <param name="cancellationToken">읽기 지연 도중 연결 종료를 전달할 취소 신호입니다.</param>
    /// <returns>읽기 순번과 찾은 공지 또는 null을 담아 완료되는 Task를 반환합니다.</returns>
    public async Task<RepositoryRead<Announcement?>> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var readNumber = Interlocked.Increment(ref _readCount);
        await Task.Delay(_readDelay, cancellationToken);

        _announcements.TryGetValue(id, out var announcement);
        return new RepositoryRead<Announcement?>(readNumber, announcement);
    }

    /// <summary>
    /// ConcurrentDictionary의 원자적 TryAdd로 기존 공지를 덮어쓰지 않고 새 공지를 저장합니다.
    /// </summary>
    /// <param name="announcement">Domain 검증을 이미 통과한 불변 공지입니다.</param>
    /// <param name="cancellationToken">저장 전에 요청 중단 여부를 확인할 신호입니다.</param>
    /// <returns>저장 성공 Result 또는 매우 드문 식별자 중복 실패를 담은 완료 Task를 반환합니다.</returns>
    public Task<Result<Announcement>> AddAsync(
        Announcement announcement,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        cancellationToken.ThrowIfCancellationRequested();

        var result = _announcements.TryAdd(announcement.Id, announcement)
            ? Result<Announcement>.Success(announcement)
            : Result<Announcement>.Failure(
                new DomainError("announcement.id.duplicate", "이미 존재하는 공지 식별자입니다."));

        return Task.FromResult(result);
    }

    /// <summary>
    /// 실행할 때마다 같은 학습 결과를 내도록 세 카테고리의 고정 seed 공지를 만듭니다.
    /// </summary>
    /// <returns>release, maintenance, general 공지가 담긴 읽기 전용 목록을 반환합니다.</returns>
    private static IReadOnlyList<Announcement> CreateSeedAnnouncements()
    {
        // [ ... ]는 여러 값을 간결하게 배열로 만드는 C# 컬렉션 식입니다.
        Announcement[] announcements =
        [
            CreateSeedAnnouncement(
                "11111111-1111-1111-1111-111111111111",
                "출력 캐시 API 공개",
                "동일한 목록 요청은 5분 동안 원본 읽기를 줄입니다.",
                "release",
                new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero)),
            CreateSeedAnnouncement(
                "22222222-2222-2222-2222-222222222222",
                "정기 점검 안내",
                "점검 중에는 공지 등록이 잠시 제한될 수 있습니다.",
                "maintenance",
                new DateTimeOffset(2026, 9, 24, 9, 0, 0, TimeSpan.Zero)),
            CreateSeedAnnouncement(
                "33333333-3333-3333-3333-333333333333",
                "학습 환경 안내",
                "Accept-Language와 category에 따라 별도 캐시 항목이 만들어집니다.",
                "general",
                new DateTimeOffset(2026, 9, 23, 3, 0, 0, TimeSpan.Zero))
        ];

        return announcements;
    }

    /// <summary>
    /// 코드에 고정한 seed도 일반 Domain 팩터리를 통과시켜 운영 데이터와 같은 규칙을 보장합니다.
    /// </summary>
    /// <param name="id">파싱 가능한 고정 Guid 문자열입니다.</param>
    /// <param name="title">seed 제목입니다.</param>
    /// <param name="body">seed 본문입니다.</param>
    /// <param name="category">seed 카테고리입니다.</param>
    /// <param name="publishedAtUtc">seed UTC 게시 시각입니다.</param>
    /// <returns>검증이 끝난 Announcement를 반환합니다.</returns>
    private static Announcement CreateSeedAnnouncement(
        string id,
        string title,
        string body,
        string category,
        DateTimeOffset publishedAtUtc)
    {
        var result = Announcement.Create(Guid.Parse(id), title, body, category, publishedAtUtc);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"잘못된 seed 공지입니다: {result.Error!.Message}");
        }

        return result.Value!;
    }
}
