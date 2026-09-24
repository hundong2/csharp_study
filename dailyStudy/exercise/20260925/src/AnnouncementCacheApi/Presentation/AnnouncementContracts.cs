using AnnouncementCacheApi.Application.Ports;

namespace AnnouncementCacheApi.Presentation;

/// <summary>
/// POST 요청 JSON과 일치하는 외부 입력 계약입니다.
/// </summary>
/// <param name="Title">새 공지 제목이며 Domain 계층에서 필수·길이 검증을 합니다.</param>
/// <param name="Body">새 공지 본문이며 Domain 계층에서 필수·길이 검증을 합니다.</param>
/// <param name="Category">새 공지 카테고리이며 Domain 계층에서 문자·길이 검증을 합니다.</param>
public sealed record CreateAnnouncementRequest(string? Title, string? Body, string? Category);

/// <summary>
/// 공지 한 건을 HTTP JSON으로 내보내는 불변 응답 계약입니다.
/// </summary>
/// <param name="Id">URL에서 다시 상세 조회할 공지 식별자입니다.</param>
/// <param name="Title">작성자가 저장한 제목입니다.</param>
/// <param name="Body">작성자가 저장한 본문입니다.</param>
/// <param name="Category">필터와 캐시 키에 사용하는 안정적인 코드입니다.</param>
/// <param name="CategoryLabel">선택 언어 Strategy가 만든 표시 이름입니다.</param>
/// <param name="PublishedAtUtc">UTC 게시 시각입니다.</param>
/// <param name="Language">실제로 적용된 <c>ko</c> 또는 <c>en</c>입니다.</param>
public sealed record AnnouncementItemResponse(
    Guid Id,
    string Title,
    string Body,
    string Category,
    string CategoryLabel,
    DateTimeOffset PublishedAtUtc,
    string Language)
{
    /// <summary>
    /// Application 모델을 HTTP 전용 응답 계약으로 복사하여 계층 경계를 명확히 합니다.
    /// </summary>
    /// <param name="announcement">언어 전략이 적용된 Application 모델입니다.</param>
    /// <returns>JSON 직렬화에 사용할 AnnouncementItemResponse를 반환합니다.</returns>
    public static AnnouncementItemResponse FromApplication(LocalizedAnnouncement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);
        return new AnnouncementItemResponse(
            announcement.Id,
            announcement.Title,
            announcement.Body,
            announcement.Category,
            announcement.CategoryLabel,
            announcement.PublishedAtUtc,
            announcement.Language);
    }
}

/// <summary>
/// 캐시되는 목록 응답 전체를 나타냅니다.
/// </summary>
/// <param name="Language">목록에 적용된 정규화 언어입니다.</param>
/// <param name="OriginReadNumber">이 body를 만든 실제 원본 Repository 읽기 순번입니다.</param>
/// <param name="Items">언어와 카테고리 변형에 해당하는 공지들입니다.</param>
public sealed record AnnouncementFeedResponse(
    string Language,
    long OriginReadNumber,
    IReadOnlyList<AnnouncementItemResponse> Items);

/// <summary>
/// 예상 가능한 API 실패를 기계용 코드와 사람용 메시지로 제공합니다.
/// </summary>
/// <param name="Code">클라이언트가 분기 처리할 안정적인 실패 코드입니다.</param>
/// <param name="Message">입력을 어떻게 고칠지 알려 주는 설명입니다.</param>
public sealed record ApiProblemResponse(string Code, string Message);

/// <summary>
/// 캐시 효과를 관찰하기 위한 Repository 진단 응답입니다.
/// </summary>
/// <param name="ReadCount">프로세스 시작 뒤 원본 Repository가 읽기를 시작한 횟수입니다.</param>
public sealed record RepositoryDiagnosticsResponse(long ReadCount);

/// <summary>
/// 호스트가 요청을 처리할 수 있음을 알리는 최소 상태 응답입니다.
/// </summary>
/// <param name="Status"><c>ok</c>이면 프로세스가 정상 응답 중임을 뜻합니다.</param>
public sealed record HealthResponse(string Status);
