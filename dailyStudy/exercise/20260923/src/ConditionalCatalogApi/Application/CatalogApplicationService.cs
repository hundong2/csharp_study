using ConditionalCatalogApi.Application.Ports;
using ConditionalCatalogApi.Domain;

namespace ConditionalCatalogApi.Application;

/// <summary>
/// HTTP 세부사항 없이 “상품 조회”와 “기대한 버전으로 상품 수정” 유스케이스를 조율하는 Application Service입니다.
/// Domain 규칙은 CatalogItem에, 저장 원자성은 Repository에 맡기고 순서와 실패 변환만 담당해 SRP를 지킵니다.
/// </summary>
public sealed class CatalogApplicationService
{
    private const int MaxUpdateAttempts = 8;
    private readonly ICatalogRepository _repository;

    /// <summary>
    /// 상품 유스케이스가 사용할 Repository Port를 주입받습니다.
    /// </summary>
    /// <param name="repository">구체 저장 기술 대신 Application이 의존할 상품 저장 계약입니다.</param>
    /// <returns>생성자는 값을 반환하지 않고 서비스를 사용할 준비를 합니다.</returns>
    public CatalogApplicationService(ICatalogRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>
    /// 현재 상품 snapshot을 식별자로 조회합니다.
    /// </summary>
    /// <param name="id">찾을 상품 식별자입니다.</param>
    /// <param name="cancellationToken">HTTP 연결 종료 같은 중단 요청을 Repository까지 전달하는 토큰입니다.</param>
    /// <returns>상품이 있으면 불변 snapshot, 없으면 null을 담아 완료되는 Task를 반환합니다.</returns>
    public Task<CatalogItem?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return _repository.FindAsync(id, cancellationToken);
    }

    /// <summary>
    /// 호출자가 허용한 버전일 때만 입력을 검증하고 compare-and-swap 충돌을 제한된 횟수만큼 재시도합니다.
    /// </summary>
    /// <param name="id">수정할 상품 식별자입니다.</param>
    /// <param name="isVersionAllowed">현재 버전이 호출자의 원래 동시성 전제 조건에 맞는지 판단하는 함수입니다.</param>
    /// <param name="command">새 이름과 가격을 담은 수정 명령입니다.</param>
    /// <param name="cancellationToken">조회와 저장에 전달할 중단 요청 토큰입니다.</param>
    /// <returns>갱신·없음·검증 실패·버전 충돌·재시도 한도 초과 중 하나와 관련 값을 담은 Task를 반환합니다.</returns>
    // Func<long, bool>은 long 버전을 받아 허용 여부 bool을 돌려주는 함수 타입입니다.
    // Application이 ETag 문자열을 몰라도 wildcard나 여러 허용 버전을 매 충돌 뒤 다시 검사하게 합니다.
    // async는 메서드가 await를 사용할 수 있고 최종 결과를 Task에 담아 반환한다는 C# 키워드입니다.
    public async Task<UpdateCatalogResult> UpdateAsync(
        Guid id,
        Func<long, bool> isVersionAllowed,
        UpdateCatalogCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(isVersionAllowed);
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        // await는 I/O가 끝날 때까지 thread를 점유하지 않고 기다리는 문법입니다.
        // 지금은 메모리 Adapter지만 DB Adapter로 교체해도 Application 흐름을 바꾸지 않기 위해 비동기 Port를 유지합니다.
        var current = await _repository.FindAsync(id, cancellationToken);
        if (current is null)
        {
            return UpdateCatalogResult.NotFound();
        }

        // CAS 직전에 다른 writer가 저장하면 Repository가 최신 snapshot을 돌려줍니다.
        // 유한 반복은 원래 전제 조건을 최신 버전에 다시 적용하되, 지속 경합에서 CPU를 무한히 쓰지 않게 합니다.
        for (var attempt = 1; attempt <= MaxUpdateAttempts; attempt++)
        {
            if (!isVersionAllowed(current.Version))
            {
                return UpdateCatalogResult.VersionConflict(current);
            }

            var revision = current.Revise(command.Name, command.Price);
            if (!revision.IsSuccess)
            {
                return UpdateCatalogResult.ValidationFailed(revision.Error);
            }

            // 조회 뒤 다른 요청이 먼저 저장할 수 있으므로 최종 판정은 Repository의 원자적 compare-and-swap에 맡깁니다.
            var stored = await _repository.TryReplaceAsync(
                revision.Value,
                current.Version,
                cancellationToken);

            if (stored.Status == ReplaceStatus.Updated)
            {
                return UpdateCatalogResult.Updated(stored.Current);
            }

            if (stored.Status == ReplaceStatus.NotFound)
            {
                return UpdateCatalogResult.NotFound();
            }

            if (stored.Status != ReplaceStatus.VersionConflict || stored.Current is null)
            {
                throw new InvalidOperationException("버전 충돌 결과에는 최신 상품이 필요합니다.");
            }

            current = stored.Current;
        }

        // 마지막 CAS가 돌려준 최신 버전에서 원래 조건이 이미 거짓이면 503이 아니라 정확한 버전 충돌입니다.
        if (!isVersionAllowed(current.Version))
        {
            return UpdateCatalogResult.VersionConflict(current);
        }

        return UpdateCatalogResult.ContentionLimitExceeded(current);
    }
}

/// <summary>
/// 상품 수정 유스케이스에 필요한 사용자 입력만 담는 불변 명령입니다.
/// positional record의 괄호는 속성 생성과 생성자 parameter 선언을 합친 문법이며, 생성자는 반환값 없이 인스턴스를 초기화합니다.
/// </summary>
/// <param name="Name">null일 수 있는 새 상품 이름입니다.</param>
/// <param name="Price">새 원화 가격입니다.</param>
public sealed record UpdateCatalogCommand(string? Name, decimal Price);

/// <summary>
/// Application Service가 HTTP 상태 코드를 몰라도 수정 결과를 명확히 표현하게 합니다.
/// </summary>
public enum UpdateCatalogStatus
{
    Updated,
    NotFound,
    ValidationFailed,
    VersionConflict,
    ContentionLimitExceeded,
}

/// <summary>
/// 수정 상태와 성공·충돌·재시도 한도 초과 snapshot 또는 검증 오류를 함께 전달합니다.
/// </summary>
public sealed class UpdateCatalogResult
{
    /// <summary>
    /// 일관된 수정 결과를 내부에서만 만들 수 있게 상태·상품·오류를 초기화합니다.
    /// </summary>
    /// <param name="status">수정 유스케이스의 종료 상태입니다.</param>
    /// <param name="item">갱신·충돌·재시도 한도 초과 시 최신 상품이며 다른 상태에서는 null입니다.</param>
    /// <param name="error">검증 실패 원인이며 다른 상태에서는 null입니다.</param>
    /// <returns>생성자는 값을 반환하지 않고 새 결과 객체를 초기화합니다.</returns>
    private UpdateCatalogResult(
        UpdateCatalogStatus status,
        CatalogItem? item,
        DomainError? error)
    {
        Status = status;
        Item = item;
        Error = error;
    }

    /// <summary>
    /// 수정 유스케이스가 끝난 이유를 반환합니다.
    /// </summary>
    public UpdateCatalogStatus Status { get; }

    /// <summary>
    /// 성공·충돌·재시도 한도 초과 때의 최신 상품을 반환하며, 값이 없는 상태에서는 null입니다.
    /// </summary>
    public CatalogItem? Item { get; }

    /// <summary>
    /// Domain 검증 실패를 반환하며 검증 실패가 아니면 null입니다.
    /// </summary>
    public DomainError? Error { get; }

    /// <summary>
    /// 저장된 최신 상품을 담은 성공 결과를 만듭니다.
    /// </summary>
    /// <param name="item">Repository가 저장한 최신 상품입니다.</param>
    /// <returns>Updated 상태의 Application 결과를 반환합니다.</returns>
    public static UpdateCatalogResult Updated(CatalogItem? item)
    {
        return new UpdateCatalogResult(
            UpdateCatalogStatus.Updated,
            RequireItem(item, "갱신 성공 결과에는 상품이 필요합니다."),
            error: null);
    }

    /// <summary>
    /// 대상 상품이 없다는 결과를 만듭니다.
    /// </summary>
    /// <returns>상품과 오류 값이 없는 NotFound 결과를 반환합니다.</returns>
    public static UpdateCatalogResult NotFound()
    {
        return new UpdateCatalogResult(UpdateCatalogStatus.NotFound, item: null, error: null);
    }

    /// <summary>
    /// 사용자가 고칠 수 있는 Domain 검증 실패 결과를 만듭니다.
    /// </summary>
    /// <param name="error">CatalogItem이 발견한 입력 오류입니다.</param>
    /// <returns>ValidationFailed 상태와 오류를 담은 결과를 반환합니다.</returns>
    public static UpdateCatalogResult ValidationFailed(DomainError? error)
    {
        if (error is null)
        {
            throw new ArgumentNullException(nameof(error), "검증 실패 결과에는 오류가 필요합니다.");
        }

        return new UpdateCatalogResult(UpdateCatalogStatus.ValidationFailed, item: null, error);
    }

    /// <summary>
    /// 기대 버전이 오래되었음을 최신 상품과 함께 알리는 결과를 만듭니다.
    /// </summary>
    /// <param name="current">충돌 시점에 Repository가 가진 최신 상품입니다.</param>
    /// <returns>VersionConflict 상태와 최신 상품을 담은 결과를 반환합니다.</returns>
    public static UpdateCatalogResult VersionConflict(CatalogItem? current)
    {
        return new UpdateCatalogResult(
            UpdateCatalogStatus.VersionConflict,
            RequireItem(current, "버전 충돌 결과에는 최신 상품이 필요합니다."),
            error: null);
    }

    /// <summary>
    /// 원래 전제 조건은 계속 맞지만 연속 충돌로 안전한 저장을 끝내지 못했다는 결과를 만듭니다.
    /// </summary>
    /// <param name="current">마지막 compare-and-swap 충돌에서 관찰한 최신 상품입니다.</param>
    /// <returns>잠시 뒤 다시 시도해야 하는 ContentionLimitExceeded 상태와 최신 상품을 반환합니다.</returns>
    public static UpdateCatalogResult ContentionLimitExceeded(CatalogItem? current)
    {
        return new UpdateCatalogResult(
            UpdateCatalogStatus.ContentionLimitExceeded,
            RequireItem(current, "재시도 한도 초과 결과에는 최신 상품이 필요합니다."),
            error: null);
    }

    /// <summary>
    /// 성공·충돌·재시도 한도 초과 결과에 필수인 상품이 누락되면 프로그래밍 오류로 즉시 드러냅니다.
    /// </summary>
    /// <param name="item">null이면 안 되는 상품 후보입니다.</param>
    /// <param name="message">누락 시 예외에 포함할 설계 불변식 설명입니다.</param>
    /// <returns>null이 아님을 확인한 같은 CatalogItem을 반환합니다.</returns>
    private static CatalogItem RequireItem(CatalogItem? item, string message)
    {
        return item ?? throw new InvalidOperationException(message);
    }
}
