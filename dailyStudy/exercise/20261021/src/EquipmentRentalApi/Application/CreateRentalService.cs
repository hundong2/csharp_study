using EquipmentRentalApi.Application.Ports;
using EquipmentRentalApi.Domain;

namespace EquipmentRentalApi.Application;

/// <summary>
/// 입력 검증, 정책 선택, 시간 충돌 저장을 올바른 순서로 조율하는 Application Service입니다.
/// </summary>
public sealed class CreateRentalService
{
    private readonly IEquipmentRentalRepository _repository;
    private readonly IReadOnlyDictionary<string, IRentalPolicy> _policies;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Repository Port, 여러 정책 Strategy, 교체 가능한 시간을 주입받아 서비스를 만듭니다.
    /// </summary>
    /// <param name="repository">예약 저장·조회 기술을 감춘 Repository Port입니다.</param>
    /// <param name="policies">장비 분류별 정책 Strategy 모음입니다.</param>
    /// <param name="timeProvider">운영에서는 시스템 시간, 테스트에서는 고정 시간을 제공하는 추상화입니다.</param>
    public CreateRentalService(
        IEquipmentRentalRepository repository,
        IEnumerable<IRentalPolicy> policies,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _timeProvider = timeProvider;
        // lambda의 =>는 policy를 받아 Category를 돌려주는 이름 없는 작은 함수입니다.
        // ToDictionary는 여러 Strategy를 category key로 한 번에 찾게 하며, 중복 key가 있으면 조립 오류를 즉시 드러냅니다.
        _policies = policies.ToDictionary(policy => policy.Category, StringComparer.Ordinal);
    }

    /// <summary>
    /// 외부 명령을 Domain 값으로 만들고 분류 정책을 적용한 뒤 충돌 없이 예약을 저장합니다.
    /// </summary>
    /// <param name="command">Presentation 또는 다른 Adapter가 전달한 대여 생성 명령입니다.</param>
    /// <param name="cancellationToken">HTTP 연결 종료 등으로 작업을 중단할 수 있는 신호입니다.</param>
    /// <returns>저장된 예약 또는 예상 가능한 검증·충돌 오류를 담은 Result를 반환합니다.</returns>
    // async는 await로 비동기 작업을 기다릴 수 있는 메서드라는 뜻이며, 완료 결과는 Task 안에 담깁니다.
    public async Task<Result<RentalReservation>> CreateAsync(
        CreateRentalCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        // var는 오른쪽 반환 형식이 분명할 때 compiler가 형식을 추론하게 하여 긴 generic 형식의 반복을 줄입니다.
        var windowResult = RentalWindow.Create(command.StartsAt, command.EndsAt);
        if (!windowResult.IsSuccess)
        {
            // Result.IsSuccess의 nullable 계약 덕분에 실패 분기에서 Error가 null이 아님을 compiler도 압니다.
            return Result<RentalReservation>.Failure(windowResult.Error);
        }

        var nowUtc = _timeProvider.GetUtcNow();
        var reservationResult = RentalReservation.Create(
            command.ReservationId,
            command.EquipmentId,
            command.BorrowerId,
            command.Category,
            command.Purpose,
            windowResult.Value,
            nowUtc);
        if (!reservationResult.IsSuccess)
        {
            return reservationResult;
        }

        var reservation = reservationResult.Value;
        if (!_policies.TryGetValue(reservation.Category, out var policy))
        {
            // 형식은 맞지만 등록되지 않은 category 입력과 DI 등록 누락을 모두 설명 가능한 실패로 막습니다.
            return Result<RentalReservation>.Failure(
                new Error(
                    "rental.policy_unavailable",
                    "요청한 장비 분류의 대여 정책이 등록되지 않았습니다.",
                    "category"));
        }

        var policyResult = policy.Validate(reservation.Window, nowUtc);
        if (!policyResult.IsSuccess)
        {
            return Result<RentalReservation>.Failure(policyResult.Error);
        }

        // await는 실제 DB Adapter로 바뀌었을 때 스레드를 점유하지 않고 I/O 완료를 기다릴 수 있게 합니다.
        var writeStatus = await _repository.TryAddAsync(reservation, cancellationToken);
        // switch 식은 상태 하나를 성공 또는 실패 Result 하나로 빠짐없이 변환하고 그 값을 즉시 반환합니다.
        return writeStatus switch
        {
            ReservationWriteStatus.Saved => Result<RentalReservation>.Success(reservation),
            ReservationWriteStatus.DuplicateReservationId => Result<RentalReservation>.Failure(
                new Error(
                    "rental.duplicate_reservation_id",
                    "이미 사용한 reservationId입니다.",
                    "reservationId")),
            ReservationWriteStatus.EquipmentUnavailable => Result<RentalReservation>.Failure(
                new Error(
                    "rental.equipment_unavailable",
                    "같은 장비에 겹치는 대여 예약이 있습니다.",
                    "equipmentId")),
            _ => throw new InvalidOperationException($"알 수 없는 저장 상태: {writeStatus}")
        };
    }

    /// <summary>
    /// 예약 ID의 공백과 대소문자를 정규화한 뒤 저장된 예약을 조회합니다.
    /// </summary>
    /// <param name="reservationId">URL 경로에서 받은 예약 식별자입니다.</param>
    /// <param name="cancellationToken">조회가 필요 없어졌음을 알리는 취소 신호입니다.</param>
    /// <returns>저장된 예약 또는 없음을 뜻하는 null을 담아 완료되는 ValueTask를 반환합니다.</returns>
    public ValueTask<RentalReservation?> FindAsync(
        string reservationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reservationId);
        var normalizedId = reservationId.Trim().ToUpperInvariant();
        return _repository.FindByIdAsync(normalizedId, cancellationToken);
    }
}
