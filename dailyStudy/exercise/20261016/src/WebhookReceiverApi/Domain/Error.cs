namespace WebhookReceiverApi.Domain;

/// <summary>
/// 사용자가 입력을 고쳐 다시 요청할 수 있는 예상 실패를 예외 대신 값으로 전달합니다.
/// 예외는 개발자가 고쳐야 할 버그나 복구할 수 없는 장애에 남겨 두고, 이 형식은 HTTP 계층이 안전한 응답으로 바꿀 수 있게 합니다.
/// </summary>
/// <param name="Code">프로그램과 테스트가 안정적으로 분기할 기계 판독용 오류 코드입니다.</param>
/// <param name="Message">비밀값이나 내부 구현을 노출하지 않는 사용자용 설명입니다.</param>
/// <param name="Field">잘못된 입력 필드나 헤더의 이름이며 특정 대상이 없으면 null입니다.</param>
// record는 같은 값을 가진 객체를 값으로 비교하고, 생성 뒤 바뀌지 않게 만드는 불변 자료형입니다.
// string?의 ?는 문자열뿐 아니라 "값 없음"인 null도 허용한다고 nullable 분석기에 알려 줍니다.
public sealed record Error(string Code, string Message, string? Field = null);
