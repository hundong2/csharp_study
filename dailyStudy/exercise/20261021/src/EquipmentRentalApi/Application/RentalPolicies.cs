using EquipmentRentalApi.Application.Ports;
using EquipmentRentalApi.Domain;

namespace EquipmentRentalApi.Application;

/// <summary>
/// 노트북은 가까운 시각부터 최대 7일까지 대여할 수 있다는 정책 Strategy입니다.
/// </summary>
public sealed class LaptopRentalPolicy : IRentalPolicy
{
    /// <summary>
    /// DI로 주입된 여러 정책 중 이 구현을 찾는 안정적인 분류 key입니다.
    /// </summary>
    // => 식 본문 멤버는 짧은 반환 전용 속성을 한 줄로 표현합니다. 고정 key라서 별도 상태가 필요 없습니다.
    public string Category => "laptop";

    /// <summary>
    /// 노트북 대여가 과거에 시작하지 않고 7일을 넘지 않는지 검사합니다.
    /// </summary>
    /// <param name="window">검사할 UTC 대여 구간입니다.</param>
    /// <param name="nowUtc">테스트에서 바꿀 수 있는 현재 UTC 시각입니다.</param>
    /// <returns>통과하면 대여 구간, 실패하면 start 또는 endsAt 오류를 담은 Result를 반환합니다.</returns>
    public Result<RentalWindow> Validate(RentalWindow window, DateTimeOffset nowUtc)
    {
        return RentalPolicyRules.Validate(window, nowUtc, TimeSpan.Zero, TimeSpan.FromDays(7), Category);
    }
}

/// <summary>
/// 카메라는 점검 준비 2시간이 필요하고 최대 2일까지 대여할 수 있다는 정책 Strategy입니다.
/// </summary>
public sealed class CameraRentalPolicy : IRentalPolicy
{
    /// <summary>
    /// DI로 주입된 여러 정책 중 이 구현을 찾는 안정적인 분류 key입니다.
    /// </summary>
    public string Category => "camera";

    /// <summary>
    /// 카메라 대여가 2시간 준비 시간과 최대 2일 제한을 만족하는지 검사합니다.
    /// </summary>
    /// <param name="window">검사할 UTC 대여 구간입니다.</param>
    /// <param name="nowUtc">테스트에서 바꿀 수 있는 현재 UTC 시각입니다.</param>
    /// <returns>통과하면 대여 구간, 실패하면 start 또는 endsAt 오류를 담은 Result를 반환합니다.</returns>
    public Result<RentalWindow> Validate(RentalWindow window, DateTimeOffset nowUtc)
    {
        return RentalPolicyRules.Validate(
            window,
            nowUtc,
            TimeSpan.FromHours(2),
            TimeSpan.FromDays(2),
            Category);
    }
}

/// <summary>
/// 두 Strategy가 공유하는 시각 경계 검증을 한곳에 모아 규칙 문구의 불일치를 막습니다.
/// </summary>
internal static class RentalPolicyRules
{
    /// <summary>
    /// 최소 준비 시간과 최대 대여 기간을 순서대로 검사합니다.
    /// </summary>
    /// <param name="window">검사할 UTC 대여 구간입니다.</param>
    /// <param name="nowUtc">정책 판단의 기준이 되는 현재 UTC 시각입니다.</param>
    /// <param name="minimumLeadTime">현재부터 시작까지 확보해야 하는 최소 준비 시간입니다.</param>
    /// <param name="maximumDuration">한 번에 빌릴 수 있는 최대 기간입니다.</param>
    /// <param name="category">오류 설명에 넣을 장비 분류입니다.</param>
    /// <returns>모든 경계를 통과하면 같은 구간, 아니면 호출자가 고칠 수 있는 오류 Result를 반환합니다.</returns>
    internal static Result<RentalWindow> Validate(
        RentalWindow window,
        DateTimeOffset nowUtc,
        TimeSpan minimumLeadTime,
        TimeSpan maximumDuration,
        string category)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (window.StartsAtUtc < nowUtc.ToUniversalTime() + minimumLeadTime)
        {
            return Result<RentalWindow>.Failure(
                new Error(
                    "rental.insufficient_lead_time",
                    $"{category} 대여는 현재부터 최소 {minimumLeadTime.TotalHours:0}시간 뒤에 시작해야 합니다.",
                    "startsAt"));
        }

        if (window.EndsAtUtc - window.StartsAtUtc > maximumDuration)
        {
            return Result<RentalWindow>.Failure(
                new Error(
                    "rental.duration_exceeded",
                    $"{category} 대여 기간은 최대 {maximumDuration.TotalDays:0}일입니다.",
                    "endsAt"));
        }

        return Result<RentalWindow>.Success(window);
    }
}
