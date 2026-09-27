namespace ReportTimeoutApi.Domain;

/// <summary>
/// 저장소에서 읽은 청구 한 줄을 표현하는 불변 Domain Model입니다.
/// record는 값이 같은지 비교하기 쉽고 생성 후 속성을 바꾸지 못하게 해 테스트를 단순하게 합니다.
/// </summary>
/// <param name="InvoiceNumber">사용자에게 보여 줄 청구서 번호입니다.</param>
/// <param name="Description">청구 항목의 설명입니다.</param>
/// <param name="Amount">통화 단위를 생략한 실습용 금액입니다.</param>
/// <remarks>괄호의 세 positional 파라미터가 생성자와 읽기 전용 속성을 만들며, 생성자는 별도 값을 반환하지 않습니다.</remarks>
public sealed record InvoiceLine(string InvoiceNumber, string Description, decimal Amount);

/// <summary>
/// Strategy가 만든 다운로드 문서와 HTTP 메타데이터를 함께 전달하는 불변 값입니다.
/// </summary>
/// <param name="FileName">Content-Disposition 헤더에 사용할 안전한 파일 이름입니다.</param>
/// <param name="ContentType">클라이언트가 본문 형식을 해석할 MIME 형식입니다.</param>
/// <param name="Content">UTF-8로 인코딩하기 전의 문서 문자열입니다.</param>
/// <param name="LineCount">문서에 포함된 청구 항목 개수입니다.</param>
/// <remarks>괄호의 네 positional 파라미터가 생성자와 읽기 전용 속성을 만들며, 생성자는 별도 값을 반환하지 않습니다.</remarks>
public sealed record ReportDocument(
    string FileName,
    string ContentType,
    string Content,
    int LineCount);
