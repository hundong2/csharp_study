namespace AuditEventApi.Domain;

/// <summary>
/// 예외로 프로그램을 중단할 필요가 없는 예상된 실패를 안정적인 코드와 설명으로 표현합니다.
/// </summary>
/// <param name="Code">클라이언트와 테스트가 분기할 때 사용하는, 번역되지 않는 오류 식별자입니다.</param>
/// <param name="Message">초보자와 API 사용자가 문제를 고칠 수 있도록 알려 주는 설명입니다.</param>
/// <param name="Field">오류의 원인이 된 입력 필드 또는 업무 자원의 이름입니다.</param>
// record는 생성 뒤 값을 바꾸지 않는 작은 데이터 묶음을 선언하며, 같은 값끼리 비교하는 기능도 자동으로 만듭니다.
public sealed record Error(string Code, string Message, string Field);
