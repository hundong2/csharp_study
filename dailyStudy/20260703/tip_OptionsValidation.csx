using System;
using System.Collections.Generic;

// 실무 패턴: Options + Validation
// 설정값은 문자열로 들어오는 경우가 많습니다. 앱 시작 시 강하게 검증해야 운영 중 장애를 줄일 수 있습니다.

public sealed class GatewayOptions
{
    // init 접근자:
    // 객체를 만들 때만 값을 설정하고 이후에는 변경하지 못하게 합니다.
    public required string Host { get; init; }
    public required int Port { get; init; }
    public int TimeoutMs { get; init; } = 1000;
}

public static List<string> Validate(GatewayOptions options)
{
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(options.Host))
    {
        errors.Add("Host is required.");
    }

    if (options.Port is < 1 or > 65535)
    {
        errors.Add("Port must be between 1 and 65535.");
    }

    if (options.TimeoutMs <= 0)
    {
        errors.Add("TimeoutMs must be positive.");
    }

    return errors;
}

var options = new GatewayOptions
{
    Host = "api.example.local",
    Port = 443,
    TimeoutMs = 1500
};

List<string> errors = Validate(options);

Console.WriteLine(errors.Count == 0
    ? "[Options] Configuration is valid."
    : $"[Options] Invalid: {string.Join(", ", errors)}");
