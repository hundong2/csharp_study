namespace DocumentAccessApi.Domain;

/// <summary>
/// 권한 판단의 대상(resource)이 되는 프로젝트 문서입니다.
/// record의 값 비교 장점은 유지하되, get 전용 속성과 private 생성자로 factory 검증을 우회하는 변경을 막습니다.
/// </summary>
public sealed record ProjectDocument
{
    /// <summary>
    /// 검증을 마친 값으로만 불변 문서를 초기화합니다. 외부 코드는 Create factory를 사용해야 합니다.
    /// </summary>
    /// <param name="id">서버가 발급한 문서 식별자입니다.</param>
    /// <param name="title">공백을 제거하고 길이를 검증한 제목입니다.</param>
    /// <param name="body">공백을 제거하고 길이를 검증한 본문입니다.</param>
    /// <param name="ownerId">인증된 사용자의 검증된 subject 식별자입니다.</param>
    /// <param name="createdAtUtc">문서가 생성된 UTC 시각입니다.</param>
    private ProjectDocument(
        Guid id,
        string title,
        string body,
        string ownerId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Title = title;
        Body = body;
        OwnerId = ownerId;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>문서를 구분하는 서버 발급 식별자입니다.</summary>
    public Guid Id { get; }

    /// <summary>화면에 표시할 검증된 문서 제목입니다.</summary>
    public string Title { get; }

    /// <summary>인가를 통과한 사용자에게만 보여 줄 검증된 본문입니다.</summary>
    public string Body { get; }

    /// <summary>문서를 만든 사용자의 신뢰된 subject 식별자입니다.</summary>
    public string OwnerId { get; }

    /// <summary>문서가 생성된 UTC 시각입니다.</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>
    /// 외부 입력을 검사하고 유효한 문서만 생성합니다.
    /// </summary>
    /// <param name="id">서버가 발급한 문서 식별자입니다.</param>
    /// <param name="title">공백 제거 전의 사용자 입력 제목입니다.</param>
    /// <param name="body">공백 제거 전의 사용자 입력 본문입니다.</param>
    /// <param name="ownerId">인증된 ClaimsPrincipal에서 꺼낸 사용자 식별자입니다.</param>
    /// <param name="createdAtUtc">테스트 가능한 TimeProvider에서 얻은 생성 시각입니다.</param>
    /// <returns>검증 성공 시 불변 문서, 실패 시 고칠 수 있는 validation 오류를 반환합니다.</returns>
    // string?의 물음표는 JSON 경계에서 값이 빠질 수 있음을 타입으로 드러냅니다. 아래 검사 뒤에는 non-null 값만 Domain에 보관합니다.
    public static Result<ProjectDocument> Create(
        Guid id,
        string? title,
        string? body,
        string? ownerId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            return Result<ProjectDocument>.Failure(
                new Error("DOCUMENT_ID_REQUIRED", "문서 ID는 빈 GUID일 수 없습니다."));
        }

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 100)
        {
            return Result<ProjectDocument>.Failure(
                new Error("TITLE_INVALID", "제목은 UTF-16 코드 단위로 1 이상 100 이하이어야 합니다."));
        }

        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 2_000)
        {
            return Result<ProjectDocument>.Failure(
                new Error("BODY_INVALID", "본문은 UTF-16 코드 단위로 1 이상 2,000 이하이어야 합니다."));
        }

        if (string.IsNullOrWhiteSpace(ownerId) || ownerId.Length > 64)
        {
            return Result<ProjectDocument>.Failure(
                new Error("OWNER_INVALID", "인증된 사용자 식별자가 없거나 너무 깁니다."));
        }

        return Result<ProjectDocument>.Success(
            new ProjectDocument(id, title.Trim(), body.Trim(), ownerId, createdAtUtc));
    }
}
