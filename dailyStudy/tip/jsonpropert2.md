# C# Attribute와 JSON Property 처리 원리

이 문서는 C# 어트리뷰트(Attribute)가 어떻게 동작하는지, 그리고 JSON 직렬화에서 `[JsonProperty]`, `[JsonPropertyName]`, `JsonConverter`가 어떤 방식으로 사용되는지 정리합니다.

핵심은 다음 한 문장입니다.

> **어트리뷰트는 실행되는 코드가 아니라, 코드에 붙여 두는 메타데이터입니다.**

즉, `[JsonPropertyName("user_name")]`을 붙였다고 해서 그 줄이 직접 실행되는 것은 아닙니다. 컴파일러가 해당 정보를 DLL/EXE의 메타데이터에 기록해 두고, 나중에 JSON 라이브러리나 프레임워크가 그 정보를 읽어서 동작을 결정합니다.

## 1. Attribute 기본 개념

어트리뷰트는 클래스, 메서드, 프로퍼티, 필드, 매개변수 등에 붙일 수 있는 설명 정보입니다.

```csharp
using System;

[Obsolete("Use NewApi instead.")]
public class OldApi
{
}
```

위 코드에서 `[Obsolete]`는 `OldApi` 클래스에 붙은 메타데이터입니다. 컴파일러와 IDE는 이 정보를 보고 “이 API는 오래되었다”는 경고를 보여 줄 수 있습니다.

### 어트리뷰트 클래스 이름 규칙

C#에서는 어트리뷰트 클래스 이름 끝의 `Attribute`를 생략할 수 있습니다.

```csharp
[Obsolete]
```

위 코드는 실제로는 아래 타입을 가리킵니다.

```csharp
ObsoleteAttribute
```

JSON에서도 마찬가지입니다.

| 코드에 쓰는 이름 | 실제 어트리뷰트 타입 | 소속 |
|------------------|----------------------|------|
| `[JsonProperty]` | `JsonPropertyAttribute` | Newtonsoft.Json |
| `[JsonPropertyName]` | `JsonPropertyNameAttribute` | System.Text.Json |
| `[JsonConverter]` | `JsonConverterAttribute` | System.Text.Json |

## 2. 컴파일 단계: 실행 코드가 아니라 메타데이터로 기록된다

다음 코드를 예로 보겠습니다.

```csharp
using System.Text.Json.Serialization;

public class User
{
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = "";
}
```

빌드할 때 C# 컴파일러는 `[JsonPropertyName("user_name")]`을 일반 실행 코드로 바꾸지 않습니다.

대신 생성된 DLL/EXE의 메타데이터에 다음과 같은 정보를 기록합니다.

```text
Type: User
Property: UserName
Attribute: JsonPropertyNameAttribute
Constructor argument: "user_name"
```

이 단계에서 중요한 점은 다음과 같습니다.

- 어트리뷰트 객체가 자동으로 실행되지 않습니다.
- 어트리뷰트 생성자가 매번 호출되는 것도 아닙니다.
- “UserName 프로퍼티에는 이런 어트리뷰트가 붙어 있다”는 정보만 어셈블리에 기록됩니다.

## 3. JIT 단계: 대부분의 어트리뷰트는 무시된다

.NET 프로그램이 실행되면 JIT 컴파일러가 IL을 CPU가 실행할 수 있는 기계어로 바꿉니다.

이때 JIT는 대부분의 일반 어트리뷰트를 신경 쓰지 않습니다.

예를 들어 다음 어트리뷰트들은 JIT의 기계어 생성에 직접 영향을 주지 않습니다.

- `[JsonPropertyName]`
- `[JsonProperty]`
- `[JsonConverter]`
- ASP.NET Core의 `[HttpGet]`
- MVC 모델 검증용 `[Required]`

이 어트리뷰트들은 JSON 라이브러리, ASP.NET Core, 검증 프레임워크 같은 별도 코드가 나중에 읽어서 처리합니다.

### JIT가 참고할 수 있는 특수 정보

반대로 JIT나 런타임 동작과 더 가까운 특수 어트리뷰트나 메타데이터도 있습니다.

```csharp
using System.Runtime.CompilerServices;

public class Calculator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Add(int left, int right)
    {
        return left + right;
    }
}
```

`MethodImplOptions.AggressiveInlining`은 JIT에게 “이 메서드는 가능하면 호출부에 펼쳐 달라”는 힌트를 줍니다. 단, 이것도 무조건 인라인을 보장하는 명령은 아닙니다. JIT가 코드 크기, 호출 패턴, 최적화 단계 등을 고려해서 최종 결정합니다.

## 4. 런타임 단계: 리플렉션으로 읽을 때 어트리뷰트가 의미를 가진다

일반 어트리뷰트는 런타임에 리플렉션으로 읽을 수 있습니다.

```csharp
using System;
using System.Reflection;
using System.Text.Json.Serialization;

public class User
{
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = "";
}

PropertyInfo property = typeof(User).GetProperty(nameof(User.UserName))!;
JsonPropertyNameAttribute? attribute =
    property.GetCustomAttribute<JsonPropertyNameAttribute>();

Console.WriteLine(attribute?.Name); // user_name
```

여기서 흐름은 다음과 같습니다.

1. `typeof(User)`로 `User` 타입 정보를 얻습니다.
2. `GetProperty`로 `UserName` 프로퍼티 정보를 찾습니다.
3. `GetCustomAttribute`로 메타데이터에 기록된 어트리뷰트를 읽습니다.
4. 이때 필요한 경우 어트리뷰트 객체가 만들어지고 값이 채워집니다.

실무에서는 직접 리플렉션을 자주 쓰기보다, JSON 라이브러리나 ASP.NET Core 같은 프레임워크가 내부에서 이 작업을 대신 수행합니다.

## 5. JSON Property 이름 변경 예제

`System.Text.Json`에서는 `[JsonPropertyName]`을 사용합니다.

```csharp
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class User
{
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = "";
}

var user = new User { UserName = "Gemini" };
string json = JsonSerializer.Serialize(user);

Console.WriteLine(json);
// {"user_name":"Gemini"}
```

JSON 라이브러리는 `UserName` 프로퍼티를 발견한 뒤, 해당 프로퍼티에 붙은 `[JsonPropertyName("user_name")]` 메타데이터를 읽습니다. 그래서 C# 프로퍼티 이름인 `UserName` 대신 JSON 키 이름으로 `user_name`을 사용합니다.

Newtonsoft.Json을 쓴다면 같은 목적에 `[JsonProperty]`를 사용합니다.

```csharp
using Newtonsoft.Json;

public class User
{
    [JsonProperty("user_name")]
    public string UserName { get; set; } = "";
}
```

둘은 이름이 비슷하지만 서로 다른 라이브러리의 어트리뷰트입니다.

## 6. 외부 API JSON을 안전하게 받기 위한 옵션

외부 API는 항상 우리가 원하는 형식으로 JSON을 보내 주지 않습니다.

예를 들어 다음과 같은 문제가 자주 발생합니다.

- 숫자를 숫자 `100`이 아니라 문자열 `"100"`으로 보냅니다.
- 프로퍼티 이름 대소문자가 다릅니다.
- 배열이나 객체 끝에 불필요한 쉼표가 있습니다.
- JSON 안에 주석이 섞여 있습니다.
- 우리가 모르는 필드가 추가됩니다.

이럴 때는 `JsonSerializerOptions`를 명시적으로 설정합니다.

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

var looseOptions = new JsonSerializerOptions
{
    // JSON의 userName, USERNAME, UserName을 C#의 UserName에 매핑할 수 있습니다.
    PropertyNameCaseInsensitive = true,

    // "100"처럼 문자열로 온 숫자도 int, long, double 등에 읽을 수 있게 합니다.
    NumberHandling = JsonNumberHandling.AllowReadingFromString,

    // [1, 2, 3,]처럼 마지막 쉼표가 있어도 허용합니다.
    AllowTrailingCommas = true,

    // JSON 안의 // 주석이나 /* */ 주석을 건너뜁니다.
    ReadCommentHandling = JsonCommentHandling.Skip
};
```

### 옵션 사용 시 주의점

옵션을 느슨하게 만들면 외부 API 변화에 덜 깨집니다. 하지만 너무 느슨하면 데이터 품질 문제를 늦게 발견할 수 있습니다.

권장 기준은 다음과 같습니다.

- 외부 API가 불안정하거나 레거시라면 유연한 옵션을 사용합니다.
- 내부 시스템 간 통신이라면 엄격하게 받아서 문제를 빨리 발견하는 편이 좋습니다.
- 중요 필드가 잘못 들어오면 조용히 넘어가지 말고 로그를 남깁니다.

## 7. 날짜 필드를 안전하게 처리하는 Custom Converter

실무에서 JSON 파싱 오류가 가장 자주 나는 필드 중 하나가 날짜입니다.

다음처럼 값이 제각각 들어올 수 있습니다.

```json
{ "created_at": "2026-07-11" }
{ "created_at": "" }
{ "created_at": "not-a-date" }
{ "created_at": null }
```

기본 파서에 맡기면 전체 역직렬화가 실패할 수 있습니다. 특정 필드만 안전하게 처리하고 싶다면 `JsonConverter<T>`를 만들 수 있습니다.

```csharp
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class SafeNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            return null;
        }

        string? value = reader.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out DateTime parsed)
            ? parsed
            : null;
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTime? value,
        JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

public class SampleDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("created_at")]
    [JsonConverter(typeof(SafeNullableDateTimeConverter))]
    public DateTime? CreatedAt { get; set; }
}
```

이 컨버터는 날짜가 비어 있거나 이상한 문자열이어도 예외를 던지지 않고 `null`로 처리합니다.

## 8. 전체 JSON 구조가 깨졌을 때는 try-catch가 필요하다

옵션과 컨버터를 잘 설정해도 JSON 자체가 문법적으로 깨져 있으면 `JsonException`이 발생합니다.

예를 들어 중괄호가 닫히지 않은 JSON은 파싱할 수 없습니다.

```csharp
using System;
using System.Text.Json;

string brokenJson = "{ \"name\": \"Gemini\", \"age\": 25, ";

try
{
    SampleDto? result = JsonSerializer.Deserialize<SampleDto>(brokenJson);
}
catch (JsonException ex)
{
    Console.WriteLine("JSON parsing failed.");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"Path: {ex.Path}");
    Console.WriteLine($"Line: {ex.LineNumber}");
    Console.WriteLine($"BytePositionInLine: {ex.BytePositionInLine}");
}
```

`JsonException`에는 디버깅에 필요한 위치 정보가 들어 있습니다.

| 속성 | 의미 |
|------|------|
| `Path` | 오류가 발생한 JSON 경로입니다. 예: `$.products[2].price` |
| `LineNumber` | 오류가 발생한 줄 번호입니다. |
| `BytePositionInLine` | 해당 줄에서 오류가 발생한 바이트 위치입니다. |
| `Message` | 파서가 제공하는 상세 오류 메시지입니다. |

실무에서는 이 정보와 함께 원본 응답 일부, 외부 API 이름, correlation id, 요청 id를 같이 로깅하는 것이 좋습니다.

## 9. 실무용 안전 역직렬화 헬퍼

여러 곳에서 try-catch를 반복하지 않으려면 작은 헬퍼를 만들어 둘 수 있습니다.

```csharp
using System;
using System.Text.Json;

public readonly record struct JsonReadResult<T>(
    bool IsSuccess,
    T? Value,
    string Error);

public static class SafeJson
{
    public static JsonReadResult<T> TryDeserialize<T>(
        string json,
        JsonSerializerOptions options)
    {
        try
        {
            T? value = JsonSerializer.Deserialize<T>(json, options);
            return new JsonReadResult<T>(true, value, "");
        }
        catch (JsonException ex)
        {
            string error =
                $"Path={ex.Path}, Line={ex.LineNumber}, Position={ex.BytePositionInLine}, Message={ex.Message}";

            return new JsonReadResult<T>(false, default, error);
        }
    }
}
```

사용 예시는 다음과 같습니다.

```csharp
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
};

JsonReadResult<SampleDto> result =
    SafeJson.TryDeserialize<SampleDto>("{\"name\":\"Gemini\"}", options);

if (result.IsSuccess)
{
    Console.WriteLine(result.Value?.Name);
}
else
{
    Console.WriteLine(result.Error);
}
```

이 방식은 외부 API 응답 처리에서 특히 유용합니다.

## 10. 성능 관점: 리플렉션은 매번 무겁게 도는가

초심자가 자주 오해하는 부분이 있습니다.

> “어트리뷰트는 리플렉션으로 읽는다는데, JSON 직렬화할 때마다 매번 느린가?”

대부분의 JSON 라이브러리는 타입 정보를 한 번 분석한 뒤 내부 캐시에 저장합니다. 즉, 매번 모든 프로퍼티와 어트리뷰트를 처음부터 다시 뒤지는 방식으로 동작하지 않습니다.

그래도 고성능 경로에서는 다음을 고려합니다.

- `JsonSerializerOptions`를 매번 새로 만들지 말고 재사용합니다.
- DTO 타입을 명확하게 유지합니다.
- 필요하면 System.Text.Json source generation을 검토합니다.
- 커스텀 컨버터 안에서 예외를 흐름 제어로 남발하지 않습니다.

## 11. System.Text.Json Source Generation 간단 개념

`System.Text.Json`은 리플렉션 비용과 AOT 호환성을 개선하기 위해 source generation 방식을 지원합니다.

개념적으로는 다음과 같습니다.

- 빌드 시점에 JSON 직렬화에 필요한 타입 정보를 미리 생성합니다.
- 런타임 리플렉션 의존을 줄입니다.
- Native AOT 같은 환경에서 유리합니다.

예시:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(SampleDto))]
public partial class AppJsonContext : JsonSerializerContext
{
}
```

source generation은 처음부터 필수는 아닙니다. 기본 직렬화를 이해한 뒤, 성능이나 AOT 요구가 있을 때 도입하는 것이 좋습니다.

## 12. 요약

- 어트리뷰트는 실행 코드가 아니라 메타데이터입니다.
- 컴파일러는 어트리뷰트 정보를 어셈블리 메타데이터에 기록합니다.
- JIT는 `[JsonPropertyName]`, `[JsonConverter]` 같은 일반 어트리뷰트를 기계어 생성에 직접 반영하지 않습니다.
- JSON 라이브러리는 런타임에 타입 정보를 읽고, 어트리뷰트에 따라 JSON 이름과 변환 방식을 결정합니다.
- 외부 API JSON은 옵션, 커스텀 컨버터, try-catch, 로깅을 함께 사용해야 안전합니다.
- 고성능 또는 Native AOT가 중요하면 `JsonSerializerOptions` 재사용과 source generation을 검토합니다.

## 관련 파일

- [jsonproperty/README.md](./jsonproperty/README.md)
- [jsonproperty/json.csx](./jsonproperty/json.csx)
- [jsonproperty/FlexibleStringConverter.cs](./jsonproperty/FlexibleStringConverter.cs)
- [jsonproperty/target_type_inference.md](./jsonproperty/target_type_inference.md)
