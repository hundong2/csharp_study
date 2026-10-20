namespace EquipmentRentalApi.Domain;

/// <summary>
/// 호출자가 고칠 수 있는 예상된 실패를 안정적인 코드, 설명, 입력 필드로 표현합니다.
/// </summary>
/// <param name="Code">로그와 HTTP 응답에서 기계가 비교할 수 있는 변하지 않는 오류 코드입니다.</param>
/// <param name="Message">초보자와 API 사용자가 문제를 이해할 수 있는 안전한 설명입니다.</param>
/// <param name="Field">문제가 생긴 입력 필드이며, 특정 필드가 아니면 null입니다.</param>
// record는 값이 같은 두 오류를 값으로 비교할 수 있는 불변 자료형에 알맞습니다.
// string?는 Field가 특정 입력과 무관할 때 null일 수 있음을 compiler와 독자에게 함께 알립니다.
public sealed record Error(string Code, string Message, string? Field = null);
