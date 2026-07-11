using System;
using System.Collections.Generic;

// Options 클래스:
// appsettings.json, 환경 변수, command line 등에서 들어온 설정을 강한 타입으로 담는 모델입니다.
public sealed class ApiOptions
{
    // required:
    // 객체를 만들 때 반드시 값을 넣어야 한다는 뜻입니다.
    public required string BaseUrl { get; init; }

    // init:
    // 객체 초기화 시점에는 설정할 수 있지만, 이후에는 바꿀 수 없습니다.
    // 설정 객체는 실행 중 바뀌지 않는 값으로 다루는 경우가 많습니다.
    public int TimeoutSeconds { get; init; } = 30;
    public int RetryCount { get; init; } = 3;
}

// IReadOnlyList<string>:
// 호출자가 오류 목록을 읽을 수는 있지만 수정하지는 못하게 하는 반환 타입입니다.
static IReadOnlyList<string> Validate(ApiOptions options)
{
    var errors = new List<string>();

    // Uri.TryCreate:
    // 문자열이 올바른 절대 URL인지 예외 없이 검사합니다.
    if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
    {
        errors.Add("BaseUrl must be an absolute URL.");
    }

    // timeout이 0 이하이면 외부 API 호출이 즉시 실패하거나 이상한 동작을 할 수 있습니다.
    if (options.TimeoutSeconds <= 0)
    {
        errors.Add("TimeoutSeconds must be positive.");
    }

    // retry 횟수가 음수인 설정은 의미가 없으므로 시작 시점에 막습니다.
    if (options.RetryCount < 0)
    {
        errors.Add("RetryCount cannot be negative.");
    }

    return errors;
}

// 실제 프로젝트에서는 이 값들이 appsettings.json에서 바인딩됩니다.
// 여기서는 실행 가능한 예제를 위해 코드에서 직접 생성합니다.
var options = new ApiOptions
{
    BaseUrl = "https://api.example.local",
    TimeoutSeconds = 10,
    RetryCount = 2
};

IReadOnlyList<string> errors = Validate(options);

// 조건 연산자:
// errors.Count가 0이면 Valid를 출력하고, 아니면 오류 목록을 출력합니다.
Console.WriteLine(errors.Count == 0
    ? "[Options] Valid"
    : $"[Options] Invalid: {string.Join(", ", errors)}");
