namespace InterviewValidationApi.SelfTesting;

/// <summary>
/// 자체 테스트가 실제 시계에 흔들리지 않도록 원하는 UTC 시각을 돌려주는 학습용 TimeProvider입니다.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    /// <summary>
    /// 테스트에서 사용할 최초 UTC 시각을 저장합니다.
    /// </summary>
    /// <param name="utcNow">UTC의 0 오프셋인 최초 시각입니다.</param>
    /// <returns>생성자이므로 값을 반환하지 않고 시계를 초기화합니다.</returns>
    public ManualTimeProvider(DateTimeOffset utcNow)
    {
        if (utcNow.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("수동 시계는 UTC 시각만 받을 수 있습니다.", nameof(utcNow));
        }

        _utcNow = utcNow;
    }

    /// <summary>
    /// 저장된 고정 UTC 시각을 반환합니다.
    /// </summary>
    /// <returns>현재 테스트 시각을 반환합니다.</returns>
    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>
    /// 테스트 시계를 지정한 시간만큼 앞으로 이동합니다.
    /// </summary>
    /// <param name="amount">0 이상인 이동 시간입니다.</param>
    /// <returns>상태를 제자리에서 변경하므로 반환값은 없습니다.</returns>
    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "시간은 뒤로 이동할 수 없습니다.");
        }

        _utcNow = _utcNow.Add(amount);
    }
}
