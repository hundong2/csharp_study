using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WorkshopContractApi.OpenApi;

/// <summary>
/// 특정 operation의 응답 header와 path parameter처럼 route metadata만으로 부족한 세부 계약을 보완합니다.
/// </summary>
public sealed class WorkshopApiOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// 예약 생성 201의 필수 Location header와 예약 조회 ID의 정규화 전 입력 schema를 추가합니다.
    /// </summary>
    /// <param name="operation">framework가 생성 중인 단일 path·HTTP method 작업입니다.</param>
    /// <param name="context">문서 이름, API 설명, DI를 제공하는 operation 문맥입니다.</param>
    /// <param name="cancellationToken">문서 요청이 취소되면 변환도 중단할 신호입니다.</param>
    /// <returns>header 보완이 끝났음을 나타내는 완료된 Task를 반환합니다.</returns>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // out var는 조회 성공 여부와 찾은 응답을 함께 받고, `is OpenApiResponse concreteResponse`는
        // interface 값을 수정 가능한 구체 형식인지 검사하면서 새 변수로 받는 declaration pattern입니다.
        if (operation.OperationId == "CreateWorkshopRegistration" &&
            operation.Responses is not null &&
            operation.Responses.TryGetValue("201", out var response) &&
            response is OpenApiResponse concreteResponse)
        {
            // ??=는 Headers가 아직 없을 때만 새 사전을 만들어 다른 transformer의 값을 보존합니다.
            concreteResponse.Headers ??= new Dictionary<string, IOpenApiHeader>(StringComparer.OrdinalIgnoreCase);
            concreteResponse.Headers["Location"] = new OpenApiHeader
            {
                Description = "생성된 예약을 조회할 상대 URI입니다.",
                Required = true,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Format = "uri-reference"
                }
            };
        }

        if (operation.OperationId == "GetWorkshopRegistration" &&
            operation.Parameters is not null)
        {
            // foreach는 collection의 parameter를 하나씩 확인하며, 이름이 맞는 path parameter만 보완합니다.
            foreach (var parameter in operation.Parameters)
            {
                if (parameter is OpenApiParameter concreteParameter &&
                    concreteParameter.Name == "registrationId")
                {
                    concreteParameter.Description = "앞뒤 공백 제거 후 3~40자의 영문·숫자·하이픈 예약 식별자입니다.";
                    concreteParameter.Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Pattern = @"^\s*[A-Za-z0-9-]{3,40}\s*$"
                    };
                }
            }
        }

        return Task.CompletedTask;
    }
}
