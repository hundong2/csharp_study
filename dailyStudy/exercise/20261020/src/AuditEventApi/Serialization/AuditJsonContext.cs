using System.Text.Json.Serialization;
using AuditEventApi.Presentation;

namespace AuditEventApi.Serialization;

/// <summary>
/// 빌드 시점에 감사 API JSON 계약 metadata를 생성하도록 System.Text.Json source generator에 알려 줍니다.
/// </summary>
/// <remarks>
/// 다형성 역직렬화는 metadata 모드에서 지원됩니다. 런타임 reflection만 사용할 때보다 계약을 빌드 결과에서
/// 확인하기 쉽고 trimming 또는 Native AOT로 확장할 토대를 만들 수 있습니다.
/// </remarks>
// partial은 이 파일의 선언과 compiler가 생성한 다른 선언을 하나의 클래스로 합치는 C# 문법입니다.
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AuditEventRequest))]
[JsonSerializable(typeof(UserSignedInRequest))]
[JsonSerializable(typeof(DocumentChangedRequest))]
[JsonSerializable(typeof(PermissionChangedRequest))]
[JsonSerializable(typeof(AuditEventResponse))]
[JsonSerializable(typeof(ServiceStatusResponse))]
internal sealed partial class AuditJsonContext : JsonSerializerContext;
