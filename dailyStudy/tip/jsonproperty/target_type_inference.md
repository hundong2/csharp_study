# `new(...)` 뒤에 타입이 없는 이유(타입 추론 `target-typed new`)
`new(reader.GetString()!, null, null)`는 C#의 **타겟-타입 `new`(target-typed new expression)** 문법이라서, `new` 뒤에 타입을 생략해도 **주변 문맥(컨텍스트)에서 생성할 타입을 컴파일러가 추론**합니다.

## 예시 파일
[target-typed new expressions 제안서 (csharplang)](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-9.0/target-typed-new.md)

## 답변
당신 코드에서 이 부분:

```csharp
JsonTokenType.String => new(reader.GetString()!, null, null),
```

는 컴파일러가 아래처럼 해석합니다:

- `Read(...)`의 반환 타입이 `LineToken`
- `switch` 식의 각 arm(가지)도 결국 `LineToken`을 만들어야 함
- 그래서 `new(...)`는 **`new LineToken(...)`** 로 추론됨

즉, “이 `new`의 목표 타입(target type)”이 이미 `LineToken`으로 정해져 있으니 타입을 생략할 수 있는 겁니다.

### 같은 코드를 타입을 적어서 쓰면
```csharp
JsonTokenType.String => new LineToken(reader.GetString()!, null, null),
```

### 언제는 타입 생략이 안 되나?
타입을 추론할 “목표”가 불분명하면 실패합니다. 예를 들어:

- `var x = new(1, 2);` 처럼 왼쪽에 타입 정보가 없고
- 오버로드/제네릭 때문에 어떤 타입인지 결정이 안 되면

이때는 `new SomeType(...)`처럼 **명시**해야 합니다.

아래 예제는 “되는 경우 / 안 되는 경우”를 같이 보여줍니다.

````csharp
#!/usr/bin/env dotnet-script
using System;

record LineToken(string Text, int? Number);

static LineToken Parse(object input)
    => input switch
    {
        string s => new(s, null),     // OK: 반환 타입이 LineToken이라서 new LineToken(...)으로 추론
        int n    => new(null, n),     // OK
        _        => new(null, null)   // OK
    };

// ❌ 아래는 컴파일 에러(목표 타입이 없음)
// var x = new("hi", 1);

LineToken ok = new("hello", 123);     // OK: 왼쪽이 LineToken이라 목표 타입이 명확
Console.WriteLine(Parse("abc"));
Console.WriteLine(ok);
````

### 추가 자료
- [C# `new` 연산자 문서 (Microsoft Learn)](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator)
- [C# 패턴/스위치 식 문서 (Microsoft Learn)](https://learn.microsoft.com/dotnet/csharp/language-reference/operators/switch-expression)