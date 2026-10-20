using EquipmentRentalApi.Application;
using EquipmentRentalApi.Application.Ports;
using EquipmentRentalApi.Infrastructure;
using EquipmentRentalApi.Tests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentRentalApi.Tests.Application;

/// <summary>
/// HTTP 없이 Application Service, 정책 Strategy, Repository Port의 협력을 검증합니다.
/// </summary>
[TestClass]
public sealed class CreateRentalServiceTests
{
    /// <summary>
    /// 유효한 노트북 명령이 정규화되어 저장되고 고정 생성 시각을 사용하는지 검증합니다.
    /// </summary>
    /// <returns>비동기 Application 호출과 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    // async 테스트는 await 대상이 끝나야 성공/실패가 확정되므로 완료를 나타내는 Task를 반환합니다.
    public async Task CreateAsync_WithValidLaptop_SavesNormalizedReservation()
    {
        // Arrange
        var service = CreateService();
        // with 식은 원본 record를 바꾸지 않고 지정한 값만 다른 새 복사본을 만들어 테스트 의도를 선명하게 합니다.
        var command = TestData.ValidLaptopCommand() with
        {
            ReservationId = " res-test-001 ",
            EquipmentId = " eq-laptop-001 ",
            Category = " LAPTOP "
        };

        // Act
        var result = await service.CreateAsync(command, CancellationToken.None);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual("RES-TEST-001", result.Value.ReservationId);
        Assert.AreEqual("EQ-LAPTOP-001", result.Value.EquipmentId);
        Assert.AreEqual("laptop", result.Value.Category);
        Assert.AreEqual(TestData.FixedNow, result.Value.CreatedAtUtc);
    }

    /// <summary>
    /// 최대 2일을 넘는 카메라 요청이 Repository에 도달하기 전에 정책 실패가 되는지 검증합니다.
    /// </summary>
    /// <returns>비동기 Application 호출과 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    public async Task CreateAsync_WithLongCameraRental_ReturnsPolicyFailure()
    {
        // Arrange: 카메라는 준비 시간 2시간은 지키지만 기간 3일은 허용되지 않습니다.
        var service = CreateService();
        var command = TestData.ValidLaptopCommand() with
        {
            Category = "camera",
            StartsAt = TestData.FixedNow.AddHours(3),
            EndsAt = TestData.FixedNow.AddDays(3).AddHours(3)
        };

        // Act
        var result = await service.CreateAsync(command, CancellationToken.None);

        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual("rental.duration_exceeded", result.Error.Code);
        Assert.AreEqual("endsAt", result.Error.Field);
    }

    /// <summary>
    /// 같은 장비의 겹치는 두 번째 요청이 예외가 아니라 명시적인 충돌 Result가 되는지 검증합니다.
    /// </summary>
    /// <returns>두 비동기 저장 시도가 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    public async Task CreateAsync_WithOverlappingWindow_ReturnsConflictResult()
    {
        // Arrange
        var service = CreateService();
        var first = TestData.ValidLaptopCommand("RES-FIRST-001");
        var second = TestData.ValidLaptopCommand("RES-SECOND-001");

        // Act
        var firstResult = await service.CreateAsync(first, CancellationToken.None);
        var secondResult = await service.CreateAsync(second, CancellationToken.None);

        // Assert
        Assert.IsTrue(firstResult.IsSuccess);
        Assert.IsFalse(secondResult.IsSuccess);
        Assert.IsNotNull(secondResult.Error);
        Assert.AreEqual("rental.equipment_unavailable", secondResult.Error.Code);
    }

    /// <summary>
    /// 호출 전에 취소된 token이 업무 오류로 바뀌지 않고 OperationCanceledException으로 전파되는지 검증합니다.
    /// </summary>
    /// <returns>취소 예외 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    public async Task CreateAsync_WithCanceledToken_PropagatesCancellation()
    {
        // Arrange
        var service = CreateService();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act and Assert: ThrowsExactlyAsync는 파생 예외가 아닌 정확한 취소 예외 계약을 확인합니다.
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => service.CreateAsync(TestData.ValidLaptopCommand(), cancellation.Token));
    }

    /// <summary>
    /// 실제 운영 구현과 같은 InMemory Adapter, 두 Strategy, 고정 시간을 조립합니다.
    /// </summary>
    /// <returns>각 테스트가 독립적으로 사용할 새 CreateRentalService를 반환합니다.</returns>
    private static CreateRentalService CreateService()
    {
        IEquipmentRentalRepository repository = new InMemoryEquipmentRentalRepository();
        IRentalPolicy[] policies = [new LaptopRentalPolicy(), new CameraRentalPolicy()];
        return new CreateRentalService(
            repository,
            policies,
            new FixedTimeProvider(TestData.FixedNow));
    }
}
