// `namespace 이름;`은 파일 전체를 같은 이름 공간에 넣으면서 중첩 중괄호를 줄이는 file-scoped namespace입니다.
namespace SettlementExportExercise;

// enum은 허용된 상태를 이름 있는 값으로 제한합니다. 숫자나 문자열 오타가 Domain으로 퍼지는 일을 줄입니다.
public enum SettlementStatus
{
    Ready,
    Held,
}

// 내보내기 형식을 enum으로 표현하면 Application Service가 알맞은 Formatter Strategy를 안전하게 선택할 수 있습니다.
public enum ExportFormat
{
    Csv,
    PipeDelimited,
}

// positional record는 primary constructor와 init-only 속성을 한 줄에 만들며, 오류 값 비교에도 알맞습니다.
// Code는 분기용 안정 키, Message는 안전한 설명이고 생성자는 이 두 값을 초기화하며 반환값은 없습니다.
// 속성은 직접 대입으로 바꾸지 못하지만 `with`는 원본을 바꾸지 않고 새 record를 만들 수 있습니다.
public sealed record Problem(string Code, string Message);

// Result<T>는 사용자가 고칠 수 있는 입력·업무 실패를 예외와 구분해 호출자가 빠뜨리지 않고 처리하게 합니다.
// `where T : class`는 T를 참조 형식으로 제한하고 nullable 분석을 돕습니다. 실제 null 차단은 Success의 ThrowIfNull이 합니다.
public sealed class Result<T>
    where T : class
{
    // `?`는 이 참조가 상태에 따라 null일 수 있다는 nullable 의도를 컴파일러와 독자에게 알려 줍니다.
    private readonly T? _value;
    private readonly Problem? _problem;

    /// <summary>
    /// 성공값 또는 실패 정보를 한 객체에 보관합니다.
    /// value는 성공 시 값, problem은 실패 시 설명이며 둘 중 정확히 하나만 전달됩니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private Result(T? value, Problem? problem)
    {
        _value = value;
        _problem = problem;
    }

    // `=>`는 짧은 계산 속성을 나타내며 `is null`은 null constant pattern입니다.
    public bool IsSuccess => _problem is null;

    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException("실패 Result에서는 성공값을 읽을 수 없습니다.");
            }

            // `!`는 런타임 동작 없이 nullable 경고만 억제합니다. 성공 팩터리가 값을 넣는 불변식을 근거로 사용합니다.
            return _value!;
        }
    }

    public Problem Problem
    {
        get
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException("성공 Result에서는 실패 정보를 읽을 수 없습니다.");
            }

            return _problem!;
        }
    }

    /// <summary>
    /// null이 아닌 값을 가진 성공 Result를 만듭니다.
    /// value는 호출자에게 돌려줄 유효한 객체입니다.
    /// 반환값은 성공 상태의 Result입니다.
    /// </summary>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, problem: null);
    }

    /// <summary>
    /// 호출자가 분기하여 처리할 수 있는 실패 Result를 만듭니다.
    /// problem은 안정적인 코드와 안전한 설명을 가진 실패 정보입니다.
    /// 반환값은 실패 상태의 Result입니다.
    /// </summary>
    public static Result<T> Failure(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new Result<T>(value: null, problem);
    }
}

// 정산 항목은 생성 뒤 바뀌지 않는 값이므로 get 전용 속성을 가진 immutable record로 모델링합니다.
public sealed record SettlementEntry
{
    public string Id { get; }

    public string MerchantName { get; }

    public decimal Amount { get; }

    public DateOnly BusinessDate { get; }

    public SettlementStatus Status { get; }

    /// <summary>
    /// 검증이 끝난 정산 항목을 초기화합니다.
    /// id는 정산 키, merchantName은 상점 표시명, amount는 지급액, businessDate는 영업일, status는 처리 상태입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private SettlementEntry(
        string id,
        string merchantName,
        decimal amount,
        DateOnly businessDate,
        SettlementStatus status)
    {
        Id = id;
        MerchantName = merchantName;
        Amount = amount;
        BusinessDate = businessDate;
        Status = status;
    }

    /// <summary>
    /// 외부 또는 저장소에서 들어온 값을 검사하여 유효한 정산 항목을 만듭니다.
    /// id와 merchantName은 한 줄 필수 문자열, amount는 0 초과·10억 이하·소수 둘째 자리 이하 지급액입니다.
    /// businessDate는 영업일, status는 현재 상태입니다.
    /// 반환값은 정규화된 SettlementEntry 또는 고칠 수 있는 검증 Problem입니다.
    /// </summary>
    public static Result<SettlementEntry> Create(
        string? id,
        string? merchantName,
        decimal amount,
        DateOnly businessDate,
        SettlementStatus status)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.id_required", "정산 ID는 비워 둘 수 없습니다."));
        }

        string normalizedId = id.Trim();
        if (normalizedId.Length > 40)
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.id_too_long", "정산 ID는 40자 이하여야 합니다."));
        }

        // Any는 시퀀스 항목 중 조건을 만족하는 값이 하나라도 있는지 검사하는 LINQ 메서드입니다.
        // 한 번의 session 쓰기가 한 줄이라는 계약과 맞추기 위해 ID의 줄바꿈·제어 문자도 Domain 경계에서 거부합니다.
        if (normalizedId.Any(char.IsControl))
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.id_control_character", "정산 ID에는 줄바꿈 같은 제어 문자를 넣을 수 없습니다."));
        }

        if (string.IsNullOrWhiteSpace(merchantName))
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.merchant_required", "상점 이름은 비워 둘 수 없습니다."));
        }

        string normalizedMerchantName = merchantName.Trim();
        if (normalizedMerchantName.Length > 80)
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.merchant_too_long", "상점 이름은 80자 이하여야 합니다."));
        }

        // 제어 문자를 거부하면 한 정산 항목이 로그나 내보내기 파일의 여러 물리적 줄로 위장하는 일을 막을 수 있습니다.
        if (normalizedMerchantName.Any(char.IsControl))
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.merchant_control_character", "상점 이름에는 줄바꿈 같은 제어 문자를 넣을 수 없습니다."));
        }

        if (amount <= 0m || amount > 1_000_000_000m)
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.amount_out_of_range", "정산 금액은 0보다 크고 10억 이하여야 합니다."));
        }

        // 파일은 소수 둘째 자리까지 출력하므로 Domain에서도 같은 scale을 강제해 receipt와 파일 금액이 달라지는 일을 막습니다.
        if (decimal.Round(amount, 2, MidpointRounding.ToEven) != amount)
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.amount_scale_invalid", "정산 금액은 소수 둘째 자리까지만 사용할 수 있습니다."));
        }

        if (businessDate == default)
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.date_required", "유효한 영업일이 필요합니다."));
        }

        // 강제 숫자 변환으로 `(SettlementStatus)999`가 들어올 수 있어 정의된 enum 값인지도 확인합니다.
        if (!Enum.IsDefined(status))
        {
            return Result<SettlementEntry>.Failure(
                new Problem("settlement.status_invalid", "지원하지 않는 정산 상태입니다."));
        }

        return Result<SettlementEntry>.Success(
            new SettlementEntry(normalizedId, normalizedMerchantName, amount, businessDate, status));
    }
}

// Application 경계의 nullable 문자열을 검증한 뒤 아래 계층에는 null 불가 요청만 전달합니다.
public sealed record SettlementExportRequest
{
    // Windows 장치 이름은 일반 파일처럼 보여도 실제 Adapter에서 만들 수 없으므로 플랫폼 중립 이름 정책으로 제외합니다.
    // target-typed `new(...)`는 왼쪽 HashSet 형식에서 생성할 형식을 추론하고 comparer는 대소문자를 무시합니다.
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    };

    // C# 14의 `field`는 직접 `_exportName` 필드를 선언하지 않아도 컴파일러가 만든 backing field에 접근하게 합니다.
    // private init에서 한 번 더 Trim해 생성 이후 바뀌지 않는 정규화된 이름이라는 불변식을 속성 경계에 남깁니다.
    public string ExportName
    {
        get;
        private init => field = value.Trim();
    }

    public DateOnly BusinessDate { get; }

    public ExportFormat Format { get; }

    /// <summary>
    /// 검증이 끝난 내보내기 요청을 초기화합니다.
    /// exportName은 확장자를 제외한 안전한 파일 이름, businessDate는 대상 영업일, format은 출력 형식입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private SettlementExportRequest(string exportName, DateOnly businessDate, ExportFormat format)
    {
        ExportName = exportName;
        BusinessDate = businessDate;
        Format = format;
    }

    /// <summary>
    /// 사용자 입력을 검사하여 안전한 내보내기 요청을 만듭니다.
    /// exportName은 경로와 확장자를 뺀 이름, businessDate는 조회할 영업일, format은 CSV 또는 파이프 형식입니다.
    /// 반환값은 SettlementExportRequest 또는 사용자가 수정할 수 있는 검증 Problem입니다.
    /// </summary>
    public static Result<SettlementExportRequest> Create(
        string? exportName,
        DateOnly businessDate,
        ExportFormat format)
    {
        if (string.IsNullOrWhiteSpace(exportName))
        {
            return Result<SettlementExportRequest>.Failure(
                new Problem("export.name_required", "내보내기 이름은 비워 둘 수 없습니다."));
        }

        string normalizedName = exportName.Trim();
        if (normalizedName.Length > 50)
        {
            return Result<SettlementExportRequest>.Failure(
                new Problem("export.name_too_long", "내보내기 이름은 50자 이하여야 합니다."));
        }

        // 경로 구분자나 `..`를 받을 여지를 없애고, Adapter에는 안전한 leaf name만 전달합니다.
        // `is not` 뒤의 or pattern은 여러 허용 문자를 하나의 읽기 쉬운 패턴으로 검사합니다.
        foreach (char character in normalizedName)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            {
                return Result<SettlementExportRequest>.Failure(
                    new Problem("export.name_invalid", "내보내기 이름에는 영문자, 숫자, 하이픈, 밑줄만 사용할 수 있습니다."));
            }
        }

        if (ReservedDeviceNames.Contains(normalizedName))
        {
            return Result<SettlementExportRequest>.Failure(
                new Problem("export.name_reserved", "운영체제가 장치 이름으로 예약한 내보내기 이름은 사용할 수 없습니다."));
        }

        if (businessDate == default)
        {
            return Result<SettlementExportRequest>.Failure(
                new Problem("export.date_required", "유효한 영업일이 필요합니다."));
        }

        if (!Enum.IsDefined(format))
        {
            return Result<SettlementExportRequest>.Failure(
                new Problem("export.format_invalid", "지원하지 않는 내보내기 형식입니다."));
        }

        return Result<SettlementExportRequest>.Success(
            new SettlementExportRequest(normalizedName, businessDate, format));
    }
}
