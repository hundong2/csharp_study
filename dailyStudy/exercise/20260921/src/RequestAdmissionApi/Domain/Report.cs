namespace RequestAdmissionApi.Domain;

/// <summary>
/// 생성이 끝난 보고서를 나타내는 불변 record입니다. record를 쓰면 저장소와 HTTP 계층이 같은 값을
/// 안전하게 전달하면서도 값 기반 비교를 할 수 있습니다.
/// </summary>
/// <param name="Id">보고서의 고유 식별자입니다.</param>
/// <param name="Title">검증되고 정리된 제목입니다.</param>
/// <param name="Format">실제로 사용한 렌더링 형식입니다.</param>
/// <param name="Content">렌더러가 만든 본문입니다.</param>
/// <param name="CreatedAtUtc">UTC 기준 생성 시각입니다.</param>
public sealed record Report(
    Guid Id,
    string Title,
    ReportFormat Format,
    string Content,
    DateTimeOffset CreatedAtUtc);
