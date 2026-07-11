#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

// JsonConverter<int>:
// int 값을 JSON에서 읽고 쓸 방법을 직접 정의합니다.
// 외부 API가 숫자를 12 또는 "12"처럼 섞어서 줄 때 유용합니다.
public sealed class FlexibleIntConverter : JsonConverter<int>
{
    // Read:
    // JSON -> C# int로 변환할 때 호출됩니다.
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // TokenType:
        // 현재 JSON 토큰이 숫자인지, 문자열인지, null인지 등을 알려줍니다.
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        // 문자열 숫자도 허용합니다.
        // TryParse는 실패해도 예외를 던지지 않아 외부 입력 처리에 적합합니다.
        if (reader.TokenType == JsonTokenType.String &&
            int.TryParse(reader.GetString(), out int value))
        {
            return value;
        }

        // 변환할 수 없으면 기본값 0으로 처리합니다.
        // 실무에서는 이 지점에서 로그를 남기거나 Result 실패로 처리할 수도 있습니다.
        return 0;
    }

    // Write:
    // C# int -> JSON으로 쓸 때 호출됩니다.
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public sealed class ProductDto
{
    // JSON 필드 이름 product_name을 C# 속성 ProductName에 매핑합니다.
    [JsonPropertyName("product_name")]
    public string ProductName { get; set; } = "";

    // 이 속성에만 FlexibleIntConverter를 적용합니다.
    [JsonConverter(typeof(FlexibleIntConverter))]
    public int Stock { get; set; }
}

// stock이 문자열 "12"로 들어오는 외부 API 응답을 가정합니다.
string json = """{"product_name":"Keyboard","stock":"12"}""";

var options = new JsonSerializerOptions
{
    // JSON 필드 이름 대소문자가 달라도 매핑되게 합니다.
    PropertyNameCaseInsensitive = true
};

// Deserialize:
// JSON 문자열을 ProductDto 객체로 변환합니다.
ProductDto? product = JsonSerializer.Deserialize<ProductDto>(json, options);
Console.WriteLine($"[Json] {product?.ProductName} stock={product?.Stock}");
