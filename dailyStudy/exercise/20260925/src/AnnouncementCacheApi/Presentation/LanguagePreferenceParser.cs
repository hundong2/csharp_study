using System.Globalization;
using Microsoft.Extensions.Primitives;

namespace AnnouncementCacheApi.Presentation;

/// <summary>
/// HTTP Accept-Language를 애플리케이션이 지원하는 <c>ko</c>/<c>en</c>으로 바꿉니다.
/// </summary>
public static class LanguagePreferenceParser
{
    private const string DefaultLanguage = "ko";

    /// <summary>
    /// 언어 범위와 q 품질값을 읽어 가장 선호도가 높은 지원 언어를 선택합니다.
    /// </summary>
    /// <param name="headerValues">요청의 Accept-Language 헤더 전체 값입니다.</param>
    /// <returns>지원되는 <c>ko</c> 또는 <c>en</c>, 선택할 수 없으면 기본 <c>ko</c>를 반환합니다.</returns>
    public static string Parse(StringValues headerValues)
    {
        if (StringValues.IsNullOrEmpty(headerValues))
        {
            return DefaultLanguage;
        }

        // |는 [Flags] enum 선택지를 합쳐 빈 항목 제거와 앞뒤 공백 제거를 동시에 요청하는 비트 OR입니다.
        var rawEntries = headerValues.ToString().Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var candidates = new List<LanguageCandidate>();

        for (var order = 0; order < rawEntries.Length; order++)
        {
            var candidate = ParseCandidate(rawEntries[order], order);
            if (candidate is not null && candidate.Quality > 0)
            {
                candidates.Add(candidate);
            }
        }

        candidates.Sort(CompareCandidates);
        return candidates.Count > 0 ? candidates[0].Language : DefaultLanguage;
    }

    /// <summary>
    /// Accept-Language의 쉼표로 나뉜 항목 하나에서 언어와 선택적인 q 값을 읽습니다.
    /// </summary>
    /// <param name="rawEntry"><c>en-US;q=0.9</c>처럼 한 언어 범위를 담은 문자열입니다.</param>
    /// <param name="order">q 값이 같을 때 먼저 적힌 항목을 우선하기 위한 0부터 시작하는 순서입니다.</param>
    /// <returns>지원 언어 후보이며 미지원 또는 잘못된 항목이면 null을 반환합니다.</returns>
    private static LanguageCandidate? ParseCandidate(string rawEntry, int order)
    {
        var segments = rawEntry.Split(
            ';',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        var language = NormalizeSupportedLanguage(segments[0]);
        if (language is null)
        {
            return null;
        }

        var quality = 1.0;
        for (var index = 1; index < segments.Length; index++)
        {
            if (!segments[index].StartsWith("q=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // [2..]는 index 2부터 끝까지 잘라 새 문자열을 만드는 range 문법입니다.
            // "q=" 접두사를 제거하고 숫자 부분만 parser에 전달하려고 사용합니다.
            var qualityText = segments[index][2..];
            if (!double.TryParse(
                    qualityText,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out quality) ||
                quality is < 0 or > 1)
            {
                return null;
            }
        }

        return new LanguageCandidate(language, quality, order);
    }

    /// <summary>
    /// 지역 태그를 포함한 언어 범위를 현재 지원하는 짧은 코드로 정규화합니다.
    /// </summary>
    /// <param name="languageRange"><c>ko-KR</c>, <c>en-US</c> 같은 HTTP 언어 범위입니다.</param>
    /// <returns>지원하면 <c>ko</c> 또는 <c>en</c>, 그 밖에는 null을 반환합니다.</returns>
    private static string? NormalizeSupportedLanguage(string languageRange)
    {
        if (languageRange.Equals("ko", StringComparison.OrdinalIgnoreCase) ||
            languageRange.StartsWith("ko-", StringComparison.OrdinalIgnoreCase))
        {
            return "ko";
        }

        if (languageRange.Equals("en", StringComparison.OrdinalIgnoreCase) ||
            languageRange.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
        {
            return "en";
        }

        return null;
    }

    /// <summary>
    /// 품질값은 큰 순서, 품질이 같으면 헤더에 먼저 나온 순서로 후보를 정렬합니다.
    /// </summary>
    /// <param name="left">왼쪽 언어 후보입니다.</param>
    /// <param name="right">오른쪽 언어 후보입니다.</param>
    /// <returns>left가 우선이면 음수, 같으면 0, right가 우선이면 양수를 반환합니다.</returns>
    private static int CompareCandidates(LanguageCandidate left, LanguageCandidate right)
    {
        var qualityComparison = right.Quality.CompareTo(left.Quality);
        return qualityComparison != 0 ? qualityComparison : left.Order.CompareTo(right.Order);
    }

    /// <summary>
    /// 정렬할 수 있도록 언어, 품질, 원래 위치를 묶는 내부 불변 값입니다.
    /// </summary>
    /// <param name="Language">정규화된 지원 언어입니다.</param>
    /// <param name="Quality">HTTP q 품질값입니다.</param>
    /// <param name="Order">헤더에 나타난 원래 순서입니다.</param>
    private sealed record LanguageCandidate(string Language, double Quality, int Order);
}
