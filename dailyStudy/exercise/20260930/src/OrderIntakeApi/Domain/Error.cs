namespace OrderIntakeApi.Domain;

/// <summary>
/// 클라이언트가 분기할 안정적인 코드와 사람이 읽을 안전한 설명을 묶은 예상 가능 오류입니다.
/// </summary>
/// <param name="Code">로그 문장이 바뀌어도 유지할 기계 판독용 코드입니다.</param>
/// <param name="Message">비밀번호나 내부 예외 정보가 들어가지 않는 공개 가능한 설명입니다.</param>
// record는 값이 같으면 같은 오류로 비교되는 불변 데이터 모양을 짧게 선언할 때 적합합니다.
public sealed record Error(string Code, string Message);
