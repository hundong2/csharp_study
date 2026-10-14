namespace DeploymentSseApi.Domain;

/// <summary>
/// 사용자가 입력을 고쳐 다시 시도할 수 있는 예상 실패를 예외 대신 값으로 전달합니다.
/// 예외는 개발자가 고쳐야 하는 불변식 위반에 남겨 두고, 이 형식은 HTTP 계층이 안전한 오류 응답으로 바꿀 수 있게 합니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Code">Presentation 계층과 테스트가 안정적으로 분기할 기계 판독용 코드입니다.</param>
/// <param name="Message">초보자와 API 사용자가 이해할 수 있는 안전한 설명입니다.</param>
/// <param name="Field">문제가 생긴 입력 필드이며 특정 필드가 없으면 null입니다.</param>
// record는 같은 값을 가진 오류를 값으로 비교하고, 생성된 뒤 내용을 바꾸지 않게 하는 불변 자료형입니다.
// string?의 ?는 문자열뿐 아니라 "값 없음"인 null도 허용한다는 사실을 nullable 분석기에 알립니다.
public sealed record Error(string Code, string Message, string? Field = null);
