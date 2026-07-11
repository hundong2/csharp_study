#nullable enable

using System;
using System.Collections.Generic;

public readonly record struct Result<T>(bool IsSuccess, T? Value, IReadOnlyList<string> Errors)
{
    public static Result<T> Success(T value) => new(true, value, Array.Empty<string>());
    public static Result<T> Failure(params string[] errors) => new(false, default, errors);
}

public readonly record struct CreateUserRequest(string Name, string Email);

static Result<CreateUserRequest> Validate(CreateUserRequest request)
{
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        errors.Add("Name is required.");
    }

    if (!request.Email.Contains('@'))
    {
        errors.Add("Email format is invalid.");
    }

    return errors.Count == 0
        ? Result<CreateUserRequest>.Success(request)
        : Result<CreateUserRequest>.Failure(errors.ToArray());
}

var input = new CreateUserRequest("Kim", "kim@example.com");
Result<CreateUserRequest> result = Validate(input);

if (result.IsSuccess)
{
    Console.WriteLine($"[Validation] User accepted: {result.Value.Name}");
}
else
{
    Console.WriteLine($"[Validation] Failed: {string.Join(", ", result.Errors)}");
}
