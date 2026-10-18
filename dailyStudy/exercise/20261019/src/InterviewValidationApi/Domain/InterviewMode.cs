namespace InterviewValidationApi.Domain;

/// <summary>
/// 면접이 화상으로 진행되는지 실제 회의실에서 진행되는지 제한된 값으로 표현합니다.
/// </summary>
// enum은 허용된 선택지를 이름 있는 정수 값으로 제한해 임의 문자열이 Domain 안으로 퍼지는 일을 막습니다.
public enum InterviewMode
{
    Remote,
    Onsite
}
