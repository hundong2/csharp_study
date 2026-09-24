using AnnouncementCacheApi.Application.Ports;
using AnnouncementCacheApi.Domain;

namespace AnnouncementCacheApi.Application;

/// <summary>
/// 새 공지 유스케이스에 필요한 아직 검증되지 않은 입력을 모은 불변 명령입니다.
/// </summary>
/// <param name="Title">사용자가 입력한 제목입니다.</param>
/// <param name="Body">사용자가 입력한 본문입니다.</param>
/// <param name="Category">사용자가 입력한 카테고리입니다.</param>
// record는 생성 뒤 값을 바꾸지 않는 데이터 중심 타입과 값 비교를 간결하게 제공해 명령 snapshot에 사용합니다.
public sealed record CreateAnnouncementCommand(string? Title, string? Body, string? Category);

/// <summary>
/// 캐시할 공지 목록과 그 목록을 만든 원본 읽기 순번을 묶은 불변 응답 모델입니다.
/// </summary>
/// <param name="Language">목록에 적용된 정규화 언어 코드입니다.</param>
/// <param name="OriginReadNumber">cache miss 때 실제 Repository가 수행한 읽기 순번입니다.</param>
/// <param name="Items">같은 언어 전략이 적용된 공지 목록입니다.</param>
public sealed record AnnouncementFeed(
    string Language,
    long OriginReadNumber,
    IReadOnlyList<LocalizedAnnouncement> Items);

/// <summary>
/// HTTP와 저장소 사이에서 검증, 조회, 언어 전략 선택 순서를 조정하는 Application Service입니다.
/// 이 조정 책임을 HTTP·저장 구현에서 분리해 유스케이스 변경 범위를 줄이고 server 없이 독립 테스트할 수 있게 합니다.
/// </summary>
public sealed class AnnouncementApplicationService
{
    private const string DefaultLanguage = "ko";
    private readonly IAnnouncementRepository _repository;
    private readonly IReadOnlyDictionary<string, IAnnouncementLocalizationStrategy> _strategies;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 저장 Port, 사용 가능한 언어 전략, 교체 가능한 시계를 주입받아 유스케이스 서비스를 만듭니다.
    /// </summary>
    /// <param name="repository">공지 원본을 읽고 저장하는 추상 Repository입니다.</param>
    /// <param name="strategies">언어 코드별 표현 책임을 가진 Strategy 구현 모음입니다.</param>
    /// <param name="timeProvider">운영에서는 시스템 시각, 테스트에서는 고정 시각으로 바꿀 수 있는 시계입니다.</param>
    /// <returns>생성자는 값을 반환하지 않고 의존성을 보관하며 전략 중복을 검증합니다.</returns>
    public AnnouncementApplicationService(
        IAnnouncementRepository repository,
        IEnumerable<IAnnouncementLocalizationStrategy> strategies,
        TimeProvider timeProvider)
    {
        // ?? throw는 왼쪽 값이 null이면 즉시 오른쪽 예외를 던지는 null 병합 식입니다.
        // 잘못 조립된 DI 구성을 시작 시점에 빠르게 드러내기 위해 사용합니다.
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        ArgumentNullException.ThrowIfNull(strategies);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        var strategyMap = new Dictionary<string, IAnnouncementLocalizationStrategy>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var strategy in strategies)
        {
            if (!strategyMap.TryAdd(strategy.LanguageCode, strategy))
            {
                throw new ArgumentException(
                    $"언어 전략 코드가 중복되었습니다: {strategy.LanguageCode}",
                    nameof(strategies));
            }
        }

        if (!strategyMap.ContainsKey(DefaultLanguage))
        {
            throw new ArgumentException("기본 ko 언어 전략이 필요합니다.", nameof(strategies));
        }

        _strategies = strategyMap;
    }

    /// <summary>
    /// 선택적인 카테고리를 검증한 뒤 원본 목록을 읽고 요청 언어 Strategy를 적용합니다.
    /// </summary>
    /// <param name="category">null 또는 공백이면 전체, 값이 있으면 해당 카테고리만 뜻하는 외부 입력입니다.</param>
    /// <param name="language">Presentation 계층이 <c>ko</c> 또는 <c>en</c>으로 정규화한 언어입니다.</param>
    /// <param name="cancellationToken">HTTP 요청 취소를 Repository까지 전달하는 신호입니다.</param>
    /// <returns>성공 시 읽기 순번과 지역화 목록, 실패 시 카테고리 오류를 담아 완료되는 Task를 반환합니다.</returns>
    public async Task<Result<AnnouncementFeed>> GetFeedAsync(
        string? category,
        string language,
        CancellationToken cancellationToken)
    {
        string? normalizedCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryResult = Announcement.NormalizeCategory(category);
            if (!categoryResult.IsSuccess)
            {
                return Result<AnnouncementFeed>.Failure(categoryResult.Error!);
            }

            normalizedCategory = categoryResult.Value!;
        }

        var original = await _repository.ListAsync(normalizedCategory, cancellationToken);
        var strategy = SelectStrategy(language);
        var localized = new List<LocalizedAnnouncement>(original.Value.Count);

        foreach (var announcement in original.Value)
        {
            localized.Add(strategy.Localize(announcement));
        }

        return Result<AnnouncementFeed>.Success(
            new AnnouncementFeed(strategy.LanguageCode, original.OriginReadNumber, localized));
    }

    /// <summary>
    /// 캐시를 거치지 않는 상세 조회를 실행하고 찾은 공지에 언어 Strategy를 적용합니다.
    /// </summary>
    /// <param name="id">URL에서 받은 공지 식별자입니다.</param>
    /// <param name="language">표시에 사용할 정규화 언어 코드입니다.</param>
    /// <param name="cancellationToken">저장소 조회를 중단할 수 있는 요청 취소 신호입니다.</param>
    /// <returns>찾으면 지역화 공지, 없으면 not-found 오류를 담아 완료되는 Task를 반환합니다.</returns>
    public async Task<Result<LocalizedAnnouncement>> GetByIdAsync(
        Guid id,
        string language,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return Result<LocalizedAnnouncement>.Failure(
                new DomainError("announcement.id.required", "공지 식별자는 비어 있을 수 없습니다."));
        }

        var original = await _repository.FindByIdAsync(id, cancellationToken);
        if (original.Value is null)
        {
            return Result<LocalizedAnnouncement>.Failure(
                new DomainError("announcement.not_found", "요청한 공지를 찾을 수 없습니다."));
        }

        var localized = SelectStrategy(language).Localize(original.Value);
        return Result<LocalizedAnnouncement>.Success(localized);
    }

    /// <summary>
    /// 새 공지 입력을 Domain Model로 검증하고 저장한 뒤 요청 언어로 표현합니다.
    /// </summary>
    /// <param name="command">제목, 본문, 카테고리를 담은 아직 검증하지 않은 명령입니다.</param>
    /// <param name="language">생성 응답에 적용할 정규화 언어 코드입니다.</param>
    /// <param name="cancellationToken">저장 전에 요청이 끝났는지 전달하는 취소 신호입니다.</param>
    /// <returns>저장 성공 시 지역화 공지, 예상 검증·중복 실패 시 오류를 담아 완료되는 Task를 반환합니다.</returns>
    public async Task<Result<LocalizedAnnouncement>> CreateAsync(
        CreateAnnouncementCommand command,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var announcementResult = Announcement.Create(
            Guid.NewGuid(),
            command.Title,
            command.Body,
            command.Category,
            _timeProvider.GetUtcNow());

        if (!announcementResult.IsSuccess)
        {
            return Result<LocalizedAnnouncement>.Failure(announcementResult.Error!);
        }

        // 언어 표현처럼 실패 가능성이 낮은 순수 변환도 저장 전에 끝내 두면,
        // 저장은 성공했지만 응답 변환 예외 때문에 cache 세대 갱신으로 가지 못하는 틈을 만들지 않습니다.
        var localized = SelectStrategy(language).Localize(announcementResult.Value!);
        var saved = await _repository.AddAsync(announcementResult.Value!, cancellationToken);
        if (!saved.IsSuccess)
        {
            return Result<LocalizedAnnouncement>.Failure(saved.Error!);
        }

        return Result<LocalizedAnnouncement>.Success(localized);
    }

    /// <summary>
    /// 요청 언어에 맞는 Strategy를 고르고 알 수 없는 언어이면 안전한 기본 한국어 전략을 사용합니다.
    /// </summary>
    /// <param name="language">Presentation이 정규화했지만 방어적으로 다시 확인할 언어 코드입니다.</param>
    /// <returns>언어별 표시 규칙을 수행할 Strategy를 반환합니다.</returns>
    private IAnnouncementLocalizationStrategy SelectStrategy(string language)
    {
        // TryGetValue의 out var는 조회 성공 시 만들어진 전략을 같은 if 범위에서 바로 사용하게 합니다.
        if (!string.IsNullOrWhiteSpace(language) &&
            _strategies.TryGetValue(language, out var strategy))
        {
            return strategy;
        }

        return _strategies[DefaultLanguage];
    }
}
