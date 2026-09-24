using AnnouncementCacheApi.Domain;

namespace AnnouncementCacheApi.Application.Ports;

/// <summary>
/// API가 반환할 언어별 표시값을 모은 불변 Application 모델입니다.
/// </summary>
/// <param name="Id">공지 식별자입니다.</param>
/// <param name="Title">원문의 의미를 보존한 제목입니다.</param>
/// <param name="Body">원문의 의미를 보존한 본문입니다.</param>
/// <param name="Category">캐시 필터에 사용하는 안정적인 카테고리 코드입니다.</param>
/// <param name="CategoryLabel">선택 언어로 표시할 카테고리 이름입니다.</param>
/// <param name="PublishedAtUtc">UTC 게시 시각입니다.</param>
/// <param name="Language">실제로 선택된 <c>ko</c> 또는 <c>en</c> 언어 코드입니다.</param>
public sealed record LocalizedAnnouncement(
    Guid Id,
    string Title,
    string Body,
    string Category,
    string CategoryLabel,
    DateTimeOffset PublishedAtUtc,
    string Language);

/// <summary>
/// 언어별 표시 규칙을 교체 가능한 객체로 캡슐화하는 Strategy Port입니다.
/// </summary>
// Strategy를 추가하면 Application Service의 조건문을 계속 늘리지 않아도 되어 OCP를 지키기 쉽습니다.
public interface IAnnouncementLocalizationStrategy
{
    /// <summary>
    /// 이 전략이 처리하는 정규화 언어 코드입니다.
    /// </summary>
    string LanguageCode { get; }

    /// <summary>
    /// Domain 공지를 이 전략의 언어별 표시 모델로 변환합니다.
    /// </summary>
    /// <param name="announcement">내용과 카테고리가 검증된 원본 공지입니다.</param>
    /// <returns>카테고리 표시명과 실제 언어 코드가 포함된 불변 모델을 반환합니다.</returns>
    LocalizedAnnouncement Localize(Announcement announcement);
}
