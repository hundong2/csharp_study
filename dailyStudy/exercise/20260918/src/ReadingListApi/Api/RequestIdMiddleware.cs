namespace ReadingListApi.Api;

// Middleware는 엔드포인트 전후에 공통 처리를 넣습니다. 요청 ID가 있으면 클라이언트와 서버 로그를 연결하기 쉽습니다.
public sealed class RequestIdMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// 파이프라인의 다음 처리기를 받습니다. next는 다음 미들웨어를 호출하며 생성자 반환값은 없습니다.
    /// </summary>
    public RequestIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// 요청 ID를 응답 헤더에 넣고 다음 처리기로 전달합니다. context는 현재 HTTP 요청 정보이며 비동기 작업을 반환합니다.
    /// </summary>
    // async는 await를 사용할 수 있는 메서드를 뜻합니다. 다음 처리기의 완료를 기다려야 헤더와 응답 흐름이 이어집니다.
    public async Task InvokeAsync(HttpContext context)
    {
        // TraceIdentifier는 ASP.NET Core가 요청마다 제공하는 ID입니다. 응답에 싣기만 하므로 외부 입력을 신뢰할 필요가 없습니다.
        context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
        // await는 다음 처리기가 끝날 때까지 기다리되 스레드를 붙잡지 않습니다. 요청 처리 성능을 위해 사용합니다.
        await _next(context);
    }
}
