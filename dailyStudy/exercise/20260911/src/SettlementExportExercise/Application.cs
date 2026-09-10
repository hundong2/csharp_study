// file-scoped namespace는 이 파일의 형식을 같은 이름 공간에 넣으면서 중첩 중괄호를 줄입니다.
namespace SettlementExportExercise;

// Repository Port는 Application이 필요한 조회만 정의합니다. 저장 기술이나 SQL 세부 사항은 드러내지 않습니다.
public interface ISettlementRepository
{
    /// <summary>
    /// 지정한 영업일에 내보낼 준비가 된 정산 항목을 조회합니다.
    /// businessDate는 조회 대상 날짜, cancellationToken은 호출자가 중단을 요청하는 신호입니다.
    /// 반환값은 null·중복 없이 같은 날짜의 Ready 항목만 든 읽기 전용 목록 Task이며, 호출 뒤 내용도 바뀌면 안 됩니다.
    /// </summary>
    Task<IReadOnlyList<SettlementEntry>> ListReadyAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken);
}

// Formatter Strategy는 같은 정산 항목을 어떤 외부 텍스트 형식으로 바꿀지 교체할 수 있게 합니다.
public interface ISettlementFormatter
{
    /// <summary>이 Strategy가 처리하는 유일한 ExportFormat 선택 키를 반환합니다.</summary>
    ExportFormat Format { get; }

    /// <summary>점과 경로 없이 ASCII 영문자·숫자로만 된 1~10자 파일 확장자를 반환합니다.</summary>
    string FileExtension { get; }

    /// <summary>비어 있지 않고 줄바꿈이 없는 출력 파일 첫 줄을 반환합니다.</summary>
    string Header { get; }

    /// <summary>
    /// 유효한 정산 항목 하나를 외부 파일의 한 논리적 줄로 바꿉니다.
    /// entry는 Domain 검증을 통과한 정산 항목입니다.
    /// 반환값은 구분자와 escaping 규칙을 적용한 한 줄 문자열입니다.
    /// </summary>
    string FormatRow(SettlementEntry entry);
}

// Factory Port는 Application이 구체 파일·클라우드 SDK를 모르고도 소유할 쓰기 세션을 열게 합니다.
public interface ISettlementExportSessionFactory
{
    /// <summary>
    /// 아직 공개되지 않은 staged 내보내기 세션을 엽니다.
    /// destinationName은 확장자를 포함한 안전한 대상 이름, cancellationToken은 열기 중단 신호입니다.
    /// 반환값은 null이 아니고 DestinationName이 요청과 Ordinal로 같은 세션 Task이며 반드시 DisposeAsync 해야 합니다.
    /// </summary>
    Task<ISettlementExportSession> OpenAsync(
        string destinationName,
        CancellationToken cancellationToken);
}

// IAsyncDisposable은 파일 flush나 원격 upload abort처럼 기다려야 하는 정리를 DisposeAsync로 표현하는 표준 계약입니다.
// 세션은 여러 줄을 임시 영역에 쓴 뒤 Commit해야만 공개하는 작은 트랜잭션 경계처럼 동작합니다.
public interface ISettlementExportSession : IAsyncDisposable
{
    /// <summary>Factory에 요청한 값과 Ordinal로 같은 최종 목적지 이름을 반환합니다.</summary>
    string DestinationName { get; }

    // ValueTask는 실제 I/O 또는 동기 완료를 모두 표현하면서 동기 완료가 흔한 Adapter의 할당을 줄일 수 있습니다.
    /// <summary>
    /// 공개 전 임시 영역에 텍스트 한 줄을 추가합니다.
    /// line은 줄바꿈 문자를 포함하지 않는 완성된 한 줄, cancellationToken은 쓰기 중단 신호입니다.
    /// 반환값은 비동기 쓰기 완료를 나타내는 ValueTask입니다.
    /// </summary>
    ValueTask WriteLineAsync(string line, CancellationToken cancellationToken);

    /// <summary>
    /// 지금까지 staged 영역에 쓴 모든 줄을 하나의 완성 파일로 공개합니다.
    /// cancellationToken은 공개가 시작되기 전에 중단을 요청하는 신호입니다.
    /// 반환값은 commit 완료를 나타내는 ValueTask이며, 성공 뒤에는 같은 세션을 다시 쓸 수 없습니다.
    /// </summary>
    ValueTask CommitAsync(CancellationToken cancellationToken);
}

// receipt는 호출자가 성공 사실과 공개된 목적지를 추적하는 positional record 불변 응답입니다.
// primary constructor의 다섯 파라미터는 목적지·영업일·형식·행 수·합계이며 생성자는 초기화만 하고 반환값은 없습니다.
public sealed record SettlementExportReceipt(
    string DestinationName,
    DateOnly BusinessDate,
    ExportFormat Format,
    int ExportedCount,
    decimal TotalAmount);

// Application Service는 입력 검증 → 조회 → Strategy 변환 → staged 쓰기 → commit 순서를 조정합니다.
public sealed class SettlementExportService
{
    private readonly ISettlementRepository _repository;
    private readonly ISettlementExportSessionFactory _sessionFactory;
    private readonly IReadOnlyDictionary<ExportFormat, FormatterRegistration> _formatters;

    /// <summary>
    /// 정산 내보내기 유스케이스에 필요한 Port와 Formatter Strategy를 주입받아 구성합니다.
    /// repository는 조회 Port, sessionFactory는 출력 세션 Factory, formatters는 enum별 출력 Strategy 모음입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없으며 누락·중복·잘못된 구성은 시작 시 예외로 알립니다.
    /// </summary>
    public SettlementExportService(
        ISettlementRepository repository,
        ISettlementExportSessionFactory sessionFactory,
        IEnumerable<ISettlementFormatter> formatters)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(sessionFactory);
        ArgumentNullException.ThrowIfNull(formatters);

        _repository = repository;
        _sessionFactory = sessionFactory;

        // var는 오른쪽 생성식에서 정확한 형식을 추론합니다. 형식이 명확하고 반복이 긴 지역 변수에만 사용했습니다.
        var byFormat = new Dictionary<ExportFormat, FormatterRegistration>();

        foreach (ISettlementFormatter? formatter in formatters)
        {
            if (formatter is null)
            {
                throw new ArgumentException("Formatter 목록에는 null을 넣을 수 없습니다.", nameof(formatters));
            }

            // 구성 객체의 속성을 정확히 한 번 읽어 검증값과 실행값 사이에 바뀌는 TOCTOU를 막습니다.
            ExportFormat configuredFormat = formatter.Format;
            string? configuredExtension = formatter.FileExtension;
            string? configuredHeader = formatter.Header;

            if (!Enum.IsDefined(configuredFormat))
            {
                throw new ArgumentException("Formatter가 정의되지 않은 형식을 사용합니다.", nameof(formatters));
            }

            if (!IsSafeExtension(configuredExtension))
            {
                throw new ArgumentException("Formatter 확장자는 ASCII 영문자와 숫자로 된 1~10자여야 합니다.", nameof(formatters));
            }

            if (string.IsNullOrWhiteSpace(configuredHeader) || ContainsLineBreak(configuredHeader))
            {
                throw new ArgumentException("Formatter 헤더는 비어 있지 않은 한 줄이어야 합니다.", nameof(formatters));
            }

            var registration = new FormatterRegistration(
                formatter,
                configuredExtension,
                configuredHeader);

            if (!byFormat.TryAdd(configuredFormat, registration))
            {
                throw new ArgumentException($"{configuredFormat} Formatter가 중복되었습니다.", nameof(formatters));
            }
        }

        // Enum.GetValues<T>()는 enum에 정의된 모든 값을 배열로 돌려주어 새 형식 추가 시 누락 검사를 자동으로 확장합니다.
        foreach (ExportFormat format in Enum.GetValues<ExportFormat>())
        {
            if (!byFormat.ContainsKey(format))
            {
                throw new ArgumentException($"{format} Formatter가 누락되었습니다.", nameof(formatters));
            }
        }

        _formatters = byFormat;
    }

    /// <summary>
    /// 한 영업일의 Ready 정산을 선택한 형식으로 staged 작성한 뒤 성공할 때만 공개합니다.
    /// exportName은 확장자 없는 안전한 이름, businessDate는 대상 날짜, format은 출력 형식, cancellationToken은 중단 신호입니다.
    /// 반환값은 공개된 파일의 영수증 또는 사용자가 처리할 수 있는 입력·빈 결과 Problem입니다.
    /// </summary>
    // async 메서드는 await 지점에서 스레드를 막지 않고 Task로 완료·예외·취소를 호출자에게 전달합니다.
    public async Task<Result<SettlementExportReceipt>> ExportAsync(
        string? exportName,
        DateOnly businessDate,
        ExportFormat format,
        CancellationToken cancellationToken)
    {
        // 이미 취소된 호출은 검증이나 외부 의존성 호출보다 먼저 멈춰 불필요한 작업을 만들지 않습니다.
        cancellationToken.ThrowIfCancellationRequested();

        Result<SettlementExportRequest> requestResult = SettlementExportRequest.Create(
            exportName,
            businessDate,
            format);

        if (!requestResult.IsSuccess)
        {
            return Result<SettlementExportReceipt>.Failure(requestResult.Problem);
        }

        SettlementExportRequest request = requestResult.Value;
        FormatterRegistration formatter = _formatters[request.Format];

        IReadOnlyList<SettlementEntry> repositoryRows = await _repository.ListReadyAsync(
            request.BusinessDate,
            cancellationToken);

        // Adapter의 마지막 내부 검사 직후 취소될 수 있어, 빈 결과 조기 return보다 호출자 취소를 우선 확인합니다.
        cancellationToken.ThrowIfCancellationRequested();

        SettlementEntry[] orderedRows = ValidateAndOrderRows(
            repositoryRows,
            request.BusinessDate,
            cancellationToken);
        if (orderedRows.Length == 0)
        {
            return Result<SettlementExportReceipt>.Failure(
                new Problem("export.no_ready_settlements", "이 영업일에는 내보낼 Ready 정산이 없습니다."));
        }

        // decimal을 사용해 돈 계산에서 이진 부동소수점 오차를 피하고, 반복 중에도 큰 작업의 취소를 관측합니다.
        decimal totalAmount = SumAmounts(orderedRows, cancellationToken);
        string destinationName = $"{request.ExportName}.{formatter.FileExtension}";

        // `await using`은 비동기 자원 소유권을 이 메서드에 묶습니다. return·예외·취소 어느 경로든 DisposeAsync를 기다립니다.
        // 컴파일러는 이 범위를 `try/finally`와 `await session.DisposeAsync()`에 해당하는 형태로 바꿉니다.
        // `?? throw`는 Factory가 null을 반환한 계약 위반에서 즉시 예외를 던지는 null-coalescing throw expression입니다.
        await using ISettlementExportSession session =
            await _sessionFactory.OpenAsync(destinationName, cancellationToken)
            ?? throw new InvalidOperationException("Session Factory가 null 세션을 반환했습니다.");

        if (!string.Equals(session.DestinationName, destinationName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Session Factory가 요청과 다른 목적지 세션을 반환했습니다.");
        }

        await session.WriteLineAsync(formatter.Header, cancellationToken);

        // foreach 전에 ID로 정렬했으므로 Repository 구현이 순서를 바꿔도 같은 입력은 같은 파일을 만듭니다.
        foreach (SettlementEntry entry in orderedRows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string line = formatter.Formatter.FormatRow(entry);

            if (string.IsNullOrEmpty(line) || ContainsLineBreak(line))
            {
                throw new InvalidOperationException("Formatter는 줄바꿈 없는 한 줄을 반환해야 합니다.");
            }

            await session.WriteLineAsync(line, cancellationToken);
        }

        // commit 직전 취소를 관측하면 staged 내용은 DisposeAsync에서 폐기되고 공개 파일은 생기지 않습니다.
        cancellationToken.ThrowIfCancellationRequested();
        await session.CommitAsync(cancellationToken);

        // commit 뒤에는 취소를 다시 검사하지 않습니다. 이미 공개된 성공을 취소 실패로 뒤집으면 호출자가 중복 재시도할 수 있습니다.
        return Result<SettlementExportReceipt>.Success(
            new SettlementExportReceipt(
                destinationName,
                request.BusinessDate,
                request.Format,
                orderedRows.Length,
                totalAmount));
    }

    /// <summary>
    /// Repository Adapter가 계약대로 같은 날짜의 Ready 항목만 반환했는지 확인하고 안정된 ID 순서로 복사합니다.
    /// rows는 Adapter 결과, businessDate는 요청한 영업일, cancellationToken은 복사·검증·정렬 중단 신호입니다.
    /// 반환값은 ID 오름차순의 새 배열이며 null·잘못된 상태·중복 ID는 코드/Adapter 버그이므로 예외를 던집니다.
    /// </summary>
    private static SettlementEntry[] ValidateAndOrderRows(
        IReadOnlyList<SettlementEntry>? rows,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        if (rows is null)
        {
            throw new InvalidOperationException("Repository가 null 목록을 반환했습니다.");
        }

        // IReadOnlyList는 "수정 메서드를 노출하지 않음"일 뿐 불변을 보장하지 않으므로 최초 한 번 새 목록으로 고정합니다.
        // List<T>를 직접 채우면 단 한 번만 열거하면서 큰 결과 복사 중에도 매 항목 취소를 확인할 수 있습니다.
        // `[]` collection expression은 항목이 없는 새 List를 target type에 맞게 만듭니다.
        List<SettlementEntry> snapshot = [];
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (SettlementEntry? entry in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry is null)
            {
                throw new InvalidOperationException("Repository 결과에는 null 항목을 넣을 수 없습니다.");
            }

            if (entry.BusinessDate != businessDate || entry.Status != SettlementStatus.Ready)
            {
                throw new InvalidOperationException("Repository가 요청 날짜의 Ready 항목만 반환한다는 계약을 어겼습니다.");
            }

            if (!seenIds.Add(entry.Id))
            {
                throw new InvalidOperationException($"Repository가 중복 정산 ID를 반환했습니다: {entry.Id}");
            }

            snapshot.Add(entry);
        }

        // OrderBy는 원본 목록을 바꾸지 않고 정렬된 새 시퀀스를 만들고 ToArray가 그 시점의 스냅샷으로 고정합니다.
        // `entry => entry.Id` lambda는 각 정산 항목에서 정렬 기준으로 사용할 ID를 선택하는 짧은 익명 함수입니다.
        cancellationToken.ThrowIfCancellationRequested();
        SettlementEntry[] ordered = snapshot
            .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return ordered;
    }

    /// <summary>
    /// 정렬된 정산 금액을 합하면서 각 항목 사이에 호출자 취소를 확인합니다.
    /// rows는 유효한 정산 스냅샷, cancellationToken은 긴 합계 계산의 중단 신호입니다.
    /// 반환값은 모든 Amount의 decimal 합계이며 범위를 넘으면 OverflowException이 납니다.
    /// </summary>
    private static decimal SumAmounts(
        IReadOnlyList<SettlementEntry> rows,
        CancellationToken cancellationToken)
    {
        decimal total = 0m;

        foreach (SettlementEntry entry in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += entry.Amount;
        }

        return total;
    }

    /// <summary>
    /// Formatter가 제공한 확장자가 경로나 특수 문자를 끼워 넣지 못하는 안전한 값인지 검사합니다.
    /// extension은 점을 제외한 파일 확장자 후보입니다.
    /// 반환값은 1~10자이고 모든 문자가 ASCII 영문자 또는 숫자일 때 true입니다.
    /// </summary>
    private static bool IsSafeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
        {
            return false;
        }

        return extension.All(char.IsAsciiLetterOrDigit);
    }

    /// <summary>
    /// 한 줄 계약을 깨는 캐리지 리턴 또는 라인 피드가 문자열에 있는지 검사합니다.
    /// value는 검사할 문자열입니다.
    /// 반환값은 줄바꿈 문자가 하나라도 있으면 true입니다.
    /// </summary>
    private static bool ContainsLineBreak(string value)
    {
        return value.Contains('\r', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal);
    }

    // 생성 시 검증한 Strategy와 metadata를 한 불변 값으로 묶어 실행 중 getter 변화가 안전성 검사를 우회하지 못하게 합니다.
    // 주 생성자는 formatter(행 변환 구현), fileExtension(안전한 확장자), header(줄바꿈 없는 첫 줄)를 받으며 별도 반환값은 없습니다.
    private sealed record FormatterRegistration(
        ISettlementFormatter Formatter,
        string FileExtension,
        string Header);
}
