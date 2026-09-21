using DocumentAccessApi.Application.Ports;
using DocumentAccessApi.Domain;

namespace DocumentAccessApi.Application;

/// <summary>
/// HTTP나 저장 기술과 분리된 문서 생성·조회 유스케이스를 조율하는 Application Service입니다.
/// </summary>
public sealed class DocumentApplicationService
{
    private readonly IDocumentRepository _repository;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 문서 유스케이스에 필요한 저장소와 시계 의존성을 받습니다.
    /// </summary>
    /// <param name="repository">문서를 저장하고 찾는 Repository Port입니다.</param>
    /// <param name="timeProvider">현재 시각을 테스트에서 고정할 수 있게 해 주는 .NET 시계 추상화입니다.</param>
    public DocumentApplicationService(IDocumentRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 인증된 사용자를 소유자로 삼아 입력을 검증하고 새 문서를 저장합니다.
    /// </summary>
    /// <param name="ownerId">인증 handler가 만든 NameIdentifier claim 값입니다.</param>
    /// <param name="command">사용자가 입력한 제목과 본문입니다.</param>
    /// <param name="cancellationToken">HTTP 연결 종료 같은 중단 요청을 전달하는 토큰입니다.</param>
    /// <returns>성공한 문서 또는 사용자가 고칠 수 있는 validation 오류를 반환합니다.</returns>
    public async Task<Result<ProjectDocument>> CreateAsync(
        string? ownerId,
        CreateDocumentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        // var는 오른쪽 식으로 타입이 명확할 때 중복 표기를 줄이는 문법입니다. 타입이 ProjectDocument의 Result임은 Create 반환형으로 확인할 수 있습니다.
        var creation = ProjectDocument.Create(
            Guid.NewGuid(),
            command.Title,
            command.Body,
            ownerId,
            _timeProvider.GetUtcNow());

        if (!creation.IsSuccess)
        {
            return creation;
        }

        // await는 비동기 저장이 끝날 때까지 thread를 막지 않고 기다립니다. 취소 토큰은 Repository 경계까지 그대로 전달합니다.
        await _repository.AddAsync(creation.Value, cancellationToken);
        return creation;
    }

    /// <summary>
    /// 문서를 먼저 불러와 리소스 기반 인가가 판단할 실제 객체를 제공합니다.
    /// </summary>
    /// <param name="id">조회할 문서 식별자입니다.</param>
    /// <param name="cancellationToken">조회 중단 요청을 전달하는 토큰입니다.</param>
    /// <returns>문서가 있으면 문서, 없으면 null을 반환합니다.</returns>
    public Task<ProjectDocument?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return _repository.FindAsync(id, cancellationToken);
    }
}

/// <summary>
/// HTTP 요청 본문을 Application Service에 전달할 때 사용하는 입력 명령입니다.
/// </summary>
/// <param name="Title">새 문서의 제목입니다.</param>
/// <param name="Body">권한이 있는 사용자에게만 공개할 본문입니다.</param>
public sealed record CreateDocumentCommand(string? Title, string? Body);
