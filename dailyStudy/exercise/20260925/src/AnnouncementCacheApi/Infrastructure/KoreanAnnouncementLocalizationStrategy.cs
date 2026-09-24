using AnnouncementCacheApi.Application.Ports;
using AnnouncementCacheApi.Domain;

namespace AnnouncementCacheApi.Infrastructure;

/// <summary>
/// 카테고리 코드를 한국어 표시 이름으로 바꾸는 Localization Strategy입니다.
/// </summary>
public sealed class KoreanAnnouncementLocalizationStrategy : IAnnouncementLocalizationStrategy
{
    public string LanguageCode => "ko";

    /// <summary>
    /// 작성자가 입력한 제목과 본문은 보존하고 안정적인 카테고리 코드만 한국어 표시명으로 바꿉니다.
    /// </summary>
    /// <param name="announcement">검증된 원본 공지입니다.</param>
    /// <returns>한국어 카테고리 표시명과 <c>ko</c> 코드를 가진 Application 모델을 반환합니다.</returns>
    public LocalizedAnnouncement Localize(Announcement announcement)
    {
        ArgumentNullException.ThrowIfNull(announcement);

        // switch 식은 값에 맞는 결과 하나를 고르며 _는 새 카테고리가 생겼을 때의 기본 분기입니다.
        var categoryLabel = announcement.Category switch
        {
            "release" => "출시",
            "maintenance" => "점검",
            "general" => "일반",
            _ => "기타"
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
