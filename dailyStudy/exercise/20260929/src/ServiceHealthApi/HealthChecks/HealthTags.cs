namespace ServiceHealthApi.HealthChecks;

/// <summary>
/// 문자열 오타로 서로 다른 probe 묶음이 생기지 않도록 health check tag 이름을 한곳에 모읍니다.
/// </summary>
public static class HealthTags
{
    /// <summary>
    /// 애플리케이션 초기화 완료 여부만 실행할 때 사용하는 tag입니다.
    /// </summary>
    public const string Startup = "startup";

    /// <summary>
    /// 새 요청을 받아도 되는지 판단하는 모든 검사를 실행할 때 사용하는 tag입니다.
    /// </summary>
    public const string Ready = "ready";
}
