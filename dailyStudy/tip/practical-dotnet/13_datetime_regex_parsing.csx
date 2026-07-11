using System;
using System.Text.RegularExpressions;

string rawDate = "2026-07-11T13:20:00+09:00";

if (DateTimeOffset.TryParse(rawDate, out DateTimeOffset timestamp))
{
    Console.WriteLine($"[Date] UTC={timestamp.UtcDateTime:O}");
}

string logLine = "orderId=1001 status=paid amount=25000";

// Regex:
// 문자열에서 특정 패턴을 찾습니다.
// (?<name>...)는 이름 있는 그룹입니다.
var regex = new Regex(@"orderId=(?<id>\d+)\s+status=(?<status>\w+)\s+amount=(?<amount>\d+)");
Match match = regex.Match(logLine);

if (match.Success)
{
    string id = match.Groups["id"].Value;
    string status = match.Groups["status"].Value;
    string amount = match.Groups["amount"].Value;

    Console.WriteLine($"[Regex] Order={id}, Status={status}, Amount={amount}");
}
