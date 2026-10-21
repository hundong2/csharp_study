namespace GlobalMeetingApi.Domain;

/// <summary>
/// 호출자가 고칠 수 있는 예상 실패를 안정적인 코드와 안전한 설명으로 나타냅니다.
/// </summary>
/// <param name="Code">클라이언트와 로그가 비교할 수 있는, 문구와 분리된 오류 코드입니다.</param>
/// <param name="Message">초보자도 원인을 이해할 수 있는 공개 가능한 설명입니다.</param>
/// <param name="Field">문제가 생긴 입력 필드이며 특정 필드가 아니면 null입니다.</param>
/// <param name="AllowedValues">모호한 시각처럼 사용자가 골라야 하는 허용값 목록이며 없으면 null입니다.</param>
// record는 값이 같으면 같은 것으로 비교되는 불변 데이터에 알맞습니다.
// string?와 IReadOnlyList<string>?의 물음표는 값이 없을 수 있음을 compiler에게 알려 주는 nullable 문법입니다.
public sealed record Error(
    string Code,
    string Message,
    string? Field = null,
    IReadOnlyList<string>? AllowedValues = null);
