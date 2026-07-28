/*
실행: dotnet script 07_CapstonePipeline.csx
선행 문서: 02-libraries-runtime.md, 04-aspnet-core.md, 07-exercises.md
목표: 검증 → union 분류 → 비동기 처리 → Activity 추적을 하나의 작은 pipeline으로 묶습니다.
*/

#nullable enable

// 01. 기본 형식을 사용합니다.
using System;
// 02. Activity tracing API를 사용합니다.
using System.Diagnostics;
// 03. JSON 출력을 사용합니다.
using System.Text.Json;
// 04. cancellation과 async를 사용합니다.
using System.Threading;
using System.Threading.Tasks;

// 05. 입력 DTO입니다.
public sealed class WorkRequest
{
    // 06. 사용자 key입니다.
    public string Email { get; init; } = "";
    // 07. 처리할 수량입니다.
    public int Quantity { get; init; }
}

// 08. 결과 case hierarchy의 base입니다.
public abstract class WorkResult { }
// 09. 성공 case입니다.
public sealed class WorkAccepted : WorkResult
{
    // 10. 처리 ID를 저장합니다.
    public WorkAccepted(string id) => Id = id;
    // 11. 결과 payload입니다.
    public string Id { get; }
}
// 12. 검증 실패 case입니다.
public sealed class WorkRejected : WorkResult
{
    // 13. 오류 내용을 저장합니다.
    public WorkRejected(string error) => Error = error;
    // 14. 오류 payload입니다.
    public string Error { get; }
}

// 15. application service를 static helper로 단순화합니다.
public static class WorkPipeline
{
    // 16. source name은 Preview 6 tracing rule의 선택 기준이 될 수 있습니다.
    private static readonly ActivitySource Source = new ActivitySource("Study.WorkPipeline");

    // 17. Task<WorkResult>는 비동기 완료와 union 등가 결과를 함께 표현합니다.
    public static async Task<WorkResult> ExecuteAsync(
        WorkRequest request,
        CancellationToken cancellationToken)
    {
        // 18. listener가 sampling하면 Activity/span이 생성됩니다.
        using Activity? activity = Source.StartActivity("Execute");
        // 19. email 원문 대신 domain만 tag해 민감 정보 노출을 줄입니다.
        activity?.SetTag("request.email_domain", GetDomain(request.Email));
        // 20. quantity는 low-cardinality numeric tag입니다.
        activity?.SetTag("request.quantity", request.Quantity);

        // 21. 기본 문법 검증은 I/O 전에 빠르게 실패시킵니다.
        if (!request.Email.Contains("@", StringComparison.Ordinal))
            return new WorkRejected("email 형식이 잘못되었습니다.");
        // 22. business invariant를 확인합니다.
        if (request.Quantity <= 0)
            return new WorkRejected("quantity는 1 이상이어야 합니다.");

        // 23. DB/API I/O를 흉내 내며 요청 취소를 끝까지 전달합니다.
        await Task.Delay(40, cancellationToken).ConfigureAwait(false);
        // 24. Preview 6 async validator라면 endpoint 진입 전에 이 종류의 I/O 규칙을 실행할 수 있습니다.
        if (request.Email.Equals("blocked@example.com", StringComparison.OrdinalIgnoreCase))
            return new WorkRejected("차단된 계정입니다.");

        // 25. 실제 시스템에서는 DB가 만든 식별자를 사용합니다.
        string id = Guid.NewGuid().ToString("N");
        // 26. 성공 case를 반환합니다.
        return new WorkAccepted(id);
    }

    // 27. telemetry의 개인정보를 줄이는 helper입니다.
    private static string GetDomain(string email)
    {
        // 28. 마지막 @ 위치를 찾습니다.
        int at = email.LastIndexOf('@');
        // 29. 유효 위치면 뒤쪽 domain, 아니면 invalid marker를 반환합니다.
        return at >= 0 && at + 1 < email.Length ? email[(at + 1)..] : "invalid";
    }
}

// 30. ActivitySource가 실제 Activity를 만들도록 모든 data를 sampling하는 listener입니다.
using (var listener = new ActivityListener
{
    // 31. 이 학습 source만 listen합니다.
    ShouldListenTo = source => source.Name == "Study.WorkPipeline",
    // 32. 모든 데이터를 수집하도록 sampling result를 정합니다.
    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
})
{
    // 33. process-wide listener를 등록합니다.
    ActivitySource.AddActivityListener(listener);

    // 34. 유효한 입력을 만듭니다.
    var request = new WorkRequest { Email = "learner@example.com", Quantity = 2 };
    // 35. 요청에 1초 timeout budget을 주고 block 끝에서 dispose합니다.
    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
    {
        // 36. pipeline 완료를 비동기로 기다립니다.
        WorkResult result = await WorkPipeline.ExecuteAsync(request, cts.Token);
        // 37. active case를 JSON에 적합한 anonymous object로 투영합니다.
        object response = result switch
        {
            WorkAccepted ok => new { status = 202, id = ok.Id },          // 성공 case를 202 응답으로 바꿉니다.
            WorkRejected bad => new { status = 400, error = bad.Error },  // 검증 실패는 400 응답으로 바꿉니다.
            _ => new { status = 500, error = "unknown" }                  // 예상 밖 case는 500으로 방어합니다.
        };
        // 38. web response와 비슷한 JSON을 출력합니다.
        Console.WriteLine(JsonSerializer.Serialize(response));
    }
}
