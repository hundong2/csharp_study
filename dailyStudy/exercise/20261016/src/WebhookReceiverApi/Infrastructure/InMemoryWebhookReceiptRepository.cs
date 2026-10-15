using System.Collections.Concurrent;
using WebhookReceiverApi.Application.Ports;
using WebhookReceiverApi.Domain;

namespace WebhookReceiverApi.Infrastructure;

/// <summary>
/// 프로세스 메모리에 접수 결과를 보관하는 학습용 Repository Adapter입니다.
/// 운영에서는 여러 instance가 공유하고 만료 정책을 적용할 수 있는 데이터베이스의 unique key/transaction으로 교체해야 합니다.
/// </summary>
public sealed class InMemoryWebhookReceiptRepository : IWebhookReceiptRepository
{
    // ConcurrentDictionary.TryAdd는 같은 key에 대한 확인과 추가를 한 원자적 연산으로 수행해 동시 replay를 막습니다.
    // new(...)는 왼쪽 ConcurrentDictionary 형식을 다시 쓰지 않는 target-typed new 문법입니다.
    private readonly ConcurrentDictionary<string, WebhookReceipt> _receipts =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 같은 event ID가 없을 때만 접수 결과를 원자적으로 저장합니다.
    /// </summary>
    /// <param name="receipt">인증과 업무 검증을 끝낸 접수 결과입니다.</param>
    /// <param name="cancellationToken">호출자가 작업 중단을 요청했는지 확인할 신호입니다.</param>
    /// <returns>처음 저장했으면 true, 이미 있으면 false인 완료된 Task를 반환합니다.</returns>
    public Task<bool> TryAddAsync(
        WebhookReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            _receipts.TryAdd(receipt.EventId.Value, receipt));
    }

    /// <summary>
    /// 대소문자를 구분하는 정확한 event ID로 접수 결과를 찾습니다.
    /// </summary>
    /// <param name="eventId">문법 검증을 마친 조회 키입니다.</param>
    /// <param name="cancellationToken">호출자가 작업 중단을 요청했는지 확인할 신호입니다.</param>
    /// <returns>접수 결과 또는 없음을 뜻하는 null이 든 완료된 Task를 반환합니다.</returns>
    public Task<WebhookReceipt?> FindAsync(
        WebhookEventId eventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        cancellationToken.ThrowIfCancellationRequested();
        _receipts.TryGetValue(eventId.Value, out var receipt);
        return Task.FromResult(receipt);
    }
}
