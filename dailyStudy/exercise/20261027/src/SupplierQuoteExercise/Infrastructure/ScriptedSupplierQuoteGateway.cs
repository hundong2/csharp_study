using SupplierQuoteExercise.Application.Ports;
using SupplierQuoteExercise.Domain;

namespace SupplierQuoteExercise.Infrastructure;

/// <summary>
/// 설정된 지연 뒤 미리 정한 성공 또는 실패를 돌려주는 학습용 Gateway Adapter입니다.
/// 실제 network 대신 결정적인 script를 쓰면 완료 순서와 부분 실패를 반복 검증할 수 있습니다.
/// </summary>
public sealed class ScriptedSupplierQuoteGateway : ISupplierQuoteGateway
{
    private readonly TimeSpan _delay;
    private readonly Result<SupplierQuote> _outcome;

    /// <summary>
    /// 공급업체 ID, 응답 지연, 반환 결과를 고정한 학습용 gateway를 만듭니다.
    /// 세 파라미터로 script를 초기화하며 생성자는 별도의 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="supplierId">완료 결과를 식별할 비어 있지 않은 공급업체 ID입니다.</param>
    /// <param name="delay">실제 외부 I/O 시간을 흉내 낼 0 이상의 지연입니다.</param>
    /// <param name="outcome">지연 뒤 반환할 성공 견적 또는 예상 가능한 실패입니다.</param>
    public ScriptedSupplierQuoteGateway(
        string supplierId,
        TimeSpan delay,
        Result<SupplierQuote> outcome)
    {
        if (string.IsNullOrWhiteSpace(supplierId))
        {
            throw new ArgumentException("공급업체 ID가 필요합니다.", nameof(supplierId));
        }

        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "지연은 음수일 수 없습니다.");
        }

        SupplierId = supplierId.Trim();
        _delay = delay;
        _outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
    }

    /// <summary>이 adapter가 흉내 내는 공급업체 ID를 반환합니다.</summary>
    public string SupplierId { get; }

    /// <summary>
    /// 취소 가능한 지연으로 외부 I/O를 흉내 낸 뒤 script 결과를 반환합니다.
    /// </summary>
    /// <param name="request">실제 adapter라면 외부 API에 보낼 검증된 견적 요청입니다.</param>
    /// <param name="cancellationToken">지연 중 호출자가 작업을 취소할 수 있는 신호입니다.</param>
    /// <returns>생성자에서 정한 견적 성공 또는 예상 가능한 실패를 담은 Task를 반환합니다.</returns>
    public async Task<Result<SupplierQuote>> RequestQuoteAsync(
        QuoteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // await는 지연 동안 thread를 막지 않고, 취소되면 OperationCanceledException을 그대로 전파합니다.
        await Task.Delay(_delay, cancellationToken);
        return _outcome;
    }
}
