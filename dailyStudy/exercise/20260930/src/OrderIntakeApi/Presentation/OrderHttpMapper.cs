using OrderIntakeApi.Domain;

namespace OrderIntakeApi.Presentation;

/// <summary>
/// Application Result를 HTTP 상태와 Problem Details 계약으로 바꾸는 Presentation 전용 mapper입니다.
/// </summary>
public static class OrderHttpMapper
{
    /// <summary>
    /// 주문 접수 성공은 201과 Location으로, 예상 가능한 실패는 알맞은 Problem Details로 바꿉니다.
    /// </summary>
    /// <param name="result">Application Service가 반환한 영수증 또는 오류입니다.</param>
    /// <returns>Minimal API가 실행할 HTTP 결과를 반환합니다.</returns>
    public static IResult ToCreateResult(Result<OrderReceipt> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            var receipt = result.Value!;
            return Results.Created($"/orders/{receipt.OrderId}", receipt);
        }

        return ToProblemResult(result.Error!);
    }

    /// <summary>
    /// 주문 조회 성공은 200으로, 실패는 Problem Details로 바꿉니다.
    /// </summary>
    /// <param name="result">조회된 영수증 또는 오류입니다.</param>
    /// <returns>Minimal API가 실행할 HTTP 결과를 반환합니다.</returns>
    public static IResult ToReadResult(Result<OrderReceipt> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? Results.Ok(result.Value!)
            : ToProblemResult(result.Error!);
    }

    /// <summary>
    /// 안정적인 오류 코드에 따라 400, 404, 409 Problem Details를 만듭니다.
    /// </summary>
    /// <param name="error">Application 또는 Domain에서 만든 예상 가능 오류입니다.</param>
    /// <returns>code 확장 필드를 포함한 Problem Details 결과를 반환합니다.</returns>
    public static IResult ToProblemResult(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var statusCode = error.Code switch
        {
            "catalog.item.not_found" or "order.not_found" => StatusCodes.Status404NotFound,
            "order.duplicate" => StatusCodes.Status409Conflict,
            var code when code.StartsWith("order.", StringComparison.Ordinal) =>
                StatusCodes.Status400BadRequest,
            // 알려지지 않은 Result 코드를 임의의 400으로 숨기지 않아 설계 누락을 중앙 500 경계에서 발견하게 합니다.
            _ => throw new InvalidOperationException($"HTTP 매핑이 없는 오류 코드입니다: {error.Code}")
        };

        var title = statusCode switch
        {
            StatusCodes.Status400BadRequest => "주문 입력을 확인하세요.",
            StatusCodes.Status404NotFound => "요청한 대상을 찾을 수 없습니다.",
            StatusCodes.Status409Conflict => "현재 상태와 요청이 충돌합니다.",
            _ => "요청을 처리할 수 없습니다."
        };

        var extensions = new Dictionary<string, object?>
        {
            ["code"] = error.Code
        };

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: error.Message,
            extensions: extensions);
    }
}
