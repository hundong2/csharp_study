namespace EquipmentRentalApi.Domain;

/// <summary>
/// 식별자·분류·목적·시각 불변식을 만족하며 Application 정책 검사에 사용되는 불변 Domain Model입니다.
/// </summary>
// record는 값 중심 Domain Model을 표현합니다. private 생성자로 factory 검증을 우회한 생성을 막습니다.
public sealed record RentalReservation
{
    /// <summary>
    /// 검증과 정규화를 마친 값만 받아 완성된 예약을 만듭니다.
    /// </summary>
    /// <param name="reservationId">재시도와 조회에 사용하는 예약 식별자입니다.</param>
    /// <param name="equipmentId">빌릴 장비의 식별자입니다.</param>
    /// <param name="borrowerId">장비를 빌리는 사내 사용자 식별자입니다.</param>
    /// <param name="category">정책 Strategy를 고를 정규화된 장비 분류입니다.</param>
    /// <param name="purpose">대여 목적을 설명하는 짧은 문장입니다.</param>
    /// <param name="window">UTC로 정규화된 대여 시간 구간입니다.</param>
    /// <param name="createdAtUtc">예약이 만들어진 UTC 시각입니다.</param>
    private RentalReservation(
        string reservationId,
        string equipmentId,
        string borrowerId,
        string category,
        string purpose,
        RentalWindow window,
        DateTimeOffset createdAtUtc)
    {
        ReservationId = reservationId;
        EquipmentId = equipmentId;
        BorrowerId = borrowerId;
        Category = category;
        Purpose = purpose;
        Window = window;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// 재시도와 조회에 사용하는 정규화된 예약 식별자입니다.
    /// </summary>
    public string ReservationId { get; }

    /// <summary>
    /// 빌릴 장비의 정규화된 식별자입니다.
    /// </summary>
    public string EquipmentId { get; }

    /// <summary>
    /// 장비를 빌리는 사내 사용자의 정규화된 식별자입니다.
    /// </summary>
    public string BorrowerId { get; }

    /// <summary>
    /// 정책 Strategy를 찾는 소문자 장비 분류 key입니다.
    /// </summary>
    public string Category { get; }

    /// <summary>
    /// 앞뒤 공백을 제거한 대여 목적입니다.
    /// </summary>
    public string Purpose { get; }

    /// <summary>
    /// 시작·종료 관계를 검증하고 UTC로 정규화한 대여 구간입니다.
    /// </summary>
    public RentalWindow Window { get; }

    /// <summary>
    /// TimeProvider에서 얻어 UTC로 정규화한 예약 생성 시각입니다.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// 문자열 식별자와 목적을 검증·정규화한 뒤 완성된 예약 Domain Model을 만듭니다.
    /// </summary>
    /// <param name="reservationId">RES- 접두사와 영문·숫자·하이픈으로 된 예약 ID입니다.</param>
    /// <param name="equipmentId">EQ- 접두사와 영문·숫자·하이픈으로 된 장비 ID입니다.</param>
    /// <param name="borrowerId">USER- 접두사와 영문·숫자·하이픈으로 된 사용자 ID입니다.</param>
    /// <param name="category">등록된 정책 Strategy를 찾을 영문·숫자·하이픈 분류 key입니다.</param>
    /// <param name="purpose">공백을 제거한 뒤 5~120자여야 하는 대여 목적입니다.</param>
    /// <param name="window">먼저 생성·검증된 UTC 대여 구간입니다.</param>
    /// <param name="createdAtUtc">TimeProvider에서 얻은 예약 생성 UTC 시각입니다.</param>
    /// <returns>모든 불변식을 만족하면 예약, 아니면 첫 번째 입력 오류를 담은 Result를 반환합니다.</returns>
    // string?의 ?는 외부 입력이 null일 수 있음을 숨기지 않고 이 factory가 직접 검증하도록 합니다.
    public static Result<RentalReservation> Create(
        string? reservationId,
        string? equipmentId,
        string? borrowerId,
        string? category,
        string? purpose,
        RentalWindow window,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(window);

        // var는 오른쪽 메서드의 string 반환 형식을 compiler가 추론하게 해 정규화 흐름에 집중시킵니다.
        var normalizedReservationId = NormalizeIdentifier(reservationId);
        if (!IsIdentifier(normalizedReservationId, "RES-"))
        {
            return InvalidIdentifier("reservationId", "RES-");
        }

        var normalizedEquipmentId = NormalizeIdentifier(equipmentId);
        if (!IsIdentifier(normalizedEquipmentId, "EQ-"))
        {
            return InvalidIdentifier("equipmentId", "EQ-");
        }

        var normalizedBorrowerId = NormalizeIdentifier(borrowerId);
        if (!IsIdentifier(normalizedBorrowerId, "USER-"))
        {
            return InvalidIdentifier("borrowerId", "USER-");
        }

        // ?.는 category가 null이면 ToLowerInvariant를 호출하지 않고 null을 유지하는 null 조건부 접근입니다.
        // ??는 왼쪽 값이 null일 때만 빈 문자열을 선택해 이후 정책 조회가 예측 가능하도록 합니다.
        var normalizedCategory = category?.Trim().ToLowerInvariant() ?? string.Empty;
        // is < 2 or > 30은 관계 패턴 두 개를 or 패턴으로 묶어 허용 길이 밖인지 읽기 좋게 검사합니다.
        // => lambda는 각 문자를 받아 허용 문자인지 반환합니다. All과 함께 모든 문자를 검사하려고 사용합니다.
        if (normalizedCategory.Length is < 2 or > 30
            || !normalizedCategory.All(
                character => char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            return Result<RentalReservation>.Failure(
                new Error(
                    "rental.invalid_category",
                    "category는 영문·숫자·하이픈으로 된 2~30자여야 합니다.",
                    "category"));
        }

        var normalizedPurpose = purpose?.Trim() ?? string.Empty;
        if (normalizedPurpose.Length is < 5 or > 120)
        {
            return Result<RentalReservation>.Failure(
                new Error(
                    "rental.invalid_purpose",
                    "purpose는 앞뒤 공백을 제거한 뒤 5~120자여야 합니다.",
                    "purpose"));
        }

        return Result<RentalReservation>.Success(
            new RentalReservation(
                normalizedReservationId,
                normalizedEquipmentId,
                normalizedBorrowerId,
                normalizedCategory,
                normalizedPurpose,
                window,
                createdAtUtc.ToUniversalTime()));
    }

    /// <summary>
    /// nullable 식별자의 양끝 공백을 제거하고 대문자로 통일합니다.
    /// </summary>
    /// <param name="value">HTTP나 다른 Adapter에서 들어온 nullable 식별자입니다.</param>
    /// <returns>null이면 빈 문자열, 아니면 정규화된 식별자를 반환합니다.</returns>
    private static string NormalizeIdentifier(string? value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    /// <summary>
    /// 식별자가 요구한 접두사와 허용 문자·길이 규칙을 만족하는지 검사합니다.
    /// </summary>
    /// <param name="value">이미 대문자와 공백 제거가 적용된 식별자입니다.</param>
    /// <param name="prefix">RES-, EQ-, USER- 중 해당 필드가 요구하는 접두사입니다.</param>
    /// <returns>접두사, 길이, 문자 규칙을 모두 만족하면 true를 반환합니다.</returns>
    private static bool IsIdentifier(string value, string prefix)
    {
        // is >= ... and <= ...는 하나의 값을 두 관계 패턴으로 검사하는 C# 패턴 매칭입니다.
        return value.Length is >= 5 and <= 40
            && value.StartsWith(prefix, StringComparison.Ordinal)
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }

    /// <summary>
    /// 반복되는 식별자 오류를 같은 코드·메시지 모양의 실패 Result로 만듭니다.
    /// </summary>
    /// <param name="field">문제가 발생한 JSON 필드 이름입니다.</param>
    /// <param name="prefix">사용자에게 안내할 필수 식별자 접두사입니다.</param>
    /// <returns>식별자 입력 오류를 담은 실패 Result를 반환합니다.</returns>
    private static Result<RentalReservation> InvalidIdentifier(string field, string prefix)
    {
        return Result<RentalReservation>.Failure(
            new Error(
                "rental.invalid_identifier",
                $"{field}는 {prefix}로 시작하고 영문·숫자·하이픈으로 된 5~40자여야 합니다.",
                field));
    }
}
