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

// readonly record struct:
// - record: 값 비교, ToString 출력이 자동으로 편하게 만들어집니다.
// - struct: 값 타입입니다. 작은 값 객체에 적합합니다.
// - readonly: 생성 후 필드/속성을 바꾸지 않겠다는 뜻입니다.
// 메모리 관점:
// - class 객체는 힙에 만들어지고 GC 관리 대상입니다.
// - 작은 struct는 값 자체가 복사되므로 별도 객체 할당이 없습니다.
// - 단, 큰 struct를 자주 복사하면 비용이 커질 수 있습니다.
public readonly record struct CustomerId(long Value);

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

CustomerId customerId = new(1);

Console.WriteLine(customer);
Console.WriteLine($"CustomerId={customerId.Value}");
Console.WriteLine(GetContactLabel(customer));
