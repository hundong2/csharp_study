namespace WebhookReceiverApi.Application;

/// <summary>
/// HMAC과 timestamp 검증을 통과했다는 사실과 검증 시각을 함께 운반합니다.
/// </summary>
/// <param name="SignedAtUtc">공급자가 timestamp 헤더에 기록한 UTC 시각입니다.</param>
/// <param name="VerifiedAtUtc">수신기가 허용 시간창과 HMAC을 확인한 UTC 시각입니다.</param>
// record는 두 시각으로 이루어진 검증 사실을 생성 뒤 바꾸지 않게 표현합니다.
public sealed record VerifiedWebhook(
    DateTimeOffset SignedAtUtc,
    DateTimeOffset VerifiedAtUtc);
