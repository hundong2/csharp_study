using System.Collections.Frozen;

namespace ReportTimeoutApi.Domain;

/// <summary>
/// 보고서 생성에 필요한 값이 모두 검증된 뒤에만 만들어지는 불변 요청입니다.
/// </summary>
// record는 값 중심 객체를 간결하게 선언하고 값 비교를 제공해, 검증된 요청을 불변 데이터처럼 다루게 합니다.
public sealed record ReportRequest
{
    // FrozenSet은 만든 뒤 항목이 바뀌지 않는 집합입니다. Domain 허용 목록과 DI 검증이 같은 사실을 공유합니다.
    // new[]는 첫 원소 "csv"로부터 string[] 타입을 추론하므로 string을 반복해서 쓰지 않습니다.
    private static readonly FrozenSet<string> SupportedFormatNames =
        new[] { "csv", "json" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 검증 팩터리만 객체를 만들 수 있게 하여 잘못된 고객 번호나 지연 시간이 Domain 안으로 들어오지 못하게 합니다.
    /// </summary>
    /// <param name="customerId">공백 제거와 대문자 정규화가 끝난 고객 번호입니다.</param>
    /// <param name="format">csv 또는 json으로 정규화된 출력 형식입니다.</param>
    /// <param name="simulatedLatency">학습용 Repository 지연 시간입니다.</param>
    /// <returns>생성자는 현재 ReportRequest를 초기화하며 별도 값을 반환하지 않습니다.</returns>
    private ReportRequest(string customerId, string format, TimeSpan simulatedLatency)
    {
        CustomerId = customerId;
        Format = format;
        SimulatedLatency = simulatedLatency;
    }

    /// <summary>공백이 제거되고 대문자로 정규화된 고객 번호입니다.</summary>
    public string CustomerId { get; }

    /// <summary>선택된 포맷 Strategy의 키인 csv 또는 json입니다.</summary>
    public string Format { get; }

    /// <summary>timeout을 재현하기 위한 교육 전용 Repository 지연 시간입니다.</summary>
    public TimeSpan SimulatedLatency { get; }

    // =>는 한 식의 결과를 바로 반환하는 식 본문 멤버입니다. IReadOnlySet은 호출자가 목록을 바꾸지 못하게 합니다.
    /// <summary>제품 정책이 명시적으로 허용하는 출력 형식의 읽기 전용 집합입니다.</summary>
    public static IReadOnlySet<string> SupportedFormats => SupportedFormatNames;

    /// <summary>
    /// 원시 HTTP 입력을 검증하고 정규화하여 안전한 보고서 요청을 만듭니다.
    /// </summary>
    /// <param name="customerId">route에서 받은 고객 번호입니다.</param>
    /// <param name="format">query에서 받은 csv/json이며 생략하면 csv입니다.</param>
    /// <param name="simulatedLatencyMilliseconds">교육용 지연 시간이며 0~2,000ms만 허용합니다.</param>
    /// <returns>입력이 올바르면 ReportRequest 성공, 아니면 사용자가 고칠 수 있는 실패 Result를 반환합니다.</returns>
    // string?의 ?는 HTTP에서 값이 없을 수 있음을 nullable 분석기에 알립니다.
    public static Result<ReportRequest> Create(
        string? customerId,
        string? format,
        int simulatedLatencyMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Result<ReportRequest>.Failure(new DomainError(
                "report.customer.required",
                "고객 번호를 입력하세요."));
        }

        // var는 오른쪽 식으로 string 형식을 분명히 알 수 있을 때 타입 이름 반복을 줄입니다.
        var normalizedCustomerId = customerId.Trim().ToUpperInvariant();
        // is < 3 or > 20은 관계 패턴 두 개를 or로 묶어 범위 밖을 검사합니다.
        if (normalizedCustomerId.Length is < 3 or > 20)
        {
            return Result<ReportRequest>.Failure(new DomainError(
                "report.customer.length",
                "고객 번호는 3자 이상 20자 이하여야 합니다."));
        }

        // LINQ All은 모든 문자가 허용 규칙을 만족하는지 한 문장으로 표현합니다.
        // lambda의 =>는 각 문자를 검사하는 짧은 익명 함수를 뜻합니다.
        if (!normalizedCustomerId.All(character =>
                char.IsAsciiLetterOrDigit(character) || character == '-'))
        {
            return Result<ReportRequest>.Failure(new DomainError(
                "report.customer.invalid",
                "고객 번호에는 영문자, 숫자, 하이픈만 사용할 수 있습니다."));
        }

        // 조건 연산자 ?:는 format이 없을 때의 기본값과 있을 때의 정규화 값을 한 식에서 선택합니다.
        var normalizedFormat = string.IsNullOrWhiteSpace(format)
            ? "csv"
            : format.Trim().ToLowerInvariant();

        if (!SupportedFormatNames.Contains(normalizedFormat))
        {
            return Result<ReportRequest>.Failure(new DomainError(
                "report.format.unsupported",
                "format은 csv 또는 json이어야 합니다."));
        }

        // 2_000의 밑줄은 값에 영향을 주지 않고 천 단위를 읽기 쉽게 하는 숫자 구분자입니다.
        if (simulatedLatencyMilliseconds is < 0 or > 2_000)
        {
            return Result<ReportRequest>.Failure(new DomainError(
                "report.delay.out_of_range",
                "simulateMs는 0 이상 2,000 이하여야 합니다."));
        }

        var request = new ReportRequest(
            normalizedCustomerId,
            normalizedFormat,
            TimeSpan.FromMilliseconds(simulatedLatencyMilliseconds));
        return Result<ReportRequest>.Success(request);
    }
}
