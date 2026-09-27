using ReportTimeoutApi.Domain;

namespace ReportTimeoutApi.Application.Ports;

/// <summary>
/// 출력 형식마다 달라지는 문서 생성 알고리즘을 교체할 수 있게 하는 Strategy Port입니다.
/// </summary>
public interface IReportFormatterStrategy
{
    /// <summary>이 Strategy를 선택할 때 사용하는 csv 또는 json 키입니다.</summary>
    string FormatName { get; }

    /// <summary>
    /// 이미 조회가 끝난 청구 항목을 특정 형식의 다운로드 문서로 바꿉니다.
    /// </summary>
    /// <param name="customerId">파일 이름과 문서 식별에 사용할 고객 번호입니다.</param>
    /// <param name="lines">Repository에서 읽은 불변 청구 항목들입니다.</param>
    /// <param name="cancellationToken">큰 문서로 확장해도 포맷을 중단할 수 있게 전달하는 취소 신호입니다.</param>
    /// <returns>파일 이름, MIME 형식, 본문, 항목 수를 담은 ReportDocument를 반환합니다.</returns>
    ReportDocument Format(
        string customerId,
        IReadOnlyList<InvoiceLine> lines,
        CancellationToken cancellationToken);
}
