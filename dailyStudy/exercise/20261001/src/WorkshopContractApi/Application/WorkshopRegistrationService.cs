using WorkshopContractApi.Application.Ports;
using WorkshopContractApi.Domain;

namespace WorkshopContractApi.Application;

/// <summary>
/// 등록 요청의 검증, 회차 조회, 가격 계산, 원자 저장 순서를 조율하는 Application Service입니다.
/// </summary>
public sealed class WorkshopRegistrationService
{
    private readonly IWorkshopCatalog _catalog;
    private readonly IRegistrationRepository _repository;
    private readonly ITicketPricePolicy _pricePolicy;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// use case에 필요한 Port와 Strategy, 교체 가능한 시계를 생성자 주입으로 받습니다.
    /// </summary>
    /// <param name="catalog">회차를 조회할 Catalog Port입니다.</param>
    /// <param name="repository">예약을 원자적으로 저장할 Repository Port입니다.</param>
    /// <param name="pricePolicy">등급별 가격을 계산할 Strategy Port입니다.</param>
    /// <param name="timeProvider">현재 UTC 시각을 제공하며 테스트에서 바꿀 수 있는 추상화입니다.</param>
    public WorkshopRegistrationService(
        IWorkshopCatalog catalog,
        IRegistrationRepository repository,
        ITicketPricePolicy pricePolicy,
        TimeProvider timeProvider)
    {
        _catalog = catalog;
        _repository = repository;
        _pricePolicy = pricePolicy;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 외부 명령을 유효한 Domain 초안으로 바꾸고 회차·가격·좌석 충돌을 확인해 예약을 확정합니다.
    /// </summary>
    /// <param name="command">아직 신뢰하지 않는 use case 입력입니다.</param>
    /// <param name="cancellationToken">HTTP 연결 종료 같은 취소 신호를 모든 Port에 전달합니다.</param>
    /// <returns>확정 예약 또는 클라이언트가 처리할 안정 오류를 담은 Result를 반환합니다.</returns>
    public async Task<Result<WorkshopRegistration>> RegisterAsync(
        RegisterWorkshopCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var draftResult = RegistrationDraft.Create(
            command.RegistrationId,
            command.SessionId,
            command.AttendeeId,
            command.SeatNumber,
            command.AttendeeTier);
        if (!draftResult.IsSuccess)
        {
            return Result<WorkshopRegistration>.Failure(draftResult.Error!);
        }

        var draft = draftResult.Value!;
        // await는 Port 작업이 미완료면 현재 흐름을 잠시 양보하고, 이미 완료됐으면 동기적으로 이어갑니다.
        // 지금의 memory Adapter는 즉시 끝날 수 있지만 실제 DB Adapter로 교체해도 Application 코드는 바뀌지 않습니다.
        var session = await _catalog.FindByIdAsync(draft.SessionId, cancellationToken);
        if (session is null)
        {
            return Result<WorkshopRegistration>.Failure(new Error(
                "workshop.session.not_found",
                "요청한 워크숍 회차를 찾을 수 없습니다.",
                "sessionId"));
        }

        if (draft.SeatNumber > session.Capacity)
        {
            return Result<WorkshopRegistration>.Failure(new Error(
                "workshop.seat.out_of_range",
                $"이 회차의 좌석은 1부터 {session.Capacity}까지입니다.",
                "seatNumber"));
        }

        var priceWon = _pricePolicy.CalculatePriceWon(session, draft.Tier);
        var registration = new WorkshopRegistration(
            draft.RegistrationId,
            session.SessionId,
            session.Title,
            draft.AttendeeId,
            draft.SeatNumber,
            draft.Tier,
            priceWon,
            _timeProvider.GetUtcNow());

        var saveOutcome = await _repository.TryAddAsync(registration, cancellationToken);
        return saveOutcome switch
        {
            RegistrationSaveOutcome.Added => Result<WorkshopRegistration>.Success(registration),
            RegistrationSaveOutcome.DuplicateRegistrationId => Result<WorkshopRegistration>.Failure(new Error(
                "registration.id.duplicate",
                "이미 사용된 registrationId입니다.",
                "registrationId")),
            RegistrationSaveOutcome.SeatAlreadyTaken => Result<WorkshopRegistration>.Failure(new Error(
                "registration.seat.taken",
                "선택한 좌석은 이미 예약되었습니다.",
                "seatNumber")),
            _ => throw new InvalidOperationException($"처리하지 않은 저장 결과입니다: {saveOutcome}")
        };
    }

    /// <summary>
    /// 외부 예약 ID를 정규화한 뒤 저장된 예약을 조회합니다.
    /// </summary>
    /// <param name="registrationId">route에서 받은 아직 검증하지 않은 예약 ID입니다.</param>
    /// <param name="cancellationToken">저장소 조회를 중단할 취소 신호입니다.</param>
    /// <returns>예약 또는 잘못된 ID·미존재 오류를 담은 Result를 반환합니다.</returns>
    public async Task<Result<WorkshopRegistration>> FindAsync(
        string? registrationId,
        CancellationToken cancellationToken)
    {
        var normalizedId = RegistrationRules.NormalizeIdentifier(
            registrationId,
            "registration.id.invalid",
            "registrationId");
        if (!normalizedId.IsSuccess)
        {
            return Result<WorkshopRegistration>.Failure(normalizedId.Error!);
        }

        var registration = await _repository.FindByIdAsync(normalizedId.Value!, cancellationToken);
        return registration is null
            ? Result<WorkshopRegistration>.Failure(new Error(
                "registration.not_found",
                "요청한 예약을 찾을 수 없습니다.",
                "registrationId"))
            : Result<WorkshopRegistration>.Success(registration);
    }
}
