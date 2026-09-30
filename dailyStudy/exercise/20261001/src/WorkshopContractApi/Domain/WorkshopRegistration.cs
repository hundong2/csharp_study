namespace WorkshopContractApi.Domain;

/// <summary>
/// 가격 정책이 선택할 수강생 등급을 표현합니다.
/// </summary>
public enum AttendeeTier
{
    Standard,
    Premium
}
/// <summary>
/// 카탈로그가 제공하는 유효한 워크숍 회차의 불변 정보입니다.
/// </summary>
/// <param name="SessionId">정규화된 회차 식별자입니다.</param>
/// <param name="Title">사람이 읽는 워크숍 제목입니다.</param>
/// <param name="Capacity">예약할 수 있는 마지막 좌석 번호입니다.</param>
/// <param name="BasePriceWon">할인 전 원화 가격입니다.</param>
// positional record는 생성자 매개변수와 init 전용 속성, 값 비교 기능을 한 선언으로 만듭니다.
public sealed record WorkshopSession(
    string SessionId,
    string Title,
    int Capacity,
    int BasePriceWon);

/// <summary>
/// 외부 문자열을 검증하고 정규화한 뒤 Application Service에 전달할 예약 초안입니다.
/// </summary>
public sealed record RegistrationDraft
{
    /// <summary>
    /// 검증 factory만 유효한 초안을 만들 수 있도록 생성자를 숨기고 정규화된 값을 저장합니다.
    /// </summary>
    /// <param name="registrationId">정규화된 예약 식별자입니다.</param>
    /// <param name="sessionId">정규화된 워크숍 회차 식별자입니다.</param>
    /// <param name="attendeeId">정규화된 수강생 식별자입니다.</param>
    /// <param name="seatNumber">검증된 1부터 200 사이 좌석 번호입니다.</param>
    /// <param name="tier">해석이 끝난 수강생 등급입니다.</param>
    private RegistrationDraft(
        string registrationId,
        string sessionId,
        string attendeeId,
        int seatNumber,
        AttendeeTier tier)
    {
        RegistrationId = registrationId;
        SessionId = sessionId;
        AttendeeId = attendeeId;
        SeatNumber = seatNumber;
        Tier = tier;
    }

    /// <summary>정규화된 예약 식별자입니다.</summary>
    public string RegistrationId { get; }

    /// <summary>정규화된 워크숍 회차 식별자입니다.</summary>
    public string SessionId { get; }

    /// <summary>정규화된 수강생 식별자입니다.</summary>
    public string AttendeeId { get; }

    /// <summary>검증된 1부터 200 사이 좌석 번호입니다.</summary>
    public int SeatNumber { get; }

    /// <summary>해석이 끝난 수강생 등급입니다.</summary>
    public AttendeeTier Tier { get; }

    /// <summary>
    /// nullable 외부 입력을 검증·정규화해 유효한 예약 초안 또는 첫 오류를 만듭니다.
    /// </summary>
    /// <param name="registrationId">클라이언트가 다시 조회할 예약 식별자입니다.</param>
    /// <param name="sessionId">신청할 워크숍 회차 식별자입니다.</param>
    /// <param name="attendeeId">수강생을 구분할 식별자입니다.</param>
    /// <param name="seatNumber">예약하려는 좌석 번호입니다.</param>
    /// <param name="attendeeTier">Standard 또는 Premium 등급 문자열입니다.</param>
    /// <returns>모든 필드가 유효하면 RegistrationDraft, 아니면 필드별 Error를 담은 Result를 반환합니다.</returns>
    public static Result<RegistrationDraft> Create(
        string? registrationId,
        string? sessionId,
        string? attendeeId,
        int seatNumber,
        string? attendeeTier)
    {
        var normalizedRegistrationId = RegistrationRules.NormalizeIdentifier(
            registrationId,
            "registration.id.invalid",
            "registrationId");
        if (!normalizedRegistrationId.IsSuccess)
        {
            // !는 실패 branch에서 Error가 null이 아님을 논리로 확인했다고 nullable 분석기에 알립니다.
            return Result<RegistrationDraft>.Failure(normalizedRegistrationId.Error!);
        }

        var normalizedSessionId = RegistrationRules.NormalizeIdentifier(
            sessionId,
            "registration.session_id.invalid",
            "sessionId");
        if (!normalizedSessionId.IsSuccess)
        {
            return Result<RegistrationDraft>.Failure(normalizedSessionId.Error!);
        }

        var normalizedAttendeeId = RegistrationRules.NormalizeIdentifier(
            attendeeId,
            "registration.attendee_id.invalid",
            "attendeeId");
        if (!normalizedAttendeeId.IsSuccess)
        {
            return Result<RegistrationDraft>.Failure(normalizedAttendeeId.Error!);
        }

        // 관계 패턴 <와 >, 논리 패턴 or를 조합해 허용 범위 밖의 두 경우를 한 문장처럼 표현합니다.
        if (seatNumber is < 1 or > 200)
        {
            return Result<RegistrationDraft>.Failure(new Error(
                "registration.seat.out_of_range",
                "seatNumber는 1부터 200 사이여야 합니다.",
                "seatNumber"));
        }

        AttendeeTier tier;
        // Enum.TryParse는 "1" 같은 숫자 문자열도 enum 값으로 허용하므로 공개 이름 두 개를 직접 비교합니다.
        if (string.Equals(attendeeTier?.Trim(), "Standard", StringComparison.OrdinalIgnoreCase))
        {
            tier = AttendeeTier.Standard;
        }
        else if (string.Equals(attendeeTier?.Trim(), "Premium", StringComparison.OrdinalIgnoreCase))
        {
            tier = AttendeeTier.Premium;
        }
        else
        {
            return Result<RegistrationDraft>.Failure(new Error(
                "registration.tier.invalid",
                "attendeeTier는 Standard 또는 Premium이어야 합니다.",
                "attendeeTier"));
        }

        return Result<RegistrationDraft>.Success(new RegistrationDraft(
            normalizedRegistrationId.Value!,
            normalizedSessionId.Value!,
            normalizedAttendeeId.Value!,
            seatNumber,
            tier));
    }
}

/// <summary>
/// 저장 가능한 워크숍 예약의 불변 Domain Model입니다.
/// </summary>
/// <param name="RegistrationId">정규화된 예약 식별자입니다.</param>
/// <param name="SessionId">정규화된 회차 식별자입니다.</param>
/// <param name="SessionTitle">예약 시점의 워크숍 제목 snapshot입니다.</param>
/// <param name="AttendeeId">정규화된 수강생 식별자입니다.</param>
/// <param name="SeatNumber">확정한 좌석 번호입니다.</param>
/// <param name="Tier">가격 계산에 사용한 수강생 등급입니다.</param>
/// <param name="PriceWon">확정한 원화 가격입니다.</param>
/// <param name="ReservedAtUtc">예약이 확정된 UTC 시각입니다.</param>
public sealed record WorkshopRegistration(
    string RegistrationId,
    string SessionId,
    string SessionTitle,
    string AttendeeId,
    int SeatNumber,
    AttendeeTier Tier,
    int PriceWon,
    DateTimeOffset ReservedAtUtc);

/// <summary>
/// HTTP와 저장소가 같은 식별자 정규화 규칙을 재사용하도록 순수 규칙을 모읍니다.
/// </summary>
public static class RegistrationRules
{
    /// <summary>
    /// 식별자를 trim·대문자화하고 길이와 허용 문자를 검증합니다.
    /// </summary>
    /// <param name="rawValue">아직 검증하지 않은 nullable 외부 문자열입니다.</param>
    /// <param name="errorCode">실패했을 때 반환할 필드별 안정 코드입니다.</param>
    /// <param name="fieldName">Problem Details에 표시할 JSON 필드 이름입니다.</param>
    /// <returns>유효하면 정규화 문자열, 아니면 필드별 Error를 담은 Result를 반환합니다.</returns>
    public static Result<string> NormalizeIdentifier(
        string? rawValue,
        string errorCode,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Result<string>.Failure(new Error(
                errorCode,
                $"{fieldName}은(는) 비어 있을 수 없습니다.",
                fieldName));
        }

        var normalized = rawValue.Trim().ToUpperInvariant();
        // All은 모든 문자가 predicate를 통과할 때만 true인 LINQ 연산입니다.
        if (normalized.Length is < 3 or > 40 || !normalized.All(IsAllowedIdentifierCharacter))
        {
            return Result<string>.Failure(new Error(
                errorCode,
                $"{fieldName}은(는) 3~40자의 영문, 숫자, 하이픈만 사용할 수 있습니다.",
                fieldName));
        }

        return Result<string>.Success(normalized);
    }

    /// <summary>
    /// 정규화된 식별자에 허용할 ASCII 영문, 숫자, 하이픈인지 판단합니다.
    /// </summary>
    /// <param name="character">검사할 문자 하나입니다.</param>
    /// <returns>허용 문자이면 true, 아니면 false를 반환합니다.</returns>
    private static bool IsAllowedIdentifierCharacter(char character)
    {
        return character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
    }
}
