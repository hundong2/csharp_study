/*
실행: dotnet script 05_AspNetCoreConcepts.csx
선행 문서: 00-csharp-primer.md, 04-aspnet-core.md
목표: ASP.NET package 없이 async validation, CSRF, union result, cancellation의 원리를 재현합니다.
*/

#nullable enable

// 01. 기본 형식과 StringComparison을 사용합니다.
using System;
// 02. 비동기 API와 CancellationToken을 사용합니다.
using System.Threading;
using System.Threading.Tasks;

// 03. HTTP 요청에서 필요한 최소 정보만 담는 학습용 모델입니다.
public sealed class RequestInfo
{
    // 04. Method는 GET/POST 같은 HTTP method입니다.
    public string Method { get; init; } = "GET";
    // 05. Origin은 요청을 시작한 origin입니다.
    public string? Origin { get; init; }
    // 06. SecFetchSite는 same-origin/cross-site 같은 browser fetch metadata입니다.
    public string? SecFetchSite { get; init; }
}

// 07. C# 15 union의 안정 문법 등가 base type입니다.
public abstract class ApiResult { }
// 08. 성공 case는 payload를 가집니다.
public sealed class Accepted : ApiResult
{
    // 09. constructor가 message를 저장합니다.
    public Accepted(string message) => Message = message;
    // 10. public getter는 response serializer가 읽을 수 있습니다.
    public string Message { get; }
}
// 11. 실패 case는 오류 이유를 가집니다.
public sealed class Rejected : ApiResult
{
    // 12. constructor가 reason을 저장합니다.
    public Rejected(string reason) => Reason = reason;
    // 13. 오류 payload입니다.
    public string Reason { get; }
}

// 14. framework 기능의 판단 핵심을 작은 pure function으로 분리합니다.
public static class WebRules
{
    // 15. GET/HEAD/OPTIONS는 일반적으로 safe method로 분류합니다.
    public static bool IsSafeMethod(string method)
    {
        // 16. 대소문자를 무시하고 GET인지 검사합니다.
        bool isGet = method.Equals("GET", StringComparison.OrdinalIgnoreCase);
        // 17. 같은 방식으로 HEAD를 검사합니다.
        bool isHead = method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
        // 18. 같은 방식으로 OPTIONS를 검사합니다.
        bool isOptions = method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase);
        // 19. `||`는 세 조건 중 하나라도 true면 true를 반환합니다.
        return isGet || isHead || isOptions;
    }

    // 20. cross-site의 unsafe browser 요청을 차단하는 단순화된 학습 규칙입니다.
    public static bool ShouldRejectForCsrf(RequestInfo request)
    {
        // 21. safe method는 state 변경을 하지 않는다는 HTTP 계약을 전제로 허용합니다.
        if (IsSafeMethod(request.Method)) return false;
        // 22. Preview 6 실제 구현은 Origin 등 더 많은 조건과 예외를 함께 평가합니다.
        return string.Equals(request.SecFetchSite, "cross-site", StringComparison.OrdinalIgnoreCase);
    }

    // 23. I/O 검증을 async로 표현해 thread를 block하지 않습니다.
    public static async Task<ApiResult> ValidateEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        // 24. 실제 DB/API 조회 대신 취소 가능한 delay를 사용합니다.
        await Task.Delay(30, cancellationToken).ConfigureAwait(false);
        // 25. 이미 등록된 주소면 rejected case를 만듭니다.
        if (email.Equals("used@example.com", StringComparison.OrdinalIgnoreCase))
            return new Rejected("이미 등록된 이메일입니다.");
        // 26. 나머지는 accepted case입니다.
        return new Accepted("예약을 만들 수 있습니다.");
    }

    // 27. SignalR hub method처럼 token을 server 작업에 전파합니다.
    public static async Task LongHubWorkAsync(CancellationToken cancellationToken)
    {
        // 28. token이 취소되면 Task.Delay가 OperationCanceledException으로 협력적 중단합니다.
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
    }
}

// 29. cross-origin POST 요청을 구성합니다.
var request = new RequestInfo { Method = "POST", Origin = "https://evil.example", SecFetchSite = "cross-site" };
// 30. 자동 CSRF 판단의 핵심 결과를 확인합니다.
Console.WriteLine($"CSRF reject = {WebRules.ShouldRejectForCsrf(request)}");

// 31. using block은 요청 lifetime이 끝날 때 cancellation source를 dispose합니다.
using (var validationCts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
{
    // 32. 비동기 검증 결과는 union 등가 형식으로 돌아옵니다.
    ApiResult validation = await WebRules.ValidateEmailAsync("used@example.com", validationCts.Token);
    // 33. case별 response 모양을 pattern matching합니다.
    string response = validation switch
    {
        Accepted ok => $"200: {ok.Message}",       // 성공 case는 HTTP 200 형태로 투영합니다.
        Rejected bad => $"400: {bad.Reason}",      // 실패 case는 HTTP 400 형태로 투영합니다.
        _ => "500: unknown result"                 // 알 수 없는 case는 방어적으로 500 처리합니다.
    };
    // 34. endpoint가 만들 법한 상태/본문을 출력합니다.
    Console.WriteLine(response);
}

// 35. 두 번째 using block은 client 취소 source의 수명을 관리합니다.
using (var hubCts = new CancellationTokenSource(50))
{
    try
    {
        // 36. client token이 server method의 token parameter까지 전달됐다고 가정합니다.
        await WebRules.LongHubWorkAsync(hubCts.Token);
    }
    catch (OperationCanceledException)
    {
        // 37. 취소는 오류 로그보다 정상적인 요청 종료로 분류할 수 있습니다.
        Console.WriteLine("hub invocation cancelled cooperatively");
    }
}
