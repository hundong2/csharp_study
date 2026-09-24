namespace AnnouncementCacheApi.Domain;

/// <summary>
/// 검증을 통과한 공지 한 건을 나타내는 불변 Domain Model입니다.
/// </summary>
// sealed record를 사용하면 상속으로 검증 규칙이 우회되지 않고, 생성 뒤 값이 바뀌지 않아 캐시와 동시 읽기에 안전합니다.
public sealed record Announcement
{
    public Guid Id { get; }

    public string Title { get; }

    public string Body { get; }

    public string Category { get; }

    public DateTimeOffset PublishedAtUtc { get; }

    /// <summary>
    /// 팩터리가 검증하고 정규화한 값만 받아 불변 공지를 만듭니다.
    /// </summary>
    /// <param name="id">비어 있지 않은 공지 식별자입니다.</param>
    /// <param name="title">앞뒤 공백이 제거된 제목입니다.</param>
    /// <param name="body">앞뒤 공백이 제거된 본문입니다.</param>
    /// <param name="category">소문자로 정규화된 카테고리입니다.</param>
    /// <param name="publishedAtUtc">UTC로 정규화된 게시 시각입니다.</param>
    /// <returns>생성자는 값을 반환하지 않고 검증 완료 값을 새 불변 객체에 보관합니다.</returns>
    private Announcement(
        Guid id,
        string title,
        string body,
        string category,
        DateTimeOffset publishedAtUtc)
    {
        Id = id;
        Title = title;
        Body = body;
        Category = category;
        PublishedAtUtc = publishedAtUtc;
    }

    /// <summary>
    /// 외부 입력을 검증·정규화하여 안전한 Announcement를 생성합니다.
    /// </summary>
    /// <param name="id">저장과 URL에 사용할 식별자이며 Guid.Empty일 수 없습니다.</param>
    /// <param name="title">필수 제목이며 공백 제거 후 1~100자여야 합니다.</param>
    /// <param name="body">필수 본문이며 공백 제거 후 1~1000자여야 합니다.</param>
    /// <param name="category">영문자, 숫자, 하이픈으로 이루어진 1~30자 카테고리입니다.</param>
    /// <param name="publishedAtUtc">공지 게시 시각이며 내부에서 UTC로 바뀝니다.</param>
    /// <returns>모든 규칙을 통과한 공지 또는 첫 번째 검증 실패를 담은 Result를 반환합니다.</returns>
    public static Result<Announcement> Create(
        Guid id,
        string? title,
        string? body,
        string? category,
        DateTimeOffset publishedAtUtc)
    {
        if (id == Guid.Empty)
        {
            return Result<Announcement>.Failure(
                new DomainError("announcement.id.required", "공지 식별자는 비어 있을 수 없습니다."));
        }

        // ?.는 title이 null이면 Trim을 호출하지 않고 null을 돌려주는 null-conditional 연산자입니다.
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return Result<Announcement>.Failure(
                new DomainError("announcement.title.required", "제목을 입력해 주세요."));
        }

        if (normalizedTitle.Length > 100)
        {
            return Result<Announcement>.Failure(
                new DomainError("announcement.title.too_long", "제목은 100자 이하여야 합니다."));
        }

        var normalizedBody = body?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            return Result<Announcement>.Failure(
                new DomainError("announcement.body.required", "본문을 입력해 주세요."));
        }

        if (normalizedBody.Length > 1000)
        {
            return Result<Announcement>.Failure(
                new DomainError("announcement.body.too_long", "본문은 1000자 이하여야 합니다."));
        }

        var categoryResult = NormalizeCategory(category);
        if (!categoryResult.IsSuccess)
        {
            // !는 바로 위 실패 검사로 Error가 null이 아님을 아는 우리 규칙을 nullable 분석기에 알려 줍니다.
            // runtime 검사를 대신하는 연산자가 아니므로 반드시 IsSuccess 분기 뒤에만 사용합니다.
            return Result<Announcement>.Failure(categoryResult.Error!);
        }

        return Result<Announcement>.Success(
            new Announcement(
                id,
                normalizedTitle,
                normalizedBody,
                categoryResult.Value!,
                publishedAtUtc.ToUniversalTime()));
    }

    /// <summary>
    /// 카테고리 입력을 소문자로 통일하고 허용 문자와 길이를 검사합니다.
    /// </summary>
    /// <param name="category">API나 필터에서 들어온 아직 검증하지 않은 카테고리입니다.</param>
    /// <returns>정상일 때 정규화 문자열, 잘못되었을 때 사용자가 고칠 수 있는 오류 Result를 반환합니다.</returns>
    public static Result<string> NormalizeCategory(string? category)
    {
        var normalized = category?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result<string>.Failure(
                new DomainError("announcement.category.required", "카테고리를 입력해 주세요."));
        }

        if (normalized.Length > 30)
        {
            return Result<string>.Failure(
                new DomainError("announcement.category.too_long", "카테고리는 30자 이하여야 합니다."));
        }

        foreach (var character in normalized)
        {
            if (!IsAllowedCategoryCharacter(character))
            {
                return Result<string>.Failure(
                    new DomainError(
                        "announcement.category.invalid",
                        "카테고리는 영문자, 숫자, 하이픈(-)만 사용할 수 있습니다."));
            }
        }

        return Result<string>.Success(normalized);
    }

    /// <summary>
    /// 카테고리 한 글자가 URL과 캐시 키에 안전한 영문자·숫자·하이픈인지 판정합니다.
    /// </summary>
    /// <param name="character">검사할 단일 문자입니다.</param>
    /// <returns>허용 문자이면 true, 그 밖의 문자이면 false를 반환합니다.</returns>
    private static bool IsAllowedCategoryCharacter(char character)
    {
        // is/and/or 관계 패턴은 한 글자가 세 허용 범위 중 하나에 속하는지 선언적으로 표현합니다.
        return character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
    }
}
