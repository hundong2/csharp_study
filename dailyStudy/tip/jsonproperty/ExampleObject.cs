using System.Text.Json.Serialization;

namespace ExampleNamespace
{
    public class ExampleObject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("data")]
        [JsonConverter(typeof(FlexibleStringConverter))] // 커스텀 컨버터 적용
        public string Data { get; set; }
    }
}