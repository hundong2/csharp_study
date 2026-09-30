namespace WorkshopContractApi.Domain;

/// <summary>
/// 호출자가 예상하고 분기할 수 있는 실패의 안정 코드, 안전한 설명, 관련 입력 필드를 묶습니다.
/// </summary>
/// <param name="Code">로그 문구가 바뀌어도 클라이언트가 분기할 안정적인 기계용 코드입니다.</param>
/// <param name="Message">초보자와 API 사용자가 이해할 수 있는 안전한 설명입니다.</param>
/// <param name="Field">특정 입력이 원인이면 그 JSON 필드 이름이고, 전체 요청 오류면 null입니다.</param>
// record는 값이 같으면 같은 오류로 비교할 수 있는 불변 데이터 표현에 적합합니다.
// string?의 ?는 Field가 없을 수도 있다는 사실을 compile time nullable 검사에 포함합니다.
public sealed record Error(string Code, string Message, string? Field = null);
