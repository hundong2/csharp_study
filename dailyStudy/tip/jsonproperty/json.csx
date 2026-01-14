#!/usr/bin/evn dotnet-script
#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

{
    var json = @"{ ""key_name"": ""John"", ""lines"": [] }";

    var options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    var person = JsonSerializer.Deserialize<LineChecker>(json, options) ?? null;
    if(person is null)
    {
        Console.WriteLine("Deserialization failed");
        return;
    }
    Console.WriteLine($"Key Name: {person.KeyName}");
    Console.WriteLine($"Lines: {person.Lines}");
}
class LineChecker
{
    [JsonPropertyName("key_name")]
    public string KeyName { get; set; } = string.Empty;
    [JsonPropertyName("lines")]
    public ListLine Lines { get; set; } = new();
}

[JsonConverter(typeof(LineListTokenConverter))]
internal sealed class ListLine : List<LineToken>;
internal sealed record ElemntToeken(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] string Value);

[JsonConverter(typeof(LineTokenConverter))]
internal record LineToken(string? Text, ElemntToeken? Element, JsonElement? RawArray)
{
   public bool IsText => Text is not null;
   public bool IsElement => Element is not null;
   public bool IsArray => RawArray is {ValueKind: JsonValueKind.Array}; 
}  
internal class LineTokenConverter : JsonConverter<LineToken>
{
    public override LineToken Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    => reader.TokenType switch
    {
        JsonTokenType.String => new(reader.GetString()!, null, null),
        JsonTokenType.StartObject => new(null, JsonSerializer.Deserialize<ElemntToeken>(ref reader, options) ?? throw new JsonException("Element deserialization failed"), null) ,
        JsonTokenType.StartArray => new(null, null, JsonElement.ParseValue(ref reader)),
         _ => throw new JsonException("lines must be string or object or array")
    };    
    public override void Write(Utf8JsonWriter writer, LineToken value, JsonSerializerOptions options)
    {
        if( value.IsText )
        {
            writer.WriteStringValue(value.Text);
        }
        else if( value.IsElement )
        {
            JsonSerializer.Serialize(writer, value.Element, options);
        }
        else if( value.IsArray )
        {
            value.RawArray?.WriteTo(writer);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
internal sealed class LineListTokenConverter : JsonConverter<ListLine>
{
    public override ListLine Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if( reader.TokenType != JsonTokenType.StartArray )
            throw new JsonException("Unexpected end of JSON");
        // ✅ List<LineToken>으로 역직렬화하여 무한 재귀 방지
        var list = JsonSerializer.Deserialize<List<LineToken>>(ref reader, options) ?? new();
        if( list is null )
            throw new JsonException("line list deserialization failed");
        var lineList = new ListLine();
        lineList.AddRange(list);
        return lineList;
    }
    public override void Write(Utf8JsonWriter writer, ListLine value, JsonSerializerOptions options)
    {
        // ❌ 이렇게 하면 "ListLine"을 serialize하는 순간 다시 이 Write가 호출되어 무한 재귀가 됩니다.
        // JsonSerializer.Serialize(writer, value, options);

        // ✅ 대신 직접 배열로 쓰거나(아래처럼), 요소만 serialize 하세요.
        writer.WriteStartArray();
        foreach( var token in value )
        {
            JsonSerializer.Serialize<LineToken>(writer, token, options);
        }
        writer.WriteEndArray();
    }
}



