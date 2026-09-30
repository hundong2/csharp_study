using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace WorkshopContractApi.OpenApi;

/// <summary>
/// endpoint별 metadata와 별개인 문서 전체 제목·버전·설명을 한곳에서 보완하는 OpenAPI transformer입니다.
/// </summary>
public sealed class WorkshopApiDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// 생성이 끝난 OpenAPI 문서의 최상위 Info를 학습 API 정보로 교체합니다.
    /// </summary>
    /// <param name="document">framework가 endpoint metadata로 만든 OpenAPI 문서입니다.</param>
    /// <param name="context">문서 이름과 DI 같은 생성 문맥입니다.</param>
    /// <param name="cancellationToken">문서 요청이 취소되었을 때 변환도 중단할 신호입니다.</param>
    /// <returns>문서 수정이 끝났음을 나타내는 완료된 Task를 반환합니다.</returns>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        document.Info = new OpenApiInfo
        {
            Title = "워크숍 등록 계약 API",
            // context.DocumentName의 v1은 문서 이름이며 API URL versioning을 자동 구성하지 않습니다.
            Version = context.DocumentName,
            Description = "TypedResults와 OpenAPI 3.1.1 문서를 실제 응답 검증에 연결하는 C# 학습 API입니다."
        };
        return Task.CompletedTask;
    }
}
