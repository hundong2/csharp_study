namespace EquipmentRentalApi.Tests.TestDoubles;

/// <summary>
/// 테스트가 컴퓨터 시계와 실행 날짜에 흔들리지 않도록 항상 같은 현재 시각을 돌려주는 Test Double입니다.
/// </summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    /// <summary>
    /// 이후 모든 GetUtcNow 호출에서 반환할 기준 시각을 저장합니다.
    /// </summary>
    /// <param name="utcNow">테스트 시나리오가 현재라고 가정할 시각이며 내부에서는 UTC로 정규화됩니다.</param>
    public FixedTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }

    /// <summary>
    /// 생성할 때 고정한 UTC 시각을 반환합니다.
    /// </summary>
    /// <returns>호출 횟수나 실제 경과 시간과 무관한 같은 DateTimeOffset을 반환합니다.</returns>
    // override는 기반 TimeProvider의 가상 동작을 테스트용 고정 시각 구현으로 교체한다는 뜻입니다.
    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }
}
