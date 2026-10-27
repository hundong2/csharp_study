namespace SupplierQuoteExercise.Domain;

/// <summary>
/// 실패가 업무상 예상 가능한 거절인지 외부 공급업체의 예상 가능한 실패인지 구분합니다.
/// enum은 허용되는 값의 목록을 이름으로 제한해 문자열 오타를 줄이는 문법입니다.
/// </summary>
public enum ErrorKind
{
    /// <summary>요청 값이나 업무 규칙이 올바르지 않은 실패입니다.</summary>
    Business,

    /// <summary>공급업체가 재고 없음이나 일시 장애를 정상 응답으로 알려 준 실패입니다.</summary>
    External
}
