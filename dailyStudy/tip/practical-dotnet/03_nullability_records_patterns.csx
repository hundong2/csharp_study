#nullable enable

using System;

public sealed record CustomerDto
{
    // required:
    // 객체를 만들 때 반드시 값을 넣어야 하는 속성입니다.
    public required long Id { get; init; }

    public required string Name { get; init; }

    // string?:
    // null일 수 있음을 명시합니다.
    public string? Email { get; init; }
}

static string GetContactLabel(CustomerDto customer)
{
    // switch expression:
    // 조건에 따라 값을 바로 반환할 때 유용합니다.
    return customer.Email switch
    {
        null => "No email",
        "" => "Empty email",
        var email when email.EndsWith("@example.com") => "Internal domain",
        _ => "External domain"
    };
}

var customer = new CustomerDto
{
    Id = 1,
    Name = "Kim",
    Email = "kim@example.com"
};

Console.WriteLine(customer);
Console.WriteLine(GetContactLabel(customer));
