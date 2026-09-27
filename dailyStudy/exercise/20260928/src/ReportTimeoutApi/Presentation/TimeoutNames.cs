namespace ReportTimeoutApi.Presentation;

/// <summary>
/// 문자열 오타로 정책 연결이 끊기지 않도록 request timeout 정책 이름을 한곳에 모읍니다.
/// </summary>
public static class TimeoutNames
{
    /// <summary>보고서 생성 endpoint에 적용하는 짧은 제한 시간 정책 이름입니다.</summary>
    // const는 실행 중 바뀌지 않는 컴파일 시간 상수이므로 정책 이름을 모든 호출부에서 동일하게 재사용하게 합니다.
    public const string ReportGeneration = "report-generation";
}
