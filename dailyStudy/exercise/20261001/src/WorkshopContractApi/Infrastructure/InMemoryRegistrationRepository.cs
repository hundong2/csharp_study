using WorkshopContractApi.Application.Ports;
using WorkshopContractApi.Domain;

namespace WorkshopContractApi.Infrastructure;

/// <summary>
/// 한 프로세스 안에서 예약 ID와 회차·좌석 충돌을 같은 lock으로 보호하는 학습용 Repository Adapter입니다.
/// </summary>
public sealed class InMemoryRegistrationRepository : IRegistrationRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, WorkshopRegistration> _byRegistrationId =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _reservedSeats = new(StringComparer.Ordinal);

    /// <summary>
    /// 두 중복 조건을 하나의 임계 구역에서 확인하고 저장해 check-then-act 경쟁을 막습니다.
    /// </summary>
    /// <param name="registration">저장할 불변 예약 Domain Model입니다.</param>
    /// <param name="cancellationToken">lock을 기다리기 전과 상태 변경 직전에 호출자 취소를 확인할 신호입니다.</param>
    /// <returns>추가, ID 중복, 좌석 선점 결과를 담은 ValueTask를 반환합니다.</returns>
    public ValueTask<RegistrationSaveOutcome> TryAddAsync(
        WorkshopRegistration registration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        cancellationToken.ThrowIfCancellationRequested();

        // lock은 아래 여러 collection 연산을 한 덩어리로 만들어 같은 좌석이 동시에 두 번 저장되지 않게 합니다.
        lock (_gate)
        {
            // lock을 기다리는 동안 취소됐을 수도 있으므로 상태를 바꾸기 직전에 다시 확인합니다.
            cancellationToken.ThrowIfCancellationRequested();
            if (_byRegistrationId.ContainsKey(registration.RegistrationId))
            {
                return ValueTask.FromResult(RegistrationSaveOutcome.DuplicateRegistrationId);
            }

            var seatKey = CreateSeatKey(registration.SessionId, registration.SeatNumber);
            if (_reservedSeats.Contains(seatKey))
            {
                return ValueTask.FromResult(RegistrationSaveOutcome.SeatAlreadyTaken);
            }

            _byRegistrationId.Add(registration.RegistrationId, registration);
            _reservedSeats.Add(seatKey);
            return ValueTask.FromResult(RegistrationSaveOutcome.Added);
        }
    }

    /// <summary>
    /// 정규화된 ID로 저장된 불변 예약을 thread-safe하게 조회합니다.
    /// </summary>
    /// <param name="registrationId">Application이 검증한 예약 식별자입니다.</param>
    /// <param name="cancellationToken">조회 전에 관찰할 취소 신호입니다.</param>
    /// <returns>예약이 있으면 그 값, 없으면 null을 담은 ValueTask를 반환합니다.</returns>
    public ValueTask<WorkshopRegistration?> FindByIdAsync(
        string registrationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _byRegistrationId.TryGetValue(registrationId, out var registration);
            return ValueTask.FromResult(registration);
        }
    }

    /// <summary>
    /// 서로 다른 회차의 같은 좌석 번호가 충돌하지 않도록 복합 key를 만듭니다.
    /// </summary>
    /// <param name="sessionId">정규화된 회차 식별자입니다.</param>
    /// <param name="seatNumber">회차 안의 좌석 번호입니다.</param>
    /// <returns>사전 비교에 사용할 충돌 없는 회차·좌석 문자열을 반환합니다.</returns>
    private static string CreateSeatKey(string sessionId, int seatNumber)
    {
        // 문자열 보간 $"..."은 변수 값을 읽기 쉬운 문자열 안에 넣는 C# 문법입니다.
        return $"{sessionId}:{seatNumber}";
    }
}
