using SupplierQuoteExercise.Application.Ports;
using SupplierQuoteExercise.Domain;

namespace SupplierQuoteExercise.Infrastructure;

/// <summary>
/// 완료 report를 process 메모리에 보관하는 학습용 Repository Adapter입니다.
/// lock은 Count와 snapshot 읽기가 동시에 일어나도 List의 내부 상태가 깨지지 않게 합니다.
/// </summary>
public sealed class InMemoryQuoteRoundRepository : IQuoteRoundRepository
{
    // target-typed new는 왼쪽 object 형식이 분명해 생성자 형식 이름을 생략하는 문법입니다.
    private readonly object _gate = new();
    // collection expression []은 목표 List 형식이 분명할 때 빈 collection을 만드는 C# 문법입니다.
    private readonly List<QuoteRoundReport> _reports = [];

    /// <summary>
    /// 현재 저장된 report 수를 thread-safe하게 읽습니다.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _reports.Count;
            }
        }
    }

    /// <summary>
    /// 취소를 확인한 뒤 완성 report를 process 메모리에 추가합니다.
    /// </summary>
    /// <param name="report">모든 supplier Task가 끝난 뒤 만들어진 불변 report입니다.</param>
    /// <param name="cancellationToken">저장 전에 호출자가 보낼 수 있는 취소 신호입니다.</param>
    /// <returns>메모리 저장이 끝난 상태의 Task를 반환합니다.</returns>
    public Task SaveAsync(QuoteRoundReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _reports.Add(report);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 테스트와 demo 관찰을 위해 저장된 report의 독립 배열 snapshot을 만듭니다.
    /// </summary>
    /// <returns>호출자가 바꿔도 Repository 내부 List에 영향을 주지 않는 배열을 반환합니다.</returns>
    public QuoteRoundReport[] Snapshot()
    {
        lock (_gate)
        {
            return _reports.ToArray();
        }
    }
}
