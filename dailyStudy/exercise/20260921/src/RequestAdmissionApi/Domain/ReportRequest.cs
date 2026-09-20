namespace RequestAdmissionApi.Domain;

/// <summary>
/// 보고서를 어떤 방식으로 표현할지 나타냅니다. enum은 허용 가능한 값을 제한하여 잘못된 문자열이
/// 도메인 안쪽으로 흘러드는 일을 막습니다.
/// </summary>
public enum ReportFormat
{
    PlainText,
}

/// <summary>
/// 아직 검증되지 않은 한 행의 입력입니다. record는 값 자체를 표현하고 생성 뒤 바꾸지 않는 불변 데이터에 적합합니다.
/// <c>string?</c>의 물음표는 값이 없을 수 있음을 컴파일러와 독자에게 알려 주는 Nullable 표기입니다.
/// </summary>
/// <param name="Label">행을 구분하는 이름이며, 아직 null 또는 공백일 수 있습니다.</param>
/// <param name="Value">보고서에 표시할 0 이상의 값입니다.</param>
public sealed record ReportRowDraft(string? Label, decimal Value);

/// <summary>
/// 검증을 마친 한 행입니다. 위치 기반 record를 사용해 값 동등성과 불변 init 속성을 간결하게 얻습니다.
/// </summary>
/// <param name="Label">공백을 제거했고 비어 있지 않은 행 이름입니다.</param>
/// <param name="Value">검증된 0 이상의 값입니다.</param>
public sealed record ReportRow(string Label, decimal Value);

/// <summary>
/// 비싼 보고서 생성 작업에 필요한 검증 완료 입력을 나타냅니다.
/// 생성자를 감추고 팩터리만 열어 두어 유효하지 않은 객체가 도메인에 존재하지 못하게 합니다.
/// </summary>
public sealed record ReportRequest
{
    /// <summary>
    /// 검증된 값으로 보고서 요청을 만듭니다.
    /// <paramref name="title"/>은 정리된 제목, <paramref name="format"/>은 지원 형식,
    /// <paramref name="rows"/>는 방어적으로 복사된 행들이며 반환값은 완전히 유효한 요청입니다.
    /// </summary>
    private ReportRequest(string title, ReportFormat format, IReadOnlyList<ReportRow> rows)
    {
        Title = title;
        Format = format;
        Rows = rows;
    }

    public string Title { get; }

    public ReportFormat Format { get; }

    public IReadOnlyList<ReportRow> Rows { get; }

    /// <summary>
    /// 외부에서 받은 제목, 형식, 행을 한 번에 검증하고 정규화합니다.
    /// <paramref name="title"/>과 <paramref name="format"/>은 사용자 문자열이고,
    /// <paramref name="rowDrafts"/>는 미검증 행 목록이며, 성공 시 요청을 담고 실패 시 모든 오류를 담은 결과를 반환합니다.
    /// </summary>
    public static ReportRequestValidationResult Create(
        string? title,
        string? format,
        IEnumerable<ReportRowDraft?>? rowDrafts)
    {
        // var는 오른쪽 식으로 타입이 명확할 때 중복을 줄이는 문법입니다. 실제 타입은 List<string>입니다.
        var errors = new List<string>();
        var normalizedTitle = title?.Trim() ?? string.Empty;

        // `is 0 or > 100`은 패턴 매칭으로, 두 잘못된 길이 조건을 읽기 쉽게 묶습니다.
        if (normalizedTitle.Length is 0 or > 100)
        {
            errors.Add("title은 공백이 아니어야 하며 100자 이하여야 합니다.");
        }

        if (normalizedTitle.Any(char.IsControl))
        {
            errors.Add("title에는 줄바꿈 같은 제어 문자를 넣을 수 없습니다.");
        }

        var parsedFormat = ParseFormat(format);
        if (parsedFormat is null)
        {
            errors.Add("format은 text여야 합니다.");
        }

        // null 병합 연산자 ??는 왼쪽이 null일 때 안전한 빈 배열을 대신 사용하게 합니다.
        var drafts = rowDrafts?.ToArray() ?? Array.Empty<ReportRowDraft?>();
        if (drafts.Length is 0 or > 100)
        {
            errors.Add("rows는 1개 이상 100개 이하여야 합니다.");
        }

        var validatedRows = new List<ReportRow>(drafts.Length);
        for (var index = 0; index < drafts.Length; index++)
        {
            var draft = drafts[index];
            if (draft is null)
            {
                errors.Add($"rows[{index}]는 null일 수 없습니다.");
                continue;
            }

            var normalizedLabel = draft.Label?.Trim() ?? string.Empty;
            var rowIsValid = true;

            if (normalizedLabel.Length is 0 or > 60)
            {
                errors.Add($"rows[{index}].label은 공백이 아니어야 하며 60자 이하여야 합니다.");
                rowIsValid = false;
            }

            if (normalizedLabel.Any(char.IsControl))
            {
                errors.Add($"rows[{index}].label에는 줄바꿈 같은 제어 문자를 넣을 수 없습니다.");
                rowIsValid = false;
            }

            if (draft.Value < 0)
            {
                errors.Add($"rows[{index}].value는 0 이상이어야 합니다.");
                rowIsValid = false;
            }

            if (rowIsValid)
            {
                validatedRows.Add(new ReportRow(normalizedLabel, draft.Value));
            }
        }

        if (errors.Count > 0 || parsedFormat is null)
        {
            return ReportRequestValidationResult.Failure(errors);
        }

        // ToArray와 Array.AsReadOnly로 호출자가 원본 컬렉션을 바꿔도 도메인 상태가 변하지 않게 방어적으로 복사합니다.
        var immutableRows = Array.AsReadOnly(validatedRows.ToArray());
        return ReportRequestValidationResult.Success(
            new ReportRequest(normalizedTitle, parsedFormat.Value, immutableRows));
    }

    /// <summary>
    /// HTTP에서 받은 형식 문자열을 제한된 도메인 값으로 바꿉니다.
    /// <paramref name="format"/>은 null일 수 있는 원문이며, 지원하면 형식을 반환하고 지원하지 않으면 null을 반환합니다.
    /// </summary>
    private static ReportFormat? ParseFormat(string? format)
    {
        return string.Equals(format?.Trim(), "text", StringComparison.OrdinalIgnoreCase)
            ? ReportFormat.PlainText
            : null;
    }
}

/// <summary>
/// 도메인 팩터리의 성공 값 또는 예상 가능한 검증 오류를 운반합니다.
/// 잘못된 사용자 입력은 예외적인 시스템 고장이 아니므로 예외 대신 명시적인 결과로 표현합니다.
/// </summary>
public sealed record ReportRequestValidationResult
{
    /// <summary>
    /// 성공 또는 실패 상태를 내부적으로 만듭니다.
    /// <paramref name="request"/>는 성공 값, <paramref name="errors"/>는 실패 이유이며 생성된 상태 객체를 반환합니다.
    /// </summary>
    private ReportRequestValidationResult(ReportRequest? request, IReadOnlyList<string> errors)
    {
        Request = request;
        Errors = errors;
    }

    // =>는 오른쪽 식의 결과를 바로 반환하는 expression-bodied 속성 문법입니다.
    public bool IsSuccess => Request is not null;

    public ReportRequest? Request { get; }

    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// 검증된 요청을 성공 결과로 감쌉니다.
    /// <paramref name="request"/>는 유효한 도메인 객체이며 오류가 없는 성공 결과를 반환합니다.
    /// </summary>
    public static ReportRequestValidationResult Success(ReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ReportRequestValidationResult(request, Array.Empty<string>());
    }

    /// <summary>
    /// 검증 오류들을 실패 결과로 감쌉니다.
    /// <paramref name="errors"/>는 사용자에게 설명할 오류들이며 요청 값이 없는 실패 결과를 반환합니다.
    /// </summary>
    public static ReportRequestValidationResult Failure(IEnumerable<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new ReportRequestValidationResult(null, Array.AsReadOnly(errors.ToArray()));
    }
}
