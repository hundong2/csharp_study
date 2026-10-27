namespace SupplierQuoteExercise.Domain;

/// <summary>
/// 한 공급업체 Task가 완료됐을 때의 공급업체 ID와 성공/실패 결과를 묶습니다.
/// 완료된 순서대로 이 값을 모으면 진행 상황과 부분 실패를 잃지 않습니다.
/// 괄호 안 positional 생성자는 두 파라미터를 속성에 보관하며 생성자 자체는 값을 반환하지 않습니다.
/// </summary>
/// <param name="SupplierId">완료한 gateway의 공급업체 ID입니다.</param>
/// <param name="Outcome">성공 견적 또는 예상 가능한 공급업체 실패입니다.</param>
public sealed record QuoteAttempt(string SupplierId, Result<SupplierQuote> Outcome);
