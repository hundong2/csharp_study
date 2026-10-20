using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AuditEventApi.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace AuditEventApi.Presentation;

/// <summary>
/// HTTP 요청이 공통으로 가져야 할 감사 이벤트 식별자, 발생 시각, 행위자를 묶는 다형성 기반 형식입니다.
/// </summary>
/// <remarks>
/// abstract record는 이 형식 자체를 만들지 못하게 하면서 값 중심 비교를 제공합니다.
/// JSON의 type 판별자가 아래 JsonDerivedType 허용 목록 중 하나일 때만 구체 요청으로 역직렬화됩니다.
/// </remarks>
// JsonPolymorphic은 JSON의 type 값을 보고 실제 하위 record를 선택하라고 System.Text.Json에 알려 줍니다.
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "type",
    IgnoreUnrecognizedTypeDiscriminators = false,
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
// JsonDerivedType은 역직렬화를 허용할 형식을 명시적으로 등록합니다. 임의 CLR 형식 이름을 받지 않아 안전합니다.
[JsonDerivedType(typeof(UserSignedInRequest), "user-signed-in")]
[JsonDerivedType(typeof(DocumentChangedRequest), "document-changed")]
[JsonDerivedType(typeof(PermissionChangedRequest), "permission-changed")]
public abstract record AuditEventRequest
{
    /// <summary>
    /// HTTP JSON 본문을 엄격한 공통 설정으로 읽고 허용된 감사 이벤트 요청으로 변환합니다.
    /// </summary>
    /// <param name="httpContext">요청 본문, 요청 취소 신호, DI에 등록된 JSON 설정을 제공하는 현재 HTTP 문맥입니다.</param>
    /// <param name="parameter">Minimal API가 어떤 handler 매개변수를 만드는 중인지 알려 주는 메타데이터입니다.</param>
    /// <returns>본문에서 읽은 구체 감사 이벤트 요청을 반환하며, 올바른 요청에서는 null을 반환하지 않습니다.</returns>
    /// <exception cref="BadHttpRequestException">미지원 미디어 형식, 비어 있거나 null인 본문, 잘못된 JSON 계약이면 발생합니다.</exception>
    // async는 body 읽기를 기다리는 동안 thread를 붙잡지 않게 하며, ValueTask<T>는 사용자 바인딩의 비동기 결과 형식입니다.
    public static async ValueTask<AuditEventRequest?> BindAsync(
        HttpContext httpContext,
        ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(parameter);

        if (!httpContext.Request.HasJsonContentType())
        {
            throw new BadHttpRequestException(
                "Content-Type은 application/json 또는 +json 형식이어야 합니다.",
                StatusCodes.Status415UnsupportedMediaType);
        }

        // DI의 IOptions<T>는 Program에서 설정한 동일한 JSON 정책을 꺼내는 표준 .NET 패턴입니다.
        // var는 오른쪽 반환 형식으로 지역 변수 형식을 추론할 뿐, 형식 검사를 생략하는 dynamic이 아닙니다.
        var serializerOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>()
            .Value.SerializerOptions;
        // resolver chain의 첫 항목을 확인해 이 API 계약이 reflection보다 생성된 metadata를 우선 사용한다는 설명을 코드로 보장합니다.
        if (serializerOptions.TypeInfoResolverChain.Count == 0
            || serializerOptions.TypeInfoResolverChain[0] is not AuditJsonContext)
        {
            throw new InvalidOperationException(
                "AuditJsonContext가 JSON metadata resolver의 첫 항목으로 등록되지 않았습니다.");
        }

        // is not 패턴은 얻은 metadata의 형식을 검사하면서 requestTypeInfo 변수도 함께 만듭니다.
        // 잘못된 metadata 형식을 serializer에 넘기지 않도록 구성 오류를 요청 처리 전에 분명하게 드러냅니다.
        if (serializerOptions.GetTypeInfo(typeof(AuditEventRequest))
            is not JsonTypeInfo<AuditEventRequest> requestTypeInfo)
        {
            throw new InvalidOperationException(
                "AuditEventRequest용 source-generated JSON metadata가 등록되지 않았습니다.");
        }

        try
        {
            // await는 body 읽기가 끝난 뒤 다음 검증을 이어 가되 기다리는 동안 thread를 막지 않습니다.
            var request = await httpContext.Request.ReadFromJsonAsync(
                requestTypeInfo,
                httpContext.RequestAborted);
            if (request is null)
            {
                throw new BadHttpRequestException(
                    "JSON 본문은 비어 있거나 null일 수 없습니다.",
                    StatusCodes.Status400BadRequest);
            }

            return request;
        }
        catch (JsonException exception)
        {
            // 문법 오류, 중복·알 수 없는 속성, 누락된 필수 값은 클라이언트가 고칠 수 있는 400 경계입니다.
            throw new BadHttpRequestException(
                "JSON 본문이 감사 이벤트 계약과 맞지 않습니다.",
                StatusCodes.Status400BadRequest,
                exception);
        }
        catch (NotSupportedException exception)
        {
            // 다형성 type이 없거나 허용 목록 밖이면 serializer가 NotSupportedException을 낼 수 있어 400으로 좁혀 바꿉니다.
            // 요청 취소와 그 밖의 예외는 잡지 않아 취소 의미와 서버 오류 경계를 숨기지 않습니다.
            throw new BadHttpRequestException(
                "JSON type 판별자가 없거나 지원되지 않습니다.",
                StatusCodes.Status400BadRequest,
                exception);
        }
    }

    /// <summary>
    /// 생산자가 재전송했을 때 같은 사건인지 판별할 중복 식별자를 가져옵니다.
    /// </summary>
    // required는 객체를 만들 때 속성을 반드시 지정하게 하고, ?는 JSON null 가능성을 정직하게 표현합니다.
    // init은 객체를 처음 만들 때만 값을 넣게 해 역직렬화가 끝난 요청 DTO가 나중에 바뀌지 않도록 합니다.
    public required string? EventId { get; init; }

    /// <summary>
    /// 사건이 실제로 발생한 UTC 시각을 가져옵니다.
    /// </summary>
    public required DateTimeOffset? OccurredAtUtc { get; init; }

    /// <summary>
    /// 사건을 일으킨 사용자나 시스템의 식별자를 가져옵니다.
    /// </summary>
    public required string? ActorId { get; init; }
}

/// <summary>
/// 사용자 로그인 결과를 기록하는 구체적인 감사 이벤트 요청입니다.
/// </summary>
public sealed record UserSignedInRequest : AuditEventRequest
{
    /// <summary>
    /// 민감한 원문 전체가 아니라 학습용으로 전달받는 접속 IP 문자열을 가져옵니다.
    /// </summary>
    public required string? IpAddress { get; init; }

    /// <summary>
    /// 로그인이 성공했는지 여부를 가져옵니다.
    /// </summary>
    public required bool? Succeeded { get; init; }
}

/// <summary>
/// 문서 내용 변경을 기록하는 구체적인 감사 이벤트 요청입니다.
/// </summary>
public sealed record DocumentChangedRequest : AuditEventRequest
{
    /// <summary>
    /// 변경된 문서의 업무 식별자를 가져옵니다.
    /// </summary>
    public required string? DocumentId { get; init; }

    /// <summary>
    /// 비밀 본문 대신 저장할 짧은 변경 요약을 가져옵니다.
    /// </summary>
    public required string? ChangeSummary { get; init; }
}

/// <summary>
/// 한 사용자에게 권한을 부여하거나 회수한 사건을 기록하는 구체적인 감사 이벤트 요청입니다.
/// </summary>
public sealed record PermissionChangedRequest : AuditEventRequest
{
    /// <summary>
    /// 권한이 바뀐 대상 사용자의 식별자를 가져옵니다.
    /// </summary>
    public required string? TargetUserId { get; init; }

    /// <summary>
    /// 바뀐 권한 이름을 가져옵니다.
    /// </summary>
    public required string? Permission { get; init; }

    /// <summary>
    /// grant 또는 revoke 중 요청한 동작을 가져옵니다.
    /// </summary>
    public required string? Action { get; init; }
}

/// <summary>
/// 저장된 감사 이벤트를 HTTP 클라이언트에 공개하는 안정적인 응답 계약입니다.
/// </summary>
/// <param name="EventId">생산자가 부여한 중복 판별 식별자입니다.</param>
/// <param name="Kind">허용된 감사 이벤트 종류입니다.</param>
/// <param name="OccurredAtUtc">사건이 발생한 UTC 시각입니다.</param>
/// <param name="ActorId">사건을 일으킨 행위자 식별자입니다.</param>
/// <param name="Summary">민감한 원문을 제외한 사람이 읽을 수 있는 요약입니다.</param>
/// <param name="ReceivedAtUtc">이 API가 사건을 저장한 UTC 시각입니다.</param>
public sealed record AuditEventResponse(
    string EventId,
    string Kind,
    DateTimeOffset OccurredAtUtc,
    string ActorId,
    string Summary,
    DateTimeOffset ReceivedAtUtc);

/// <summary>
/// 루트 endpoint에서 실행 상태와 학습용 JSON 정책을 설명하는 응답 계약입니다.
/// </summary>
/// <param name="Service">실행 중인 서비스 이름입니다.</param>
/// <param name="SourceGeneratedJson">소스 생성 JSON metadata 사용 여부입니다.</param>
/// <param name="AllowedEventTypes">명시적으로 허용한 type 판별자 목록입니다.</param>
/// <param name="StrictInput">알 수 없는 속성, 중복 속성, 필수 멤버 누락을 거절하는지 나타냅니다.</param>
public sealed record ServiceStatusResponse(
    string Service,
    bool SourceGeneratedJson,
    IReadOnlyList<string> AllowedEventTypes,
    bool StrictInput);
