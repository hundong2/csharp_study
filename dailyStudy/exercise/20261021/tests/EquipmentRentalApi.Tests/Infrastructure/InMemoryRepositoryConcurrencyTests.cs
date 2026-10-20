using EquipmentRentalApi.Application.Ports;
using EquipmentRentalApi.Infrastructure;
using EquipmentRentalApi.Tests.TestDoubles;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EquipmentRentalApi.Tests.Infrastructure;

/// <summary>
/// 단순 순차 테스트로 놓치기 쉬운 Repository의 경쟁 조건을 여러 Task로 검증합니다.
/// </summary>
[TestClass]
public sealed class InMemoryRepositoryConcurrencyTests
{
    /// <summary>
    /// 같은 장비와 시간에 24개 저장이 동시에 경쟁해도 정확히 하나만 성공하는지 검증합니다.
    /// </summary>
    /// <returns>모든 병렬 저장과 결과 집계가 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    // async 테스트는 여러 Task를 await한 뒤에야 검증이 끝나므로 완료 신호를 Task로 test runner에 돌려줍니다.
    public async Task TryAddAsync_WithConcurrentOverlaps_SavesExactlyOne()
    {
        // Arrange
        // var는 오른쪽 new 표현식에서 구체 형식을 compiler가 추론해 중복 형식명을 줄입니다.
        var repository = new InMemoryEquipmentRentalRepository();
        var contenders = Enumerable.Range(1, 24)
            .Select(
                index => TestData.ValidReservation($"RES-RACE-{index:000}"))
            .ToArray();
        // RunContinuationsAsynchronously는 gate를 여는 thread가 모든 작업을 직렬로 실행하는 현상을 줄입니다.
        var startGate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;

        // Act: ToArray로 지연 실행 LINQ를 즉시 열거해 모든 Task를 만든 뒤 gate를 한 번에 엽니다.
        // => lambda는 후보 예약마다 실행할 작업을 만들고, 내부 async lambda는 gate와 저장을 순서대로 await합니다.
        var tasks = contenders.Select(
            reservation => Task.Run(
                async () =>
                {
                    // Interlocked는 여러 thread가 도착 수를 동시에 늘려도 증가 값을 잃지 않게 합니다.
                    if (Interlocked.Increment(ref readyCount) == contenders.Length)
                    {
                        allReady.TrySetResult();
                    }

                    await startGate.Task;
                    return await repository.TryAddAsync(reservation, CancellationToken.None);
                }))
            .ToArray();

        var allReadyBeforeTimeout = true;
        try
        {
            // WaitAsync는 test thread를 막지 않으면서 준비 신호에 5초 상한을 둡니다.
            await allReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            allReadyBeforeTimeout = false;
        }
        finally
        {
            // timeout이어도 gate를 열어 이미 만든 Task가 영원히 남지 않게 합니다.
            startGate.TrySetResult();
        }

        var statuses = await Task.WhenAll(tasks);

        // Assert: 최종 개수뿐 아니라 나머지 실패 종류도 확인해 조용한 유실을 막습니다.
        Assert.IsTrue(allReadyBeforeTimeout, "모든 경쟁 작업이 제한 시간 안에 준비되어야 합니다.");
        Assert.AreEqual(1, statuses.Count(status => status == ReservationWriteStatus.Saved));
        Assert.AreEqual(
            23,
            statuses.Count(status => status == ReservationWriteStatus.EquipmentUnavailable));
    }

    /// <summary>
    /// 같은 reservationId를 다시 쓰면 장비 충돌보다 ID 중복을 먼저 보고하는지 검증합니다.
    /// </summary>
    /// <returns>두 저장 시도와 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    [TestMethod]
    public async Task TryAddAsync_WithDuplicateId_ReturnsDuplicateStatus()
    {
        // Arrange
        var repository = new InMemoryEquipmentRentalRepository();
        var reservation = TestData.ValidReservation("RES-DUPLICATE-001");

        // Act
        var first = await repository.TryAddAsync(reservation, CancellationToken.None);
        var second = await repository.TryAddAsync(reservation, CancellationToken.None);

        // Assert
        Assert.AreEqual(ReservationWriteStatus.Saved, first);
        Assert.AreEqual(ReservationWriteStatus.DuplicateReservationId, second);
    }
}
