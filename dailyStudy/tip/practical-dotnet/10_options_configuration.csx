using System;
using System.Collections.Generic;

public sealed class ApiOptions
{
    public required string BaseUrl { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int RetryCount { get; init; } = 3;
}

static IReadOnlyList<string> Validate(ApiOptions options)
{
    var errors = new List<string>();

    if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
    {
        errors.Add("BaseUrl must be an absolute URL.");
    }

    if (options.TimeoutSeconds <= 0)
    {
        errors.Add("TimeoutSeconds must be positive.");
    }

    if (options.RetryCount < 0)
    {
        errors.Add("RetryCount cannot be negative.");
    }

    return errors;
}

var options = new ApiOptions
{
    BaseUrl = "https://api.example.local",
    TimeoutSeconds = 10,
    RetryCount = 2
};

IReadOnlyList<string> errors = Validate(options);
Console.WriteLine(errors.Count == 0
    ? "[Options] Valid"
    : $"[Options] Invalid: {string.Join(", ", errors)}");
