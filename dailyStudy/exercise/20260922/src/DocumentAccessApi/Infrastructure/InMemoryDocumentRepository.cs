using System.Collections.Concurrent;
using DocumentAccessApi.Application.Ports;
using DocumentAccessApi.Domain;

namespace DocumentAccessApi.Infrastructure;

/// <summary>
/// 학습과 자체 테스트를 위해 프로세스 메모리에 문서를 저장하는 Repository Adapter입니다.
/// 운영 환경에서는 영속 DB 구현으로 교체해야 합니다.
/// </summary>
public sealed class InMemoryDocumentRepository : IDocumentRepository
{
    // ConcurrentDictionary는 여러 요청이 동시에 접근해도 내부 자료구조가 깨지지 않도록 동시성 제어를 제공합니다.
    private readonly ConcurrentDictionary<Guid, ProjectDocument> _documents = new();

    /// <summary>
    /// 식별자 중복을 허용하지 않고 문서를 메모리에 추가합니다.
    /// </summary>
    /// <param name="document">검증을 통과한 저장 대상 문서입니다.</param>
    /// <param name="cancellationToken">저장 전에 중단 여부를 확인할 토큰입니다.</param>
    /// <returns>메모리 저장이 끝난 완료 Task를 반환합니다.</returns>
    public Task AddAsync(ProjectDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_documents.TryAdd(document.Id, document))
        {
            // GUID 충돌은 사용자가 고칠 입력 오류가 아니라 시스템 불변식 위반이므로 예외로 드러냅니다.
            throw new InvalidOperationException($"문서 ID가 중복되었습니다: {document.Id}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 식별자에 해당하는 문서를 메모리 저장소에서 찾습니다.
    /// </summary>
    /// <param name="id">조회할 문서 식별자입니다.</param>
    /// <param name="cancellationToken">조회 전에 중단 여부를 확인할 토큰입니다.</param>
    /// <returns>찾은 문서 또는 null을 Task에 담아 반환합니다.</returns>
    public Task<ProjectDocument?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _documents.TryGetValue(id, out var document);

        // Task.FromResult는 실제 I/O가 없는 학습용 Adapter도 비동기 Port 계약을 구현하게 해 줍니다.
        return Task.FromResult(document);
    }
}
