namespace InterviewValidationApi.Domain;

/// <summary>
/// 호출자가 복구할 수 있는 예상된 실패를 code, message, field 세 값으로 표현합니다.
/// </summary>
/// <param name="Code">프로그램이 분기할 때 사용하는 안정적인 오류 식별자입니다.</param>
/// <param name="Message">학습자와 API 사용자가 이해할 수 있는 오류 설명입니다.</param>
/// <param name="Field">오류와 가장 가까운 입력 필드 또는 업무 자원 이름입니다.</param>
// record는 생성 뒤 값을 바꾸지 않는 데이터 묶음을 간결하게 선언하고, 값 기준 비교도 제공합니다.
public sealed record Error(string Code, string Message, string Field);
