#nullable enable

using System;
using System.Collections.Generic;

// Result<T>:
// 성공/실패를 예외가 아니라 값으로 표현하는 패턴입니다.
// T는 성공했을 때 담을 값의 타입입니다.
// 메모리 관점:
// - readonly record struct라 작은 결과 객체를 값 타입으로 전달합니다.
// - Errors는 IReadOnlyList<string> 참조를 들고 있으므로, 큰 에러 목록은 별도 객체로 존재합니다.
public readonly record struct Result<T>(bool IsSuccess, T? Value, IReadOnlyList<string> Errors)
{
    // Array.Empty<string>:
    // 빈 배열을 매번 새로 만들지 않고 재사용합니다.
    public static Result<T> Success(T value) => new(true, value, Array.Empty<string>());

    // params:
    // 호출할 때 Failure("a", "b")처럼 여러 문자열을 넘길 수 있게 합니다.
    public static Result<T> Failure(params string[] errors) => new(false, default, errors);
}

// 입력 요청 DTO입니다.
// record struct는 값 비교가 편하고, 작은 입력 모델 예제에 적합합니다.
public readonly record struct CreateUserRequest(string Name, string Email);

static Result<CreateUserRequest> Validate(CreateUserRequest request)
{
    // List<string>:
    // 검증 오류가 여러 개일 수 있으므로 목록에 모읍니다.
    var errors = new List<string>();

    // IsNullOrWhiteSpace:
    // null, 빈 문자열, 공백 문자열을 모두 검사합니다.
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        errors.Add("Name is required.");
    }

    // Contains:
    // 아주 단순한 이메일 예제 검증입니다.
    // 실무에서는 정교한 이메일 검증보다 "필요한 수준의 검증"을 정하는 것이 중요합니다.
    if (!request.Email.Contains('@'))
    {
        errors.Add("Email format is invalid.");
    }

    // 삼항 연산자:
    // 조건 ? true일 때 값 : false일 때 값 형태입니다.
    return errors.Count == 0
        ? Result<CreateUserRequest>.Success(request)
        : Result<CreateUserRequest>.Failure(errors.ToArray());
}

// 테스트 입력입니다.
var input = new CreateUserRequest("Kim", "kim@example.com");
Result<CreateUserRequest> result = Validate(input);

if (result.IsSuccess)
{
    // IsSuccess가 true일 때만 Value를 사용한다는 규칙을 지킵니다.
    Console.WriteLine($"[Validation] User accepted: {result.Value.Name}");
}
else
{
    Console.WriteLine($"[Validation] Failed: {string.Join(", ", result.Errors)}");
}
