namespace EquipmentRentalApi.Domain;

/// <summary>
/// 장비를 빌리는 시작·종료 시각을 UTC로 보관하는 불변 값 객체입니다.
/// </summary>
// record는 값 객체의 값 기반 비교를 제공하며, private 생성자와 읽기 전용 속성으로 유효한 상태만 만듭니다.
public sealed record RentalWindow
{
    /// <summary>
    /// 검증을 통과한 UTC 시각만 받아 대여 구간을 만듭니다.
    /// </summary>
    /// <param name="startsAtUtc">대여가 시작되는 UTC 시각입니다.</param>
    /// <param name="endsAtUtc">대여가 끝나는 UTC 시각입니다.</param>
    private RentalWindow(DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc)
    {
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
    }

    /// <summary>
    /// 대여가 시작되는 UTC 시각입니다.
    /// </summary>
    public DateTimeOffset StartsAtUtc { get; }

    /// <summary>
    /// 대여가 끝나는 UTC 시각입니다.
    /// </summary>
    public DateTimeOffset EndsAtUtc { get; }

    /// <summary>
    /// 외부 시각을 UTC로 정규화하고 종료가 시작보다 뒤인지 검사해 대여 구간을 만듭니다.
    /// </summary>
    /// <param name="startsAt">호출자가 보낸 대여 시작 시각이며 어떤 offset도 허용합니다.</param>
    /// <param name="endsAt">호출자가 보낸 대여 종료 시각이며 시작보다 뒤여야 합니다.</param>
    /// <returns>유효하면 UTC 대여 구간, 아니면 수정할 입력 필드를 가진 실패 Result를 반환합니다.</returns>
    public static Result<RentalWindow> Create(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        // var는 오른쪽 호출 결과가 DateTimeOffset임을 compiler가 추론하게 합니다. 형식을 숨기려는 용도가 아닙니다.
        // DateTimeOffset은 같은 순간을 여러 offset으로 표현할 수 있으므로 저장·비교 전에 UTC 하나로 정규화합니다.
        var startsAtUtc = startsAt.ToUniversalTime();
        var endsAtUtc = endsAt.ToUniversalTime();

        if (endsAtUtc <= startsAtUtc)
        {
            return Result<RentalWindow>.Failure(
                new Error(
                    "rental.invalid_window",
                    "종료 시각은 시작 시각보다 뒤여야 합니다.",
                    "endsAt"));
        }

        return Result<RentalWindow>.Success(new RentalWindow(startsAtUtc, endsAtUtc));
    }

    /// <summary>
    /// 현재 구간이 다른 구간과 한 순간이라도 겹치는지 반열린 구간 규칙으로 확인합니다.
    /// </summary>
    /// <param name="other">겹침 여부를 비교할 다른 대여 구간입니다.</param>
    /// <returns>두 구간이 겹치면 true, 한 구간의 종료와 다음 시작만 같으면 false를 반환합니다.</returns>
    public bool Overlaps(RentalWindow other)
    {
        ArgumentNullException.ThrowIfNull(other);
        // [시작, 종료) 반열린 구간을 쓰면 11시에 반납하고 11시에 다음 대여를 시작할 수 있습니다.
        return StartsAtUtc < other.EndsAtUtc && other.StartsAtUtc < EndsAtUtc;
    }
}
