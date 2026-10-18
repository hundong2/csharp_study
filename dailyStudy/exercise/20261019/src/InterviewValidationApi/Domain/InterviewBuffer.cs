namespace InterviewValidationApi.Domain;

/// <summary>
/// 면접 전후에 같은 면접관에게 확보해야 하는 준비·정리 시간을 나타냅니다.
/// </summary>
/// <param name="Before">면접 시작 전에 비워 둘 시간입니다.</param>
/// <param name="After">면접 종료 뒤에 비워 둘 시간입니다.</param>
public sealed record InterviewBuffer(TimeSpan Before, TimeSpan After);
