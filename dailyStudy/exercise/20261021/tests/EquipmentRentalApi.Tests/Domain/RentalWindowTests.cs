using EquipmentRentalApi.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentRentalApi.Tests.Domain;

/// <summary>
/// 외부 자원 없이 빠르게 실행되는 RentalWindow 값 객체 단위 테스트입니다.
/// </summary>
[TestClass]
public sealed class RentalWindowTests
{
    /// <summary>
    /// 서로 다른 offset으로 표현한 시각이 Domain 안에서 UTC로 정규화되는지 검증합니다.
    /// </summary>
    /// <returns>동기 검증이므로 반환값이 없습니다.</returns>
    [TestMethod]
    public void Create_WithOffsets_NormalizesToUtc()
    {
        // Arrange: +09:00 시각 두 개를 준비합니다.
        // var는 오른쪽 생성식에서 DateTimeOffset 형식을 compiler가 추론하게 합니다.
        var startsAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.FromHours(9));
        var endsAt = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.FromHours(9));

        // Act: 검증할 한 동작만 실행합니다.
        var result = RentalWindow.Create(startsAt, endsAt);

        // Assert: 성공 여부와 공개 결과를 함께 확인해 실패 원인을 좁힙니다.
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(TimeSpan.Zero, result.Value.StartsAtUtc.Offset);
        Assert.AreEqual(new DateTimeOffset(2026, 10, 21, 1, 0, 0, TimeSpan.Zero), result.Value.StartsAtUtc);
    }

    /// <summary>
    /// 종료가 시작과 같거나 앞선 두 경계가 모두 같은 오류로 거절되는지 데이터 기반으로 검증합니다.
    /// </summary>
    /// <param name="startText">DateTimeOffset으로 바꿀 ISO 8601 시작 문자열입니다.</param>
    /// <param name="endText">같거나 앞선 종료 시각 문자열입니다.</param>
    /// <returns>동기 검증이므로 반환값이 없습니다.</returns>
    [TestMethod]
    // DataRow는 같은 테스트 메서드를 서로 다른 경계 입력으로 반복 실행하는 MSTest 특성입니다.
    [DataRow("2026-10-21T01:00:00Z", "2026-10-21T01:00:00Z")]
    [DataRow("2026-10-21T02:00:00Z", "2026-10-21T01:00:00Z")]
    public void Create_WithNonPositiveDuration_ReturnsExpectedError(
        string startText,
        string endText)
    {
        // Arrange: DataRow 문자열을 문화권과 무관한 ISO 시각으로 해석합니다.
        var startsAt = DateTimeOffset.Parse(startText, System.Globalization.CultureInfo.InvariantCulture);
        var endsAt = DateTimeOffset.Parse(endText, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var result = RentalWindow.Create(startsAt, endsAt);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual("rental.invalid_window", result.Error.Code);
        Assert.AreEqual("endsAt", result.Error.Field);
    }

    /// <summary>
    /// 반납 시각과 다음 시작이 같은 인접 구간은 충돌하지 않음을 검증합니다.
    /// </summary>
    /// <returns>동기 검증이므로 반환값이 없습니다.</returns>
    [TestMethod]
    public void Overlaps_WithTouchingBoundary_ReturnsFalse()
    {
        // Arrange
        var first = RentalWindow.Create(
            DateTimeOffset.Parse("2026-10-21T01:00:00Z"),
            DateTimeOffset.Parse("2026-10-21T02:00:00Z")).Value!;
        var second = RentalWindow.Create(
            DateTimeOffset.Parse("2026-10-21T02:00:00Z"),
            DateTimeOffset.Parse("2026-10-21T03:00:00Z")).Value!;

        // Act
        var overlaps = first.Overlaps(second);

        // Assert
        Assert.IsFalse(overlaps);
    }

    /// <summary>
    /// 한 구간이 다른 구간 안쪽에 시작하면 겹침으로 판단하는지 검증합니다.
    /// </summary>
    /// <returns>동기 검증이므로 반환값이 없습니다.</returns>
    [TestMethod]
    public void Overlaps_WithSharedTime_ReturnsTrue()
    {
        // Arrange
        var first = RentalWindow.Create(
            DateTimeOffset.Parse("2026-10-21T01:00:00Z"),
            DateTimeOffset.Parse("2026-10-21T03:00:00Z")).Value!;
        var second = RentalWindow.Create(
            DateTimeOffset.Parse("2026-10-21T02:00:00Z"),
            DateTimeOffset.Parse("2026-10-21T04:00:00Z")).Value!;

        // Act and Assert: bool 하나가 관심 대상이라 한 줄로 의도를 선명하게 표현합니다.
        Assert.IsTrue(first.Overlaps(second));
    }
}
