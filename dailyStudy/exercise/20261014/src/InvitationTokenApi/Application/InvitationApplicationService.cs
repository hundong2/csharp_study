using InvitationTokenApi.Application.Ports;
using InvitationTokenApi.Domain;

namespace InvitationTokenApi.Application;

/// <summary>
/// 초대 생성, 조회, 토큰 사용 순서를 조율하는 Application Service입니다.
/// HTTP 표현, Domain 규칙, Adapter 구현에서 use case 순서를 분리해 SRP를 지키고 같은 흐름을 다른 진입점에서 재사용·단위 테스트할 수 있게 합니다.
/// </summary>
public sealed class InvitationApplicationService
{
    private readonly IInvitationRepository _repository;
    private readonly IInvitationTokenProtector _tokenProtector;
    private readonly IInvitationExpiryPolicy _expiryPolicy;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// use case가 필요한 Port, Strategy, 시계를 명시적으로 주입받습니다.
    /// </summary>
    /// <param name="repository">초대 snapshot과 원자적 사용 전이를 보관하는 Port입니다.</param>
    /// <param name="tokenProtector">토큰을 보호하고 검증하는 보안 Port입니다.</param>
    /// <param name="expiryPolicy">권한별 수명과 만료 경계를 결정하는 Strategy입니다.</param>
    /// <param name="timeProvider">실제 시간과 테스트 시간을 교체 가능하게 하는 시계 추상화입니다.</param>
    /// <returns>생성자는 서비스를 초기화하므로 별도 반환값은 없습니다.</returns>
    public InvitationApplicationService(
        IInvitationRepository repository,
        IInvitationTokenProtector tokenProtector,
        IInvitationExpiryPolicy expiryPolicy,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(tokenProtector);
        ArgumentNullException.ThrowIfNull(expiryPolicy);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _tokenProtector = tokenProtector;
        _expiryPolicy = expiryPolicy;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 입력을 검증하고 권한별 만료를 계산해 저장한 뒤 보호 토큰을 발급합니다.
    /// </summary>
    /// <param name="command">아직 신뢰할 수 없는 ID, 이메일, 권한 입력입니다.</param>
    /// <param name="cancellationToken">HTTP 요청 종료 같은 호출자 취소 신호입니다.</param>
    /// <returns>생성된 초대와 토큰 또는 검증·중복 오류가 든 Task를 반환합니다.</returns>
    // async/await는 Repository가 끝날 때 thread를 붙잡지 않고 기다리면서 취소와 예외 흐름을 보존합니다.
    public async Task<Result<CreatedInvitation>> CreateAsync(
        CreateInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var roleResult = ProjectInvitation.ParseRole(command.Role);
        if (!roleResult.IsSuccess)
        {
            return Result<CreatedInvitation>.Failure(roleResult.Error!);
        }

        // TimeProvider는 DateTimeOffset.UtcNow 직접 호출을 감춰 테스트가 임의 시각을 결정하게 합니다.
        var issuedAtUtc = _timeProvider.GetUtcNow();
        var expiresAtUtc = _expiryPolicy.CalculateExpiration(roleResult.Value, issuedAtUtc);
        var invitationResult = ProjectInvitation.Create(
            command.InvitationId,
            command.ProjectId,
            command.Email,
            roleResult.Value,
            issuedAtUtc,
            expiresAtUtc);
        if (!invitationResult.IsSuccess)
        {
            return Result<CreatedInvitation>.Failure(invitationResult.Error!);
        }

        var invitation = invitationResult.Value!;
        var addResult = await _repository.AddAsync(invitation, cancellationToken);
        if (addResult.Status is InvitationAddStatus.Duplicate)
        {
            return Result<CreatedInvitation>.Failure(
                new Error(
                    "invitation.duplicate",
                    "같은 invitationId의 초대가 이미 있습니다.",
                    "invitationId"));
        }

        var payload = new InvitationTokenPayload(
            Version: 1,
            invitation.InvitationId,
            invitation.ProjectId,
            invitation.Role,
            invitation.ExpiresAtUtc);
        var token = _tokenProtector.Protect(payload);
        return Result<CreatedInvitation>.Success(new CreatedInvitation(invitation, token));
    }

    /// <summary>
    /// route ID를 정규화해 저장된 초대를 조회합니다.
    /// </summary>
    /// <param name="invitationId">route에서 받은 초대 식별자입니다.</param>
    /// <param name="cancellationToken">호출자가 조회를 더 기다리지 않는다는 취소 신호입니다.</param>
    /// <returns>찾은 초대 또는 존재하지 않는다는 오류가 든 Task를 반환합니다.</returns>
    public async Task<Result<ProjectInvitation>> GetAsync(
        string? invitationId,
        CancellationToken cancellationToken)
    {
        var normalized = ProjectInvitation.NormalizeInvitationId(invitationId);
        if (!normalized.IsSuccess)
        {
            return Result<ProjectInvitation>.Failure(NotFoundError());
        }

        var invitation = await _repository.GetByIdAsync(
            normalized.Value!,
            cancellationToken);
        return invitation is null
            ? Result<ProjectInvitation>.Failure(NotFoundError())
            : Result<ProjectInvitation>.Success(invitation);
    }

    /// <summary>
    /// 토큰을 검증하고 저장된 발급 기록과 대조한 뒤 정확히 한 번만 사용 완료로 바꿉니다.
    /// </summary>
    /// <param name="command">외부에서 돌아온 신뢰할 수 없는 토큰 입력입니다.</param>
    /// <param name="cancellationToken">호출자가 결과를 더 기다리지 않는다는 취소 신호입니다.</param>
    /// <returns>사용 완료된 초대 또는 공통 토큰·재사용 오류가 든 Task를 반환합니다.</returns>
    public async Task<Result<ProjectInvitation>> RedeemAsync(
        RedeemInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 한 요청 안에서는 같은 now 값을 사용해야 만료 검사와 저장 상태 전이의 경계가 흔들리지 않습니다.
        var nowUtc = _timeProvider.GetUtcNow();
        var payloadResult = _tokenProtector.Unprotect(command.Token, nowUtc);
        if (!payloadResult.IsSuccess)
        {
            return Result<ProjectInvitation>.Failure(InvalidTokenError());
        }

        var payload = payloadResult.Value!;
        var invitation = await _repository.GetByIdAsync(
            payload.InvitationId,
            cancellationToken);
        if (invitation is null ||
            invitation.ProjectId != payload.ProjectId ||
            invitation.Role != payload.Role ||
            invitation.ExpiresAtUtc != payload.ExpiresAtUtc ||
            _expiryPolicy.IsExpired(invitation.ExpiresAtUtc, nowUtc))
        {
            return Result<ProjectInvitation>.Failure(InvalidTokenError());
        }

        var redeemResult = await _repository.RedeemAsync(
            invitation.InvitationId,
            nowUtc,
            cancellationToken);

        return redeemResult.Status switch
        {
            InvitationRedeemStatus.Redeemed =>
                Result<ProjectInvitation>.Success(redeemResult.Invitation!),
            InvitationRedeemStatus.AlreadyRedeemed =>
                Result<ProjectInvitation>.Failure(
                    new Error(
                        "invitation.already_redeemed",
                        "이미 사용한 초대 토큰입니다.",
                        "token")),
            InvitationRedeemStatus.NotFound or InvitationRedeemStatus.Expired =>
                Result<ProjectInvitation>.Failure(InvalidTokenError()),
            // switch의 기본 분기는 개발자가 새 Repository 상태를 매핑하지 않은 프로그래밍 오류이므로 Result로 숨기지 않고 예외로 드러냅니다.
            _ => throw new InvalidOperationException(
                $"처리하지 않은 Repository 결과입니다: {redeemResult.Status}")
        };
    }

    /// <summary>
    /// 조회 시 ID 형식과 실제 존재 여부를 하나의 404 계약으로 숨깁니다.
    /// </summary>
    /// <returns>GET endpoint가 404로 변환할 안전한 오류를 반환합니다.</returns>
    private static Error NotFoundError()
    {
        return new Error(
            "invitation.not_found",
            "요청한 초대를 찾을 수 없습니다.",
            "invitationId");
    }

    /// <summary>
    /// 형식, 변조, purpose 불일치, 미존재, 만료 원인을 공격자에게 구분하지 않는 오류를 만듭니다.
    /// </summary>
    /// <returns>redeem endpoint가 400으로 변환할 공통 토큰 오류를 반환합니다.</returns>
    private static Error InvalidTokenError()
    {
        return new Error(
            "invitation.token.invalid_or_expired",
            "초대 토큰이 올바르지 않거나 만료되었습니다.",
            "token");
    }
}
