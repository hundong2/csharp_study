using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSerializable(typeof(JsonpropertyExample.ExampleObject))]
[JsonSerializable(typeof(JsonpropertyExample.ExampleObjectTwo))]
internal partial class MyJsonContext : JsonSerializerContext { }

public class JsonpropertyExample
{
    public static void Main(string[] args)
    {

        Console.WriteLine("Hello, World!");
        ExampleCode exampleCode = new ExampleCode();
        exampleCode.ExampleJsonObjectUsingConverter();
        exampleCode.ExampleJsonObjectUsingConverterOptional();
    }
    public class ExampleCode
    {
        public ExampleCode()
        {

        }

        public void ExampleJsonObjectUsingConverterOptional()
        {
            ExampleObject example = new ExampleObject
            {
                Id = 1,
                Name = "Example",
                Data = "{\"key\":\"value\"}" // JSON 형식의 문자열
            };

            // Serialize with custom converter
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new FlexibleStringConverter() },
                TypeInfoResolver = MyJsonContext.Default
            };
            string jsonString = JsonSerializer.Serialize<ExampleObject>(example, options);
            Console.WriteLine("Serialized JSON:");
            Console.WriteLine(jsonString);

            // Deserialize back to object
            ExampleObject deserialized = JsonSerializer.Deserialize<ExampleObject>(jsonString, options);
            Console.WriteLine("Deserialized Object:");
            Console.WriteLine($"Id: {deserialized.Id}, Name: {deserialized.Name}, Data: {deserialized.Data}");
        }
        public void ExampleJsonObjectUsingConverter()
        {
            ExampleObject example = new ExampleObject
            {
                Id = 1,
                Name = "Example",
                Data = "{\"key\":\"value\"}" // JSON 형식의 문자열
            };

            // Serialize with custom converter
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                TypeInfoResolver = MyJsonContext.Default
            };
            string jsonString = JsonSerializer.Serialize<ExampleObject>(example, options);
            Console.WriteLine("Serialized JSON:");
            Console.WriteLine(jsonString);

            // Deserialize back to object
            ExampleObject deserialized = JsonSerializer.Deserialize<ExampleObject>(jsonString, options);
            Console.WriteLine("Deserialized Object:");
            Console.WriteLine($"Id: {deserialized.Id}, Name: {deserialized.Name}, Data: {deserialized.Data}");
        }

    }

    public class FlexibleStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Read는 이전과 같이 기본 동작을 구현합니다.
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }
            using (var jsonDoc = JsonDocument.ParseValue(ref reader))
            {
                return jsonDoc.RootElement.GetRawText();
            }
        }

        // 🔥 핵심: 유연하게 동작하도록 수정한 Write 메서드
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            // 1. 먼저 문자열을 JSON으로 파싱 시도
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(value))
                {
                    // 2. 파싱 성공 시: Raw JSON으로 쓴다.
                    doc.RootElement.WriteTo(writer);
                }
            }
            // 3. 파싱 실패 시 (JsonException 발생): 일반 문자열로 간주
            catch (JsonException)
            {
                // 4. 일반 문자열을 쓰는 기본 동작을 수행한다.
                writer.WriteStringValue(value);
            }
        }
    }

    public class ExampleObject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        [JsonConverter(typeof(FlexibleStringConverter))] // 커스텀 컨버터 적용
        public string Data { get; set; } = string.Empty;
    }
    public class ExampleObjectTwo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;
    }
}