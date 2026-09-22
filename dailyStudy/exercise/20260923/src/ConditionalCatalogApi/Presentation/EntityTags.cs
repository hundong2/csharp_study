using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace ConditionalCatalogApi.Presentation;

/// <summary>
/// HTTP ETag 문자열의 생성과 조건 헤더 비교를 교체 가능한 Strategy로 정의합니다.
/// endpoint가 version 문자열 규칙과 parser 세부사항을 직접 반복하지 않게 해 SRP와 테스트 용이성을 높입니다.
/// </summary>
public interface IEntityTagCodec
{
    /// <summary>
    /// 양의 Domain 버전을 HTTP strong ETag 문자열로 바꿉니다.
    /// </summary>
    /// <param name="version">ETag에 담을 1 이상의 상품 버전입니다.</param>
    /// <returns>큰따옴표를 포함한 strong ETag 문자열을 반환합니다.</returns>
    string Format(long version);

    /// <summary>
    /// 조건 헤더를 표준 parser로 읽고 현재 버전과 strong 또는 weak 방식으로 비교합니다.
    /// </summary>
    /// <param name="headerValues">If-Match 또는 If-None-Match의 원시 헤더 값들입니다.</param>
    /// <param name="currentVersion">현재 상품 상태의 양의 버전입니다.</param>
    /// <param name="useStrongComparison">true이면 쓰기 보호용 strong 비교, false이면 cache 검증용 weak 비교입니다.</param>
    /// <returns>헤더 없음·잘못된 문법·일치·불일치 중 하나를 반환합니다.</returns>
    EntityTagEvaluation Evaluate(
        StringValues headerValues,
        long currentVersion,
        bool useStrongComparison);
}

/// <summary>
/// 조건 헤더를 평가한 결과를 제한된 네 상태로 나타냅니다.
/// </summary>
public enum EntityTagEvaluation
{
    Missing,
    Invalid,
    Match,
    NoMatch,
}

/// <summary>
/// 상품 버전 N을 <c>"vN"</c> 모양으로 표현하고 ASP.NET Core의 RFC-aware parser로 비교합니다.
/// </summary>
public sealed class VersionEntityTagCodec : IEntityTagCodec
{
    /// <summary>
    /// 상품 버전을 큰따옴표로 감싼 불투명 strong ETag로 만듭니다.
    /// </summary>
    /// <param name="version">ETag에 담을 1 이상의 버전입니다.</param>
    /// <returns>예를 들어 버전 3이면 <c>"v3"</c>를 반환합니다.</returns>
    public string Format(long version)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "ETag 버전은 1 이상이어야 합니다.");
        }

        // $"..."는 문자열 안에 값을 넣는 보간 문법입니다. 버전과 wire format의 관계를 한눈에 보이게 합니다.
        return $"\"v{version}\"";
    }

    /// <summary>
    /// 여러 줄·쉼표 목록·와일드카드를 포함할 수 있는 조건 헤더를 parse하고 현재 ETag와 비교합니다.
    /// </summary>
    /// <param name="headerValues">If-Match 또는 If-None-Match에서 읽은 모든 문자열 값입니다.</param>
    /// <param name="currentVersion">현재 표현의 버전입니다.</param>
    /// <param name="useStrongComparison">쓰기 전제 조건이면 true, GET cache 재검증이면 false입니다.</param>
    /// <returns>헤더 상태와 비교 결과를 나타내는 EntityTagEvaluation을 반환합니다.</returns>
    public EntityTagEvaluation Evaluate(
        StringValues headerValues,
        long currentVersion,
        bool useStrongComparison)
    {
        if (StringValues.IsNullOrEmpty(headerValues))
        {
            return EntityTagEvaluation.Missing;
        }

        // StringValues.ToString은 여러 header line을 쉼표 목록 하나로 합칩니다. parser가 목록 문법을 다시 안전하게 나눕니다.
        var combinedHeader = headerValues.ToString();

        // out var는 메서드가 돌려주는 두 번째 값을 선언과 동시에 받는 문법입니다.
        // 직접 문자열을 split하면 따옴표·공백·목록 문법을 틀리기 쉬워 framework parser를 사용합니다.
        if (!EntityTagHeaderValue.TryParseList([combinedHeader], out var candidates))
        {
            return EntityTagEvaluation.Invalid;
        }

        // HTTP 문법에서 *는 단독 선택지이며 개별 ETag와 같은 목록에 섞을 수 없습니다.
        // framework parser는 각 token을 읽는 데 집중하므로, 목록 전체 규칙은 Strategy가 한 번 더 확인합니다.
        var hasWildcard = candidates.Any(candidate => candidate.Equals(EntityTagHeaderValue.Any));
        if (hasWildcard && candidates.Count != 1)
        {
            return EntityTagEvaluation.Invalid;
        }

        var current = new EntityTagHeaderValue(Format(currentVersion));

        // LINQ Any와 lambda(candidate => ...)는 후보 중 하나라도 맞는지 선언적으로 표현합니다.
        // 조건 헤더는 OR 목록이므로 수동 index와 플래그보다 HTTP 의미가 코드에 바로 드러납니다.
        var isMatch = candidates.Any(candidate =>
            candidate.Equals(EntityTagHeaderValue.Any)
            || current.Compare(candidate, useStrongComparison));

        // ?:는 조건에 따라 두 값 중 하나를 고르는 조건 연산자입니다. 계산된 bool을 결과 enum으로 짧게 바꿉니다.
        return isMatch ? EntityTagEvaluation.Match : EntityTagEvaluation.NoMatch;
    }
}
