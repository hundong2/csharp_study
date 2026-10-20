using EquipmentRentalApi.Application.Ports;
using EquipmentRentalApi.Domain;

namespace EquipmentRentalApi.Infrastructure;

/// <summary>
/// 한 프로세스 안에서 예약 ID와 장비 시간 충돌을 원자적으로 지키는 학습용 Repository Adapter입니다.
/// </summary>
public sealed class InMemoryEquipmentRentalRepository : IEquipmentRentalRepository
{
    // new()는 왼쪽 선언에서 object 형식을 알 수 있어 형식 이름을 반복하지 않는 target-typed new 문법입니다.
    private readonly object _gate = new();
    private readonly Dictionary<string, RentalReservation> _reservations =
        new(StringComparer.Ordinal);

    /// <summary>
    /// ID 중복 확인, 장비 시간 겹침 확인, 저장을 같은 lock 안에서 실행합니다.
    /// </summary>
    /// <param name="reservation">검증과 정책을 통과한 저장 후보 예약입니다.</param>
    /// <param name="cancellationToken">lock 진입 전에 작업을 중단할 수 있는 신호입니다.</param>
    /// <returns>저장 여부를 나타내는 상태를 즉시 담은 ValueTask를 반환합니다.</returns>
    public ValueTask<ReservationWriteStatus> TryAddAsync(
        RentalReservation reservation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        cancellationToken.ThrowIfCancellationRequested();

        // lock은 아래 확인과 추가 사이에 다른 thread가 끼어들지 못하게 해 TOCTOU 경쟁 조건을 막습니다.
        lock (_gate)
        {
            if (_reservations.ContainsKey(reservation.ReservationId))
            {
                return ValueTask.FromResult(ReservationWriteStatus.DuplicateReservationId);
            }

            // var는 Any가 bool을 반환한다는 사실을 compiler가 추론합니다.
            // => lambda는 기존 예약 하나를 받아 같은 장비의 시간이 겹치는지 계산합니다.
            var overlaps = _reservations.Values.Any(
                existing => string.Equals(
                        existing.EquipmentId,
                        reservation.EquipmentId,
                        StringComparison.Ordinal)
                    && existing.Window.Overlaps(reservation.Window));
            if (overlaps)
            {
                return ValueTask.FromResult(ReservationWriteStatus.EquipmentUnavailable);
            }

            _reservations.Add(reservation.ReservationId, reservation);
            return ValueTask.FromResult(ReservationWriteStatus.Saved);
        }
    }

    /// <summary>
    /// 예약 식별자로 저장된 불변 예약을 조회합니다.
    /// </summary>
    /// <param name="reservationId">Application Service가 정규화한 예약 ID입니다.</param>
    /// <param name="cancellationToken">lock 진입 전에 작업을 중단할 수 있는 신호입니다.</param>
    /// <returns>찾은 예약 또는 null을 즉시 담은 ValueTask를 반환합니다.</returns>
    public ValueTask<RentalReservation?> FindByIdAsync(
        string reservationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservationId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            // out var는 조회 성공 시 값을 받을 지역 변수를 조건식 안에서 함께 선언하는 문법입니다.
            return ValueTask.FromResult(
                _reservations.TryGetValue(reservationId, out var reservation)
                    ? reservation
                    : null);
        }
    }
}
