namespace WebhookReceiverApi.Infrastructure;

/// <summary>
/// 환경 변수나 안전한 비밀 저장소에서 읽을 웹훅 인증 설정입니다.
/// 운영 secret을 소스 코드나 appsettings.json에 커밋하지 않도록 값 자체는 비워 둡니다.
/// </summary>
public sealed class WebhookSecurityOptions
{
    /// <summary>환경 변수에서는 WebhookSecurity__ 접두사로 표현할 구성 섹션 이름입니다.</summary>
    public const string SectionName = "WebhookSecurity";

    /// <summary>
    /// 오래된 요청과 지나치게 먼 미래 요청을 거절할 고정 허용 시간입니다.
    /// </summary>
    public const int AllowedClockSkewSeconds = 300;

    /// <summary>
    /// 메모리에 한 번에 읽을 수 있는 exact raw body의 고정 최대 크기입니다.
    /// </summary>
    public const int MaxBodyBytes = 16 * 1024;

    /// <summary>
    /// 환경 변수 WebhookSecurity__Secret 등 안전한 구성 공급자에서 받을 공유 키입니다.
    /// </summary>
    // init은 객체를 만들 때만 값을 지정하게 해 실행 중 실수로 secret 참조를 바꾸지 못하게 합니다.
    public string Secret { get; init; } = string.Empty;
}
