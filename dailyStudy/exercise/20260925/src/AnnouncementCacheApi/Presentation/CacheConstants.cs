namespace AnnouncementCacheApi.Presentation;

/// <summary>
/// Endpoint와 Composition Root가 오타 없이 공유할 Output Cache 정책 이름입니다.
/// </summary>
public static class CacheNames
{
    public const string AnnouncementFeedPolicy = "announcement-feed-v1";
}

/// <summary>
/// 여러 query/header 변형을 한 번에 제거하기 위해 공유하는 캐시 태그입니다.
/// </summary>
public static class CacheTags
{
    public const string AnnouncementFeed = "announcement-feed";
}
