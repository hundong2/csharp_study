using AnnouncementCacheApi.Application.Ports;
using AnnouncementCacheApi.Domain;

namespace AnnouncementCacheApi.Infrastructure;

/// <summary>
/// 카테고리 코드를 영어 표시 이름으로 바꾸는 Localization Strategy입니다.
/// </summary>
public sealed class EnglishAnnouncementLocalizationStrategy : IAnnouncementLocalizationStrategy
{
    public string LanguageCode => "en";

    /// <summary>
    /// 사용자 작성 내용을 임의 번역하지 않고 카테고리 표시명만 영어로 안전하게 지역화합니다.
    /// </summary>
    /// <param name="announcement">검증된 원본 공지입니다.</param>
    /// <returns>영어 카테고리 표시명과 <c>en</c> 코드를 가진 Application 모델을 반환합니다.</returns>
    public LocalizedAnnouncement Localize(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        var categoryLabel = announcement.Category switch
        {
            "release" => "Release",
            "maintenance" => "Maintenance",
            "general" => "General",
            _ => "Other"
        };

        return new LocalizedAnnouncement(
            announcement.Id,
            announcement.Title,
            announcement.Body,
            announcement.Category,
            categoryLabel,
            announcement.PublishedAtUtc,
            LanguageCode);
    }
}
