using RequestAdmissionApi.Application.Ports;
using RequestAdmissionApi.Domain;

namespace RequestAdmissionApi.Application;

/// <summary>
/// HTTP와 무관한 보고서 생성 유스케이스 입력입니다. Nullable 속성은 외부 입력이 아직 검증되지 않았음을 드러냅니다.
/// </summary>
/// <param name="Title">사용자가 보낸 제목입니다.</param>
/// <param name="Format">사용자가 보낸 형식 문자열입니다.</param>
/// <param name="Rows">사용자가 보낸 행들입니다.</param>
public sealed record CreateReportCommand(
    string? Title,
    string? Format,
    IReadOnlyList<ReportRowDraft?>? Rows);

/// <summary>
/// 도메인 검증, 렌더링 전략 선택, 저장 순서를 조율하는 Application Service입니다.
/// 구체 어댑터 대신 포트에만 의존하므로 단일 책임과 의존성 역전 원칙을 지키고 테스트하기 쉽습니다.
/// </summary>
public sealed class ReportApplicationService
{
    private readonly IReadOnlyDictionary<ReportFormat, IReportRenderer> renderers;
    private readonly IReportRepository repository;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// 보고서 생성에 필요한 협력 객체들을 주입받습니다.
    /// <paramref name="renderers"/>는 형식별 전략들, <paramref name="repository"/>는 저장 포트,
    /// <paramref name="timeProvider"/>는 교체 가능한 시계이며 서비스 인스턴스를 초기화합니다.
    /// </summary>
    public ReportApplicationService(
        IEnumerable<IReportRenderer> renderers,
        IReportRepository repository,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(renderers);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // var는 오른쪽 생성식으로 타입이 분명할 때 중복을 줄이며, 실제 타입은 Dictionary<ReportFormat, IReportRenderer>입니다.
        var rendererMap = new Dictionary<ReportFormat, IReportRenderer>();
        foreach (var renderer in renderers)
        {
            if (renderer is null)
            {
                throw new ArgumentException("렌더러 목록에 null을 넣을 수 없습니다.", nameof(renderers));
            }

            var rendererFormat = renderer.Format;
            // 강제 형 변환으로 만든 미정의 enum 값도 들어올 수 있으므로 DI가 넘긴 값이라도 서비스 생성 시 거절합니다.
            if (!Enum.IsDefined(rendererFormat))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(renderers),
                    rendererFormat,
                    "정의되지 않은 보고서 형식의 렌더러는 등록할 수 없습니다.");
            }

            if (!rendererMap.TryAdd(rendererFormat, renderer))
            {
                throw new ArgumentException($"{rendererFormat} 형식의 렌더러가 중복 등록되었습니다.", nameof(renderers));
            }
        }

        // Enum.GetValues<T>는 enum에 선언된 모든 값을 열거해 새 형식의 Strategy 등록 누락을 시작 시 발견하게 합니다.
        foreach (var format in Enum.GetValues<ReportFormat>())
        {
            if (!rendererMap.ContainsKey(format))
            {
                throw new ArgumentException($"{format} 형식의 렌더러가 등록되지 않았습니다.", nameof(renderers));
            }
        }

        // 외부 코드가 참조하지 않는 private Dictionary를 읽기 전용 interface로만 보관해 구성 변경을 막습니다.
        this.renderers = rendererMap;
        this.repository = repository;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// 보고서 생성 유스케이스 전체를 실행합니다.
    /// <paramref name="command"/>는 미검증 입력, <paramref name="cancellationToken"/>은 클라이언트 중단 신호이며
    /// 성공하면 저장된 보고서를, 예상 가능한 검증 실패면 오류 Result를 반환합니다.
    /// 취소는 업무 실패가 아니므로 Result로 바꾸지 않고 OperationCanceledException으로 호출자에게 전파합니다.
    /// </summary>
    public async Task<Result<Report>> CreateAsync(
        CreateReportCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var validation = ReportRequest.Create(command.Title, command.Format, command.Rows);
        if (!validation.IsSuccess)
        {
            return Result<Report>.Failure(new ApplicationError(
                "validation_failed",
                "보고서 요청을 확인해 주세요.",
                validation.Errors));
        }

        // !는 직전 성공 검사로 Request가 null이 아님을 확인했다는 사실을 Nullable 분석기에 알려 줍니다.
        var request = validation.Request!;
        // Dictionary indexer는 생성자에서 완전성을 확인한 Strategy map에서 형식에 맞는 구현을 찾습니다.
        var renderer = renderers[request.Format];

        // await는 스레드를 붙잡지 않고 렌더링 I/O가 끝난 뒤 이어서 실행하게 하며, 취소 신호도 아래로 전달합니다.
        var content = await renderer.RenderAsync(request, cancellationToken);
        var report = new Report(
            Guid.NewGuid(),
            request.Title,
            request.Format,
            content,
            timeProvider.GetUtcNow());

        await repository.SaveAsync(report, cancellationToken);
        return Result<Report>.Success(report);
    }

    /// <summary>
    /// 생성된 보고서를 식별자로 조회합니다.
    /// <paramref name="id"/>는 보고서 ID, <paramref name="cancellationToken"/>은 호출 중단 신호이며
    /// 저장소에 있으면 보고서를, 없으면 null을 반환합니다.
    /// ValueTask는 메모리 조회처럼 자주 즉시 끝나는 비동기 작업에서 불필요한 Task 할당을 줄이는 반환 형식입니다.
    /// </summary>
    public ValueTask<Report?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return repository.FindByIdAsync(id, cancellationToken);
    }
}
