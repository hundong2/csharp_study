using EquipmentRentalApi.Domain;

namespace EquipmentRentalApi.Application.Ports;

/// <summary>
/// 장비 분류마다 달라지는 최대 기간과 준비 시간을 교체 가능한 Strategy로 추상화합니다.
/// </summary>
public interface IRentalPolicy
{
    /// <summary>
    /// 이 Strategy가 처리하는 정규화된 장비 분류 이름을 알려 줍니다.
    /// </summary>
    string Category { get; }

    /// <summary>
    /// 현재 시각을 기준으로 대여 구간이 해당 장비 분류 정책을 만족하는지 검사합니다.
    /// </summary>
    /// <param name="window">Domain에서 시작·종료 관계를 이미 확인한 UTC 대여 구간입니다.</param>
    /// <param name="nowUtc">TimeProvider가 제공한 현재 UTC 시각으로, 테스트에서 고정할 수 있습니다.</param>
    /// <returns>정책을 통과하면 같은 구간의 성공 Result, 아니면 이유를 담은 실패 Result를 반환합니다.</returns>
    Result<RentalWindow> Validate(RentalWindow window, DateTimeOffset nowUtc);
}
