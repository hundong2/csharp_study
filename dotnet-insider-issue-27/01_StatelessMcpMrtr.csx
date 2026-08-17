// 실행: dotnet script 01_StatelessMcpMrtr.csx
// 목적: session 없이 self-contained request와 MRTR input_required 재요청을 모형화한다.

// 01. nullable 참조 계약을 compiler가 검사하게 한다.
#nullable enable
// 02. Console과 기본 형식을 가져온다.
using System;
// 03. Dictionary로 input response를 표현한다.
using System.Collections.Generic;
// 04. HMAC으로 opaque state 위조를 검출한다.
using System.Security.Cryptography;
// 05. byte와 string 사이 UTF-8 변환을 사용한다.
using System.Text;

// 06. 요청은 tool, ticket, 선택 reason, 선택 response/state를 모두 payload에 담는다.
record ToolRequest(string Tool, long TicketId, string? CloseReason,
    IReadOnlyDictionary<string, string>? InputResponses, string? RequestState);
// 07. 응답은 completed 또는 input_required 중 한 shape를 표현한다.
record ToolResponse(string ResultType, string Message, string? RequestState);

// 08. 교육용 HMAC key다. 실제 key는 secret manager에서 rotation하며 가져온다.
byte[] stateKey = Encoding.UTF8.GetBytes("demo-key-never-use-in-production");

// 09. ticket ID와 expiry를 서명해 client가 해석할 필요 없는 state를 만든다.
static string ProtectState(long ticketId, long expiresUnix, byte[] key)
{
    // 10. payload는 server가 재요청에서 복원할 최소 상태다.
    string payload = $"{ticketId}:{expiresUnix}";
    // 11. HMACSHA256은 공유 secret으로 payload 무결성 tag를 계산한다.
    using HMACSHA256 hmac = new(key);
    // 12. UTF-8 byte에 대한 서명을 URL-safe하지 않은 Base64 모형으로 만든다.
    string signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    // 13. payload와 signature를 함께 반환한다.
    return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}|{signature}"));
}

// 14. state signature, ticket binding, expiry를 모두 검사한다.
static bool ValidateState(string protectedState, long expectedTicket, long nowUnix, byte[] key)
{
    // 15. 외부 입력 parsing은 예외가 날 수 있으므로 실패를 false로 바꾼다.
    try
    {
        // 16. Base64를 원문으로 복원하고 구분자로 둘로 나눈다.
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(protectedState));
        // 17. 마지막 구분자 앞은 payload, 뒤는 signature다.
        int separator = decoded.LastIndexOf('|');
        // 18. 구분자가 없으면 형식이 잘못됐다.
        if (separator < 0) return false;
        // 19. payload를 잘라 ticket과 expiry를 얻는다.
        string payload = decoded[..separator];
        // 20. signature 문자열을 byte로 복원한다.
        byte[] supplied = Convert.FromBase64String(decoded[(separator + 1)..]);
        // 21. 같은 key와 payload로 기대 서명을 다시 계산한다.
        using HMACSHA256 hmac = new(key);
        // 22. fixed-time 비교는 timing side channel을 줄인다.
        if (!CryptographicOperations.FixedTimeEquals(supplied, hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))) return false;
        // 23. payload의 두 숫자를 parsing한다.
        string[] parts = payload.Split(':');
        // 24. field 수·ticket binding·expiry를 모두 확인한다.
        return parts.Length == 2 && long.Parse(parts[0]) == expectedTicket && long.Parse(parts[1]) >= nowUnix;
    }
    // 25. 잘못된 Base64/숫자는 server error 대신 invalid state로 처리한다.
    catch (FormatException) { return false; }
    // 25-1. 숫자가 long 범위를 넘는 입력도 invalid state로 처리한다.
    catch (OverflowException) { return false; }
}

// 26. tool은 요청 하나만으로 처리되며 숨은 session dictionary가 없다.
ToolResponse CloseTicket(ToolRequest request)
{
    // 27. reason이 처음부터 있으면 추가 round trip 없이 완료한다.
    if (!string.IsNullOrWhiteSpace(request.CloseReason))
        return new("completed", $"closed {request.TicketId}: {request.CloseReason}", null);

    // 28. response와 state가 함께 왔다면 재요청 경로다.
    if (request.InputResponses is not null && request.RequestState is not null)
    {
        // 29. state가 위조·만료·다른 ticket이면 거부한다.
        if (!ValidateState(request.RequestState, request.TicketId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), stateKey))
            return new("failed", "invalid request state", null);
        // 30. TryGetValue로 사용자 입력을 안전하게 꺼낸다.
        if (request.InputResponses.TryGetValue("closeReason", out string? reason) && !string.IsNullOrWhiteSpace(reason))
            return new("completed", $"closed {request.TicketId}: {reason}", null);
    }

    // 31. 입력이 없으면 5분 유효한 opaque state와 input_required를 반환한다.
    string state = ProtectState(request.TicketId, DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(), stateKey);
    // 32. client는 state를 그대로 echo하고 사용자에게 reason을 물어야 한다.
    return new("input_required", "provide closeReason", state);
}

// 33. 첫 요청에는 session ID도 reason도 없다.
ToolRequest firstRequest = new("close_support_ticket", 1234, null, null, null);
// 34. server는 input_required를 반환한다.
ToolResponse firstResponse = CloseTicket(firstRequest);
// 35. 첫 결과를 확인한다.
Console.WriteLine($"round1 = {firstResponse.ResultType}: {firstResponse.Message}");
// 36. client가 사용자 입력과 opaque state를 같은 tool call에 담아 다시 보낸다.
ToolRequest secondRequest = new(firstRequest.Tool, firstRequest.TicketId, null,
    new Dictionary<string, string> { ["closeReason"] = "resolved" }, firstResponse.RequestState);
// 37. server가 서명을 검증하고 작업을 완료한다.
ToolResponse secondResponse = CloseTicket(secondRequest);
// 38. terminal 결과를 출력한다.
Console.WriteLine($"round2 = {secondResponse.ResultType}: {secondResponse.Message}");

// CLR/JIT 관찰 메모
// - record와 Dictionary, UTF-8/Base64 string/byte[]는 heap allocation과 GC pressure를 만든다.
// - HMAC 구현은 native/managed 최적화 경로를 사용할 수 있으며 fixed-time 비교는 보안 의미가 있다.
// - stateless transport여도 각 request object와 crypto state는 process memory에 잠시 존재한다.
// - 실제 ASP.NET Core에서는 async network I/O와 cancellation/size limit도 적용해야 한다.
