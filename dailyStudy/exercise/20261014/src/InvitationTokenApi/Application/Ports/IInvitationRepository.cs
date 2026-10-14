using InvitationTokenApi.Domain;

namespace InvitationTokenApi.Application.Ports;

/// <summary>
/// 초대 저장과 한 번뿐인 상태 전이를 구현 세부사항에서 분리하는 Repository Port입니다.
/// </summary>
public interface IInvitationRepository
{
    /// <summary>
    /// 같은 ID가 없을 때만 새 초대를 저장합니다.
    /// </summary>
    /// <param name="invitation">검증을 마친 불변 초대입니다.</param>
    /// <param name="cancellationToken">호출자가 더 이상 결과를 기다리지 않는다는 취소 신호입니다.</param>
    /// <returns>추가 성공 또는 중복을 나타내는 결과가 든 ValueTask를 반환합니다.</returns>
    ValueTask<InvitationAddResult> AddAsync(
        ProjectInvitation invitation,
        CancellationToken cancellationToken);

    /// <summary>
    /// 정규화된 ID로 현재 초대 snapshot을 조회합니다.
    /// </summary>
    /// <param name="invitationId">대문자로 정규화된 저장소 key입니다.</param>
    /// <param name="cancellationToken">호출자가 더 이상 결과를 기다리지 않는다는 취소 신호입니다.</param>
    /// <returns>찾은 불변 초대 또는 없음을 뜻하는 null이 든 ValueTask를 반환합니다.</returns>
    ValueTask<ProjectInvitation?> GetByIdAsync(
        string invitationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 같은 토큰을 여러 요청이 동시에 사용해도 정확히 하나만 상태 전이에 성공시킵니다.
    /// </summary>
    /// <param name="invitationId">사용 완료로 바꿀 정규화된 초대 ID입니다.</param>
    /// <param name="redeemedAtUtc">성공 시 기록할 요청 기준 UTC 시각입니다.</param>
    /// <param name="cancellationToken">호출자가 더 이상 결과를 기다리지 않는다는 취소 신호입니다.</param>
    /// <returns>성공, 미존재, 만료, 이미 사용됨 중 하나와 선택적 snapshot을 반환합니다.</returns>
    ValueTask<InvitationRedeemResult> RedeemAsync(
        string invitationId,
        DateTimeOffset redeemedAtUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// 새 초대를 저장한 결과 종류입니다.
/// </summary>
public enum InvitationAddStatus
{
    Added,
    Duplicate
}

/// <summary>
/// 저장 결과를 Application Service가 구현 형식을 몰라도 해석하게 합니다.
/// 괄호 안 매개변수는 주 생성자가 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Status">추가 성공 또는 중복 상태입니다.</param>
public sealed record InvitationAddResult(InvitationAddStatus Status);

/// <summary>
/// 원자적인 토큰 사용 상태 전이의 결과 종류입니다.
/// </summary>
public enum InvitationRedeemStatus
{
    Redeemed,
    NotFound,
    Expired,
    AlreadyRedeemed
}

/// <summary>
/// 상태 전이 결과와 성공했을 때의 새 snapshot을 함께 전달합니다.
/// 괄호 안 매개변수는 주 생성자가 각 속성을 초기화하며, 생성자는 객체만 만들고 반환값은 없습니다.
/// </summary>
/// <param name="Status">원자적 상태 전이 결과입니다.</param>
/// <param name="Invitation">성공했을 때 사용 완료된 초대이며 실패면 null입니다.</param>
public sealed record InvitationRedeemResult(
    InvitationRedeemStatus Status,
    ProjectInvitation? Invitation = null);
