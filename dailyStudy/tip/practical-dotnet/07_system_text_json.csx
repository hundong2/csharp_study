#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public sealed class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        if (reader.TokenType == JsonTokenType.String &&
            int.TryParse(reader.GetString(), out int value))
        {
            return value;
        }

        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public sealed class ProductDto
{
    [JsonPropertyName("product_name")]
    public string ProductName { get; set; } = "";

    [JsonConverter(typeof(FlexibleIntConverter))]
    public int Stock { get; set; }
}

string json = """{"product_name":"Keyboard","stock":"12"}""";

var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

ProductDto? product = JsonSerializer.Deserialize<ProductDto>(json, options);
Console.WriteLine($"[Json] {product?.ProductName} stock={product?.Stock}");
