using ReportTimeoutApi.Application.Ports;
using ReportTimeoutApi.Domain;

namespace ReportTimeoutApi.Infrastructure;

/// <summary>
/// 외부 데이터베이스 대신 결정적인 메모리 데이터를 사용하며 취소 전파를 관찰할 수 있는 Repository Adapter입니다.
/// </summary>
public sealed class InMemoryReportRepository : IReportRepository
{
    private readonly IReadOnlyDictionary<string, InvoiceLine[]> _linesByCustomer;
    private int _startedReads;
    private int _completedReads;
    private int _canceledReads;

    /// <summary>
    /// 실습과 테스트가 항상 같은 결과를 얻도록 두 고객의 고정 seed 데이터를 준비합니다.
    /// </summary>
    /// <returns>생성자는 메모리 저장소를 초기화하며 별도 값을 반환하지 않습니다.</returns>
    public InMemoryReportRepository()
    {
        // ["키"] = 값은 Dictionary index initializer, 안쪽 [ ... ]은 배열을 만드는 collection expression입니다.
        // 125_000m의 _는 큰 수를 읽기 위한 자리 구분자이고 m은 정확한 decimal 금액임을 뜻합니다.
        _linesByCustomer = new Dictionary<string, InvoiceLine[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["CUST-100"] =
            [
                new InvoiceLine("INV-1001", "클라우드 사용료", 125_000m),
                new InvoiceLine("INV-1002", "기술 지원", 80_000m)
            ],
            ["CUST-200"] =
            [
                new InvoiceLine("INV-2001", "데이터 보관", 45_500m)
            ]
        };
    }

    // =>는 짧은 getter 결과를 한 식으로 반환합니다. Volatile.Read는 다른 요청이 갱신한 최신 계수를 읽습니다.
    // 첫 ref는 복사한 숫자가 아니라 원래 필드의 메모리 위치를 읽도록 참조로 전달한다는 뜻입니다.
    /// <summary>시작된 Repository 읽기 횟수이며 테스트 관찰용입니다.</summary>
    public int StartedReads => Volatile.Read(ref _startedReads);

    /// <summary>정상 완료된 Repository 읽기 횟수이며 테스트 관찰용입니다.</summary>
    public int CompletedReads => Volatile.Read(ref _completedReads);

    /// <summary>취소 신호에 응답해 중단된 Repository 읽기 횟수이며 테스트 관찰용입니다.</summary>
    public int CanceledReads => Volatile.Read(ref _canceledReads);

    /// <summary>
    /// 교육용 지연 뒤 고객 청구 데이터를 복사해 반환하고, 기다리는 동안 취소 신호에 즉시 협력합니다.
    /// </summary>
    /// <param name="customerId">정규화된 고객 번호입니다.</param>
    /// <param name="simulatedLatency">실제 I/O 대기를 흉내 낼 지연 시간입니다.</param>
    /// <param name="cancellationToken">호출 중단 또는 request timeout을 알리는 신호입니다.</param>
    /// <returns>고객의 청구 항목 복사본을 담아 완료되는 Task를 반환하며, 없으면 빈 목록입니다.</returns>
    public async Task<IReadOnlyList<InvoiceLine>> LoadInvoiceLinesAsync(
        string customerId,
        TimeSpan simulatedLatency,
        CancellationToken cancellationToken)
    {
        // Interlocked는 여러 요청이 동시에 숫자를 바꿔도 증가 연산이 유실되지 않게 합니다.
        // ref는 메서드가 숫자의 복사본이 아니라 원래 필드 자체를 안전하게 바꾸도록 참조로 전달합니다.
        Interlocked.Increment(ref _startedReads);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Task.Delay에 token을 넘겨야 timeout 동안 남은 시간을 끝까지 기다리지 않고 즉시 깨어납니다.
            await Task.Delay(simulatedLatency, cancellationToken);

            if (!_linesByCustomer.TryGetValue(customerId, out var lines))
            {
                Interlocked.Increment(ref _completedReads);
                return Array.Empty<InvoiceLine>();
            }

            Interlocked.Increment(ref _completedReads);
            // ToArray는 내부 배열을 외부에 직접 노출하지 않아 Repository 소유 데이터를 보호합니다.
            return lines.ToArray();
        }
        // catch 뒤의 when은 이 메서드가 받은 token 때문에 생긴 취소 예외만 잡는 exception filter입니다.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Interlocked.Increment(ref _canceledReads);
            // 인수 없는 throw;는 현재 예외와 원래 stack trace를 보존해 timeout middleware까지 다시 올립니다.
            throw;
        }
    }
}
