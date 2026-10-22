using System.Collections.Immutable;
using OrderPaymentReconciliation.Application.Ports;
using OrderPaymentReconciliation.Domain;

namespace OrderPaymentReconciliation.Infrastructure;

/// <summary>
/// 학습 예제와 테스트가 외부 데이터베이스 없이 실행되도록 결제를 메모리 snapshot에 보관하는 Adapter입니다.
/// </summary>
public sealed class InMemoryPaymentRepository : IPaymentRepository
{
    private readonly ImmutableArray<Payment> _payments;

    /// <summary>
    /// 호출자가 가진 collection의 이후 변경이 저장소에 새어 들어오지 않도록 즉시 복사합니다.
    /// </summary>
    /// <param name="payments">저장소의 초기 결제 sequence입니다.</param>
    public InMemoryPaymentRepository(IEnumerable<Payment> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);
        _payments = payments.ToImmutableArray();
    }

    /// <summary>
    /// 취소 신호를 먼저 확인한 뒤 불변 결제 snapshot을 반환합니다.
    /// </summary>
    /// <param name="cancellationToken">이미 취소되었다면 OperationCanceledException을 발생시킬 신호입니다.</param>
    /// <returns>호출자가 원소를 바꿀 수 없는 결제 목록을 완료된 Task로 반환합니다.</returns>
    public Task<IReadOnlyList<Payment>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Payment>>(_payments);
    }
}
