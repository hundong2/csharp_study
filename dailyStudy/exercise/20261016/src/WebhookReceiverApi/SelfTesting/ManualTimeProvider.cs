namespace WebhookReceiverApi.SelfTesting;

/// <summary>
/// 테스트가 운영체제 시계와 실행 속도에 흔들리지 않도록 원하는 UTC 시각을 돌려주는 수동 시계입니다.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    /// <summary>
    /// 테스트가 시작할 고정 UTC 시각을 저장합니다.
    /// </summary>
    /// <param name="utcNow">DateTimeOffset.Offset이 0인 UTC 시각입니다.</param>
    /// <returns>생성자는 시계 객체를 초기화하므로 별도 반환값은 없습니다.</returns>
    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        SetUtcNow(utcNow);
    }

    /// <summary>
    /// HMAC timestamp 검증기가 읽을 현재 UTC 시각을 반환합니다.
    /// </summary>
    /// <returns>마지막으로 설정한 UTC 시각을 반환합니다.</returns>
    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    /// <summary>
    /// 시간창 경계 테스트를 위해 현재 UTC 시각을 원하는 값으로 바꿉니다.
    /// </summary>
    /// <param name="utcNow">새로 사용할 UTC 시각입니다.</param>
    /// <returns>시계를 제자리에서 변경하므로 반환값은 없습니다.</returns>
    public void SetUtcNow(DateTimeOffset utcNow)
    {
        if (utcNow.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("테스트 시각은 UTC여야 합니다.", nameof(utcNow));
        }

        _utcNow = utcNow;
    }
}
