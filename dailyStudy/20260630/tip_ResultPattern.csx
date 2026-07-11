#nullable enable

using System;

// 실무 패턴: Result<T>
// 예외(Exception)는 정말 예외적인 상황에 쓰고, 예상 가능한 실패는 값으로 돌려주는 패턴입니다.
// 예: 입력 검증 실패, 권한 없음, 찾을 수 없음 같은 흐름은 Result로 표현하면 호출부가 명확해집니다.

public readonly record struct Result<T>(bool IsSuccess, T? Value, string Error)
{
    // static 메서드:
    // 객체를 만들지 않고 타입 이름으로 호출하는 메서드입니다.
    // Result<int>.Success(10)처럼 씁니다.
    public static Result<T> Success(T value) => new(true, value, "");

    public static Result<T> Failure(string error) => new(false, default, error);
}

public static Result<int> ParseOrderId(string raw)
{
    // string.IsNullOrWhiteSpace:
    // null, 빈 문자열, 공백 문자열을 한 번에 검사합니다.
    if (string.IsNullOrWhiteSpace(raw))
    {
        return Result<int>.Failure("Order id is required.");
    }

    // int.TryParse:
    // 변환 성공 여부를 bool로 돌려주고, 성공한 값은 out 매개변수로 받습니다.
    // 사용자 입력처럼 실패 가능성이 높은 값에는 Parse보다 TryParse가 안전합니다.
    if (!int.TryParse(raw, out int id))
    {
        return Result<int>.Failure("Order id must be a number.");
    }

    if (id <= 0)
    {
        return Result<int>.Failure("Order id must be positive.");
    }

    return Result<int>.Success(id);
}

Result<int> result = ParseOrderId("42");

// if 문:
// 조건이 true일 때와 false일 때의 흐름을 나눕니다.
if (result.IsSuccess)
{
    Console.WriteLine($"[Result] Parsed order id: {result.Value}");
}
else
{
    Console.WriteLine($"[Result] Failed: {result.Error}");
}
