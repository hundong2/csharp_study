using OrderIntakeApi.Domain;
using OrderIntakeApi.Infrastructure;

namespace OrderIntakeApi.Presentation;

/// <summary>
/// Development에서만 오류 경계를 직접 관찰하게 하는 장애 주입 endpoint입니다.
/// </summary>
public static class DemoCatalogEndpoints
{
    /// <summary>
    /// 카탈로그를 healthy, unavailable, bug 상태로 바꾸는 학습용 route를 등록합니다.
    /// </summary>
    /// <param name="endpoints">route를 추가할 endpoint builder입니다.</param>
    /// <returns>같은 builder를 반환합니다.</returns>
    public static IEndpointRouteBuilder MapDemoCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPut("/demo/catalog/{behavior}", ChangeBehavior);
        return endpoints;
    }

    /// <summary>
    /// 경로 문자열을 허용된 학습 상태로 바꿔 카탈로그 Adapter에 적용합니다.
    /// </summary>
    /// <param name="behavior">healthy, unavailable, bug 중 하나인 경로 값입니다.</param>
    /// <param name="catalog">상태를 바꿀 singleton 학습 Adapter입니다.</param>
    /// <returns>성공 상태 또는 400 Problem Details를 반환합니다.</returns>
    private static IResult ChangeBehavior(string behavior, DemoProductCatalog catalog)
    {
        // switch expression과 or pattern으로 공개 허용 목록만 명시해 임의 enum 숫자 입력을 막습니다.
        var parsed = behavior.ToLowerInvariant() switch
        {
            "healthy" => CatalogBehavior.Healthy,
            "unavailable" => CatalogBehavior.Unavailable,
            "bug" => CatalogBehavior.Bug,
            _ => (CatalogBehavior?)null
        };

        if (parsed is null)
        {
            return OrderHttpMapper.ToProblemResult(
                new Error("order.demo_behavior.invalid", "behavior는 healthy, unavailable, bug 중 하나여야 합니다."));
        }

        // .Value는 nullable enum에 실제 값이 있음을 바로 앞의 null 검사로 확인한 뒤 꺼냅니다.
        catalog.SetBehavior(parsed.Value);
        return Results.Ok(new { behavior = parsed.Value.ToString() });
    }
}
