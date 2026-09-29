using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OrderIntakeApi.ErrorHandling;

/// <summary>
/// 이 JSON API의 오류 계약을 Accept header와 무관하게 application/problem+json으로 기록합니다.
/// </summary>
public sealed class JsonProblemDetailsWriter : IProblemDetailsWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);
    private readonly ProblemDetailsOptions _problemDetailsOptions;

    /// <summary>
    /// Program에 등록한 공통 CustomizeProblemDetails callback을 writer에서도 재사용합니다.
    /// </summary>
    /// <param name="problemDetailsOptions">DI가 제공한 Problem Details 공통 설정입니다.</param>
    /// <returns>생성자는 writer를 초기화하므로 별도 반환값은 없습니다.</returns>
    public JsonProblemDetailsWriter(IOptions<ProblemDetailsOptions> problemDetailsOptions)
    {
        ArgumentNullException.ThrowIfNull(problemDetailsOptions);
        _problemDetailsOptions = problemDetailsOptions.Value;
    }

    /// <summary>
    /// 이 애플리케이션의 모든 Problem Details를 같은 JSON writer가 책임진다고 알립니다.
    /// </summary>
    /// <param name="context">작성할 Problem Details와 현재 HTTP 문맥입니다.</param>
    /// <returns>항상 true를 반환해 Result, exception, status page에 같은 계약을 적용합니다.</returns>
    public bool CanWrite(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return true;
    }

    /// <summary>
    /// 공통 customization이 끝난 Problem Details를 표준 JSON media type으로 직렬화합니다.
    /// </summary>
    /// <param name="context">작성할 Problem Details와 응답 객체입니다.</param>
    /// <returns>응답 쓰기가 끝났을 때 완료되는 ValueTask를 반환합니다.</returns>
    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // custom writer는 기본 writer를 우회하므로 공통 instance/code/traceId callback을 직접 적용합니다.
        _problemDetailsOptions.CustomizeProblemDetails?.Invoke(context);
        var response = context.HttpContext.Response;
        // 명시한 content type은 text/plain이나 application/xml Accept가 와도 API 오류 모양이 바뀌지 않게 합니다.
        var writeTask = response.WriteAsJsonAsync(
            context.ProblemDetails,
            SerializerOptions,
            contentType: "application/problem+json",
            context.HttpContext.RequestAborted);
        return new ValueTask(writeTask);
    }
}
