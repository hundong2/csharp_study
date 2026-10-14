namespace InvitationTokenApi.SelfTesting;

/// <summary>
/// 테스트가 실제 시계를 기다리지 않고 현재 UTC 시각을 직접 이동하게 하는 TimeProvider입니다.
/// </summary>
public sealed class AdjustableTimeProvider : TimeProvider
{
    // new()는 왼쪽 object 선언에서 생성할 형식을 추론하는 target-typed new 문법입니다.
    private readonly object _gate = new();
    private DateTimeOffset _utcNow;

    /// <summary>
    /// 테스트의 시작 UTC 시각을 저장합니다.
    /// </summary>
    /// <param name="utcNow">첫 GetUtcNow 호출에서 돌려줄 결정적인 UTC 시각입니다.</param>
    /// <returns>생성자는 테스트 시계를 초기화하므로 별도 반환값은 없습니다.</returns>
    public AdjustableTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }

    /// <summary>
    /// 현재 테스트 UTC 시각의 일관된 snapshot을 읽습니다.
    /// </summary>
    /// <returns>생성자나 Advance가 정한 UTC 시각을 반환합니다.</returns>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _utcNow;
        }
    }

    /// <summary>
    /// sleep 없이 테스트 시계를 지정 기간만큼 앞으로 이동합니다.
    /// </summary>
    /// <param name="amount">현재 시각에 더할 0보다 큰 기간입니다.</param>
    /// <returns>시계를 제자리에서 바꾸므로 반환값은 없습니다.</returns>
    public void Advance(TimeSpan amount)
    {
        if (amount <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "앞으로 이동할 기간은 0보다 커야 합니다.");
        }

        lock (_gate)
        {
            _utcNow = _utcNow.Add(amount);
        }
    }
}
