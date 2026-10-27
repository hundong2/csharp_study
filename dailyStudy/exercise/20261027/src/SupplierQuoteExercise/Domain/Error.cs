namespace SupplierQuoteExercise.Domain;

/// <summary>
/// 예상 가능한 실패의 안정적인 코드, 초보자용 설명, 분류를 불변 값으로 묶습니다.
/// record는 객체 주소보다 속성 값이 같은지를 비교하므로 테스트와 오류 비교에 알맞습니다.
/// 괄호 안 positional 생성자는 세 파라미터를 속성에 보관하며 생성자 자체는 값을 반환하지 않습니다.
/// </summary>
/// <param name="Code">로그와 분기에 사용할, 문구가 바뀌어도 유지되는 오류 코드입니다.</param>
/// <param name="Message">사람이 원인을 이해할 수 있는 설명입니다.</param>
/// <param name="Kind">업무 실패와 외부 시스템 실패를 구분하는 종류입니다.</param>
public sealed record Error(string Code, string Message, ErrorKind Kind)
{
    /// <summary>
    /// 입력 검증이나 업무 규칙 위반을 나타내는 Error를 만듭니다.
    /// </summary>
    /// <param name="code">안정적인 업무 오류 코드입니다.</param>
    /// <param name="message">학습자가 읽을 수 있는 오류 설명입니다.</param>
    /// <returns>Business 종류로 분류된 새 Error를 반환합니다.</returns>
    public static Error Business(string code, string message)
    {
        return new Error(code, message, ErrorKind.Business);
    }

    /// <summary>
    /// 재고 없음이나 일시 장애처럼 공급업체가 알려 준 예상 가능한 실패를 만듭니다.
    /// </summary>
    /// <param name="code">안정적인 외부 오류 코드입니다.</param>
    /// <param name="message">공급업체 실패를 설명하는 문구입니다.</param>
    /// <returns>External 종류로 분류된 새 Error를 반환합니다.</returns>
    public static Error External(string code, string message)
    {
        return new Error(code, message, ErrorKind.External);
    }
}
