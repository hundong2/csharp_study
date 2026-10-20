using AuditEventApi.Application.Ports;
using AuditEventApi.Domain;

namespace AuditEventApi.Application;

/// <summary>
/// 이벤트 종류에 맞는 Strategy 선택, Domain 검증, 원자적 중복 저장 순서를 조정하는 Application Service입니다.
/// </summary>
/// <remarks>
/// 이 형식은 HTTP 응답 코드나 Dictionary 구현을 모릅니다. 계층 경계를 지키면 같은 유스케이스를 다른 입출력 기술에서도 재사용하고 테스트하기 쉽습니다.
/// </remarks>
public sealed class IngestAuditEventService
{
    private readonly IAuditEventRepository _repository;
    private readonly IReadOnlyDictionary<string, IAuditEventMapper> _mappers;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 저장 Port, 종류별 Mapper Strategy, 교체 가능한 시계를 주입받아 유스케이스를 구성합니다.
    /// </summary>
    /// <param name="repository">중복 검사와 저장을 원자적으로 처리할 Repository Port입니다.</param>
    /// <param name="mappers">지원하는 이벤트 종류마다 하나씩 등록한 Mapper Strategy 모음입니다.</param>
    /// <param name="timeProvider">운영에서는 시스템 시각, 테스트에서는 고정 시각을 제공할 추상 시계입니다.</param>
    /// <returns>생성자이므로 값을 반환하지 않고 서비스의 의존성과 Strategy 표를 초기화합니다.</returns>
    public IngestAuditEventService(
        IAuditEventRepository repository,
        IEnumerable<IAuditEventMapper> mappers,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(mappers);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _timeProvider = timeProvider;

        // var는 오른쪽 생성식에서 형식을 추론하며, 이 값의 실제 형식은 HashSet<string>입니다.
        // HashSet은 공식 세 종류인지 빠르게 확인해 mapper가 빠지거나 몰래 늘어난 구성 drift를 시작 시점에 막습니다.
        var supportedKinds = new HashSet<string>(
            AuditEventMappers.SupportedKinds,
            StringComparer.Ordinal);
        // Dictionary는 kind를 key로 사용해 긴 if/switch 없이 알맞은 Strategy를 빠르게 찾게 합니다.
        var mapperCatalog = new Dictionary<string, IAuditEventMapper>(StringComparer.Ordinal);
        foreach (var mapper in mappers)
        {
            if (mapper is null || string.IsNullOrWhiteSpace(mapper.SupportedKind))
            {
                throw new InvalidOperationException("모든 IAuditEventMapper에는 비어 있지 않은 SupportedKind가 필요합니다.");
            }

            // 내부 Strategy 이름은 사용자 입력과 달리 정규화하지 않습니다. 공백·대소문자 차이도 공식 계약 drift로 즉시 드러냅니다.
            var registeredKind = mapper.SupportedKind;
            if (!supportedKinds.Contains(registeredKind))
            {
                // $ 문자열은 중괄호 안의 실제 종류 값을 설명 문장에 넣는 문자열 보간 문법입니다.
                throw new InvalidOperationException(
                    $"'{mapper.SupportedKind}' 종류는 공식 감사 이벤트 종류 목록에 없습니다.");
            }

            // TryAdd는 같은 kind의 Strategy가 두 개일 때 하나를 임의 선택하지 않고 구성 오류로 드러내게 합니다.
            if (!mapperCatalog.TryAdd(registeredKind, mapper))
            {
                throw new InvalidOperationException(
                    $"'{mapper.SupportedKind}' 종류의 IAuditEventMapper가 두 번 등록되었습니다.");
            }
        }

        foreach (var supportedKind in AuditEventMappers.SupportedKinds)
        {
            if (!mapperCatalog.ContainsKey(supportedKind))
            {
                throw new InvalidOperationException(
                    $"'{supportedKind}' 종류의 IAuditEventMapper가 등록되지 않았습니다.");
            }
        }

        _mappers = mapperCatalog;
    }

    /// <summary>
    /// 종류별 Domain 변환을 실행하고 검증 완료 이벤트를 같은 EventId가 없을 때만 저장합니다.
    /// </summary>
    /// <param name="command">Presentation 형식에서 분리된 안정적인 감사 이벤트 입력입니다.</param>
    /// <param name="cancellationToken">클라이언트 연결 종료처럼 처리를 중단해야 할 때 전달하는 취소 신호입니다.</param>
    /// <returns>새로 저장한 AuditEvent 또는 지원 종류·검증·중복 오류를 반환합니다.</returns>
    // async/await는 저장 작업을 기다리는 동안 요청 thread를 붙잡지 않고, 완료되면 이어서 결과를 처리하게 합니다.
    public async Task<Result<AuditEvent>> IngestAsync(
        IngestAuditEventCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        // ?.는 Kind가 null일 때 Trim을 호출하지 않고 null을 유지해 아래의 명시적인 입력 오류로 연결합니다.
        var normalizedKind = command.Kind?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedKind)
            || !_mappers.TryGetValue(normalizedKind, out var mapper))
        {
            return Result<AuditEvent>.Failure(
                new Error(
                    "audit.unsupported_kind",
                    $"kind는 {string.Join(", ", AuditEventMappers.SupportedKinds)} 중 하나여야 합니다.",
                    "kind"));
        }

        // Strategy는 종류별 선택 필드만 Domain 팩터리에 넘기므로 서비스 자체는 새 종류의 세부 규칙을 알 필요가 없습니다.
        var draftResult = mapper.Map(command);
        if (!draftResult.IsSuccess)
        {
            // !는 위의 성공 검사로 Error가 null이 아님을 compiler에 알려 nullable 경고를 없앱니다.
            return Result<AuditEvent>.Failure(draftResult.Error!);
        }

        var auditEvent = AuditEvent.Create(draftResult.Value!, _timeProvider.GetUtcNow());
        // Repository가 존재 확인과 추가를 한 원자적 연산으로 묶어야 동시 요청도 중복 저장되지 않습니다.
        var added = await _repository.TryAddAsync(auditEvent, cancellationToken);
        if (!added)
        {
            return Result<AuditEvent>.Failure(
                new Error(
                    "audit.duplicate_event_id",
                    "같은 eventId의 감사 이벤트가 이미 저장되어 있습니다.",
                    "eventId"));
        }

        return Result<AuditEvent>.Success(auditEvent);
    }

    /// <summary>
    /// 입력 식별자를 저장 때와 같은 방식으로 정규화해 감사 이벤트를 조회합니다.
    /// </summary>
    /// <param name="eventId">조회할 이벤트 식별자이며 대소문자와 양끝 공백은 구분하지 않습니다.</param>
    /// <param name="cancellationToken">호출자가 조회를 더 기다리지 않을 때 전달하는 취소 신호입니다.</param>
    /// <returns>찾은 AuditEvent 또는 존재하지 않거나 식별자가 비었을 때 null을 반환합니다.</returns>
    public ValueTask<AuditEvent?> FindByEventIdAsync(
        string eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return ValueTask.FromResult<AuditEvent?>(null);
        }

        return _repository.FindByEventIdAsync(
            eventId.Trim().ToUpperInvariant(),
            cancellationToken);
    }
}
