using System.Text.Json;
using System.Text.Json.Serialization;

// 이름도 더 명확하게 바꿔봅시다.
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