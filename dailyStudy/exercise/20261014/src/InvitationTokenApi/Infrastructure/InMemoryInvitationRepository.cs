using InvitationTokenApi.Application.Ports;
using InvitationTokenApi.Domain;

namespace InvitationTokenApi.Infrastructure;

/// <summary>
/// 학습용 메모리 저장소이며 lock으로 중복 생성과 1회 사용 상태 전이를 원자적으로 지킵니다.
/// </summary>
public sealed class InMemoryInvitationRepository : IInvitationRepository
{
    // new()와 new(...)는 왼쪽 선언에서 object와 Dictionary 형식을 추론하는 target-typed new 문법입니다.
    private readonly object _gate = new();
    private readonly Dictionary<string, ProjectInvitation> _invitations =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 같은 ID가 없을 때만 초대를 dictionary에 추가합니다.
    /// </summary>
    /// <param name="invitation">검증과 정규화를 마친 불변 초대입니다.</param>
    /// <param name="cancellationToken">작업 시작 전 호출자 취소를 확인할 신호입니다.</param>
    /// <returns>추가됨 또는 중복 결과가 즉시 완료된 ValueTask를 반환합니다.</returns>
    public ValueTask<InvitationAddResult> AddAsync(
        ProjectInvitation invitation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        cancellationToken.ThrowIfCancellationRequested();

        // lock은 한 순간에 한 thread만 공유 dictionary를 바꾸게 해 "확인 후 추가" 두 동작을 하나처럼 보호합니다.
        lock (_gate)
        {
            if (!_invitations.TryAdd(invitation.InvitationId, invitation))
            {
                return ValueTask.FromResult(
                    new InvitationAddResult(InvitationAddStatus.Duplicate));
            }

            return ValueTask.FromResult(
                new InvitationAddResult(InvitationAddStatus.Added));
        }
    }

    /// <summary>
    /// 정규화된 ID로 현재 불변 snapshot을 안전하게 조회합니다.
    /// </summary>
    /// <param name="invitationId">대문자로 정규화된 저장소 key입니다.</param>
    /// <param name="cancellationToken">작업 시작 전 호출자 취소를 확인할 신호입니다.</param>
    /// <returns>찾은 초대 또는 null이 즉시 완료된 ValueTask를 반환합니다.</returns>
    public ValueTask<ProjectInvitation?> GetByIdAsync(
        string invitationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invitationId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _invitations.TryGetValue(invitationId, out var invitation);
            return ValueTask.FromResult(invitation);
        }
    }

    /// <summary>
    /// 조회, 재사용 검사, 불변 snapshot 교체를 같은 임계 구역에서 수행합니다.
    /// </summary>
    /// <param name="invitationId">사용 완료로 바꿀 정규화된 초대 ID입니다.</param>
    /// <param name="redeemedAtUtc">성공한 snapshot에 기록할 요청 기준 UTC 시각입니다.</param>
    /// <param name="cancellationToken">작업 시작 전 호출자 취소를 확인할 신호입니다.</param>
    /// <returns>성공, 미존재, 만료, 재사용 중 하나가 즉시 완료된 ValueTask를 반환합니다.</returns>
    public ValueTask<InvitationRedeemResult> RedeemAsync(
        string invitationId,
        DateTimeOffset redeemedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invitationId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_invitations.TryGetValue(invitationId, out var invitation))
            {
                return ValueTask.FromResult(
                    new InvitationRedeemResult(InvitationRedeemStatus.NotFound));
            }

            var redeemed = invitation.Redeem(redeemedAtUtc);
            if (!redeemed.IsSuccess)
            {
                var status = redeemed.Error!.Code == "invitation.already_redeemed"
                    ? InvitationRedeemStatus.AlreadyRedeemed
                    : InvitationRedeemStatus.Expired;
                return ValueTask.FromResult(new InvitationRedeemResult(status));
            }

            var updated = redeemed.Value!;
            _invitations[invitationId] = updated;
            return ValueTask.FromResult(
                new InvitationRedeemResult(
                    InvitationRedeemStatus.Redeemed,
                    updated));
        }
    }
}
