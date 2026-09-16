namespace DocumentObservabilityExercise;

/// <summary>
/// 문서 변환 기술을 Application 계층에서 분리하는 Port입니다.
/// 다른 구현을 주입하면 같은 유스케이스로 다른 변환 엔진을 사용할 수 있습니다.
/// </summary>
public interface IDocumentConverter
{
    /// <summary>
    /// 검증된 요청의 원문을 지정 형식으로 변환합니다.
    /// request는 입력과 형식을, cancellationToken은 호출자의 중단 의도를 뜻하며,
    /// 성공 시 요청과 같은 Format의 변환 문서를, 예상 가능한 정책 거절 시 실패 Result를 반환합니다.
    /// </summary>
    /// <param name="request">도메인 검증을 통과한 변환 요청입니다.</param>
    /// <param name="cancellationToken">상위 호출자가 전달한 협력적 취소 신호입니다.</param>
    /// <returns>변환 문서 또는 예상 가능한 변환 거절입니다.</returns>
    Task<OperationResult<ConvertedDocument>> ConvertAsync(
        ConversionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// 변환 완료 영수증을 저장하는 기술을 숨기는 Repository Port입니다.
/// </summary>
public interface IConversionReceiptRepository
{
    /// <summary>
    /// 완료 영수증을 저장합니다.
    /// receipt는 원문 없이 저장할 정책 승인 메타데이터, cancellationToken은 취소 신호이며 반환값은 없습니다.
    /// 저장 실패는 이 메서드가 정상 결과를 만들 수 없는 기술 장애이므로 예외로 전달합니다.
    /// </summary>
    /// <param name="receipt">본문이 없는 변환 완료 영수증입니다.</param>
    /// <param name="cancellationToken">저장 작업에 전파할 취소 신호입니다.</param>
    Task SaveAsync(ConversionReceipt receipt, CancellationToken cancellationToken);
}

/// <summary>
/// 호출자가 보는 문서 변환 유스케이스의 공통 계약입니다.
/// 핵심 Service와 관측 Decorator가 같은 계약을 구현하므로 호출자는 감싸졌는지 알 필요가 없습니다.
/// </summary>
public interface IConversionWorkflow
{
    /// <summary>
    /// 한 문서를 변환하고 완료 영수증을 저장하는 전체 유스케이스를 실행합니다.
    /// request는 검증된 입력, cancellationToken은 취소 신호이며, 완료 영수증 또는 예상 실패를 반환합니다.
    /// </summary>
    /// <param name="request">검증을 마친 문서 변환 요청입니다.</param>
    /// <param name="cancellationToken">아래 Converter와 Repository까지 전달할 취소 신호입니다.</param>
    /// <returns>완료 영수증 또는 호출자가 대응할 수 있는 실패입니다.</returns>
    Task<OperationResult<ConversionReceipt>> ExecuteAsync(
        ConversionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Converter와 Repository를 순서대로 조정하는 Application Service입니다.
/// 로그·trace·metric을 몰라도 업무 흐름을 수행할 수 있어 단위 테스트와 재사용이 쉬워집니다.
/// </summary>
public sealed class DocumentConversionService : IConversionWorkflow
{
    private readonly IDocumentConverter _converter;
    private readonly IConversionReceiptRepository _repository;

    /// <summary>
    /// 유스케이스가 필요한 Port 구현을 생성자 주입으로 받습니다.
    /// converter는 변환 기술, repository는 저장 기술이며, 완성된 Service 객체를 초기화합니다.
    /// </summary>
    /// <param name="converter">원문을 결과 문서로 바꾸는 Port 구현입니다.</param>
    /// <param name="repository">완료 영수증을 저장하는 Port 구현입니다.</param>
    public DocumentConversionService(
        IDocumentConverter converter,
        IConversionReceiptRepository repository)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Converter가 성공한 경우에만 영수증을 만들고 Repository에 저장합니다.
    /// request는 입력, cancellationToken은 취소 신호이며, 완료 영수증 또는 Converter의 거절을 반환합니다.
    /// 기술 예외와 취소를 잡아 Result로 바꾸지 않아 상위 정책이 둘을 정확히 구분하게 합니다.
    /// </summary>
    /// <param name="request">검증을 마친 문서 변환 요청입니다.</param>
    /// <param name="cancellationToken">모든 비동기 Port에 그대로 전파할 취소 신호입니다.</param>
    /// <returns>저장까지 끝난 영수증 또는 예상 가능한 변환 거절입니다.</returns>
    public async Task<OperationResult<ConversionReceipt>> ExecuteAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // await는 I/O가 끝날 때까지 스레드를 붙잡지 않고, 끝난 뒤 같은 논리 흐름을 이어 가는 문법입니다.
        // ConfigureAwait(false)는 특정 UI/요청 컨텍스트로 돌아갈 필요가 없는 라이브러리 성격의 코드임을 나타냅니다.
        var conversion = await _converter
            .ConvertAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!conversion.IsSuccess)
        {
            return OperationResult.Failure<ConversionReceipt>(
                conversion.Error.Code,
                conversion.Error.Message);
        }

        // 교체 가능한 Adapter의 성공값도 Port 계약을 지키는지 Application 경계에서 확인합니다.
        // 이 검사가 없으면 영수증의 형식과 request 기반 telemetry 태그가 서로 달라질 수 있습니다.
        if (!string.Equals(
                conversion.Value.Format,
                request.TargetFormat,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Converter가 요청과 다른 출력 형식을 반환했습니다.");
        }

        var receipt = new ConversionReceipt(
            request.JobId,
            request.TargetFormat,
            conversion.Value.Content.Length);

        await _repository
            .SaveAsync(receipt, cancellationToken)
            .ConfigureAwait(false);

        return OperationResult.Success(receipt);
    }
}
