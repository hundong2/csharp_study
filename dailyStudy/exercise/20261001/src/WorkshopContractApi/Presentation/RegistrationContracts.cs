using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using WorkshopContractApi.Domain;

namespace WorkshopContractApi.Presentation;

/// <summary>
/// POST /registrations의 JSON 요청 계약입니다. OpenAPI에는 필수·길이·문자·범위 제약을 표시하고 Domain에서도 다시 검증합니다.
/// </summary>
/// <param name="RegistrationId">3~40자의 영문·숫자·하이픈 예약 식별자입니다.</param>
/// <param name="SessionId">CSHARP-101 또는 DOTNET-ARCH 같은 워크숍 회차 ID입니다.</param>
/// <param name="AttendeeId">수강생을 구분할 공개 식별자입니다.</param>
/// <param name="SeatNumber">회차 안에서 예약할 좌석 번호입니다.</param>
/// <param name="AttendeeTier">Standard 또는 Premium 등급입니다.</param>
// record는 생성자 값으로 비교되는 전달용 데이터를 간결하게 표현하고, positional 선언은 속성도 함께 만듭니다.
// property:는 primary constructor 매개변수가 아니라 생성되는 record 속성에 validation metadata를 붙입니다.
// @"..."는 백슬래시를 escape하지 않고 그대로 쓰는 verbatim string이며 정규식의 \s를 읽기 쉽게 합니다.
public sealed record CreateRegistrationRequest(
    [property: Description("앞뒤 공백 제거 후 3~40자의 예약 식별자")]
    [property: Required]
    [property: RegularExpression(@"^\s*[A-Za-z0-9-]{3,40}\s*$")]
    string RegistrationId,
    [property: Description("앞뒤 공백 제거 후 3~40자의 워크숍 회차 ID")]
    [property: Required]
    [property: RegularExpression(@"^\s*[A-Za-z0-9-]{3,40}\s*$")]
    string SessionId,
    [property: Description("앞뒤 공백 제거 후 3~40자의 수강생 공개 식별자")]
    [property: Required]
    [property: RegularExpression(@"^\s*[A-Za-z0-9-]{3,40}\s*$")]
    string AttendeeId,
    [property: Description("회차 안의 좌석 번호")]
    [property: Range(1, 200)]
    int SeatNumber,
    [property: Description("앞뒤 공백과 대소문자를 무시한 Standard 또는 Premium")]
    [property: Required]
    [property: RegularExpression(@"^\s*(?:[Ss][Tt][Aa][Nn][Dd][Aa][Rr][Dd]|[Pp][Rr][Ee][Mm][Ii][Uu][Mm])\s*$")]
    string AttendeeTier);

/// <summary>
/// 201과 200 응답에 공통으로 사용하는 공개 예약 계약입니다.
/// </summary>
/// <param name="RegistrationId">정규화된 예약 식별자입니다.</param>
/// <param name="SessionId">정규화된 워크숍 회차 식별자입니다.</param>
/// <param name="SessionTitle">예약 시점 워크숍 제목입니다.</param>
/// <param name="AttendeeId">정규화된 수강생 식별자입니다.</param>
/// <param name="SeatNumber">확정한 좌석 번호입니다.</param>
/// <param name="AttendeeTier">가격 정책이 사용한 수강생 등급입니다.</param>
/// <param name="PriceWon">확정한 원화 가격입니다.</param>
/// <param name="ReservedAtUtc">예약 확정 UTC 시각입니다.</param>
public sealed record RegistrationResponse(
    string RegistrationId,
    string SessionId,
    string SessionTitle,
    string AttendeeId,
    int SeatNumber,
    string AttendeeTier,
    int PriceWon,
    DateTimeOffset ReservedAtUtc)
{
    /// <summary>
    /// 내부 Domain Model을 외부에 공개할 HTTP 응답 계약으로 복사합니다.
    /// </summary>
    /// <param name="registration">Application Service가 반환한 유효한 예약입니다.</param>
    /// <returns>Domain 구현 세부 정보와 분리된 RegistrationResponse를 반환합니다.</returns>
    public static RegistrationResponse FromDomain(WorkshopRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        return new RegistrationResponse(
            registration.RegistrationId,
            registration.SessionId,
            registration.SessionTitle,
            registration.AttendeeId,
            registration.SeatNumber,
            registration.Tier.ToString(),
            registration.PriceWon,
            registration.ReservedAtUtc);
    }
}

/// <summary>
/// OpenAPI에 안정 오류 확장까지 표시하기 위한 application/problem+json 응답 schema입니다.
/// </summary>
/// <param name="Type">HTTP 오류 의미를 설명하는 URI입니다.</param>
/// <param name="Title">사람이 읽는 짧은 오류 제목입니다.</param>
/// <param name="Status">HTTP 응답과 같은 숫자 상태 코드입니다.</param>
/// <param name="Detail">내부 정보를 노출하지 않는 안전한 행동 설명입니다.</param>
/// <param name="Instance">오류가 발생한 요청 경로입니다.</param>
/// <param name="Code">클라이언트가 분기할 안정적인 기계용 코드입니다.</param>
/// <param name="Field">관련 입력 필드 또는 요청 전체를 뜻하는 request입니다.</param>
/// <param name="TraceId">서버 로그와 요청을 연결할 진단 식별자이며 업무 분기에는 사용하지 않습니다.</param>
// 실제 runtime은 표준 ProblemDetails의 Extensions를 사용하고, 이 record는 그 JSON 모양을 문서에 정확히 알립니다.
public sealed record ApiProblemResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    string Code,
    string Field,
    string TraceId);
