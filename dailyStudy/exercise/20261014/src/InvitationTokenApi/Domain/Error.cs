namespace InvitationTokenApi.Domain;

/// <summary>
/// 사용자가 고칠 수 있는 예상 실패를 예외 대신 값으로 전달해 호출자가 명시적으로 분기하게 합니다.
/// 반대로 잘못된 내부 구성이나 처리하지 않은 상태처럼 개발자가 고쳐야 할 오류는 예외로 빠르게 드러냅니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Code">HTTP 계층과 테스트가 안정적으로 분기할 오류 코드입니다.</param>
/// <param name="Message">초보자와 API 사용자가 이해할 수 있는 안전한 설명입니다.</param>
/// <param name="Field">문제가 생긴 입력 필드이며, 특정 필드가 없으면 null입니다.</param>
// record는 같은 값을 가진 오류를 값으로 비교하고 생성 뒤 바꾸지 않게 하는 불변 자료형입니다.
// string?의 ?는 Field에 문자열뿐 아니라 "값 없음"인 null도 올 수 있음을 컴파일러에 알립니다.
public sealed record Error(string Code, string Message, string? Field = null);
