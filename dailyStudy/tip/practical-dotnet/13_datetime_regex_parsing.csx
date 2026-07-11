using System;
using System.Text.RegularExpressions;

// DateTimeOffset:
// 날짜/시간과 시간대 오프셋(+09:00)을 함께 표현합니다.
// 서버 간 통신이나 로그에는 DateTime보다 DateTimeOffset이 더 안전한 경우가 많습니다.
string rawDate = "2026-07-11T13:20:00+09:00";

// TryParse:
// 변환에 실패해도 예외를 던지지 않고 false를 반환합니다.
// 외부 입력, 로그, 사용자 입력은 항상 TryParse 계열을 먼저 고려합니다.
if (DateTimeOffset.TryParse(rawDate, out DateTimeOffset timestamp))
{
    // UtcDateTime:
    // 시간대가 다른 값을 UTC 기준으로 맞춰 저장하거나 비교할 때 사용합니다.
    // :O는 ISO 8601 round-trip 포맷입니다.
    Console.WriteLine($"[Date] UTC={timestamp.UtcDateTime:O}");
}

string logLine = "orderId=1001 status=paid amount=25000";

// Regex:
// 문자열에서 특정 패턴을 찾습니다.
// (?<name>...)는 이름 있는 그룹입니다.
// \d+는 숫자 1개 이상, \w+는 문자/숫자/언더스코어 1개 이상을 의미합니다.
var regex = new Regex(@"orderId=(?<id>\d+)\s+status=(?<status>\w+)\s+amount=(?<amount>\d+)");
Match match = regex.Match(logLine);

if (match.Success)
{
    // Groups["id"]:
    // 이름 있는 그룹에서 추출된 값을 꺼냅니다.
    string id = match.Groups["id"].Value;
    string status = match.Groups["status"].Value;
    string amount = match.Groups["amount"].Value;

    Console.WriteLine($"[Regex] Order={id}, Status={status}, Amount={amount}");
}
