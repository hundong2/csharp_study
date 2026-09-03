// interface는 구현 코드를 넣기보다 구현체가 반드시 제공할 메서드 모양을 선언하는 계약입니다.
// Port는 Application이 필요한 기능만 선언한 경계입니다.
// DIP(SOLID의 의존성 역전 원칙)에 따라 업무 코드는 HttpClient 같은 세부 기술이 아니라 이 인터페이스에 의존합니다.
interface IExchangeRateGateway
{
    /// <summary>
    /// 지정한 기준 통화의 최신 환율 표를 외부 시스템에서 가져옵니다.
    /// </summary>
    /// <param name="baseCurrency">환율 1의 기준이 되는 통화입니다.</param>
    /// <param name="cancellationToken">호출자가 작업 중단을 요청할 때 Adapter까지 전달할 신호입니다.</param>
    /// <returns>가져오기에 성공하면 환율 표, 예상 가능한 통신·응답 실패면 오류를 담은 비동기 결과를 반환합니다.</returns>
    Task<Result<ExchangeRateTable>> GetLatestRatesAsync(
        Currency baseCurrency,
        CancellationToken cancellationToken);
}

// Strategy 인터페이스는 "표에서 어떤 환율을 고를지"를 서비스에서 분리합니다.
// 정책이 바뀌어도 서비스 코드를 고치지 않아 OCP(SOLID의 개방-폐쇄 원칙)를 지키고 단위 테스트도 쉬워집니다.
interface IRateSelectionPolicy
{
    /// <summary>
    /// 환율 표와 목표 통화를 보고 업무 규칙에 맞는 적용 환율을 선택합니다.
    /// </summary>
    /// <param name="table">Gateway가 반환한 검증된 환율 표입니다.</param>
    /// <param name="target">환산 결과로 원하는 통화입니다.</param>
    /// <returns>적용할 양수 환율 또는 지원하지 않는 통화라는 실패 Result를 반환합니다.</returns>
    Result<decimal> SelectRate(ExchangeRateTable table, Currency target);
}

// Audit 역시 Port로 두면 Application Service를 콘솔에서 분리해 테스트에서 조용한 대역으로 바꿀 수 있습니다.
interface IConversionAudit
{
    /// <summary>
    /// 성공한 환산의 입력·출력·적용 환율을 관찰 기록으로 남깁니다.
    /// </summary>
    /// <param name="receipt">서비스가 만든 검증된 환산 결과입니다.</param>
    /// <returns>기록만 수행하므로 반환값은 없습니다.</returns>
    void RecordSuccess(ConversionReceipt receipt);

    /// <summary>
    /// 예상 가능한 환산 실패와 원인을 관찰 기록으로 남깁니다.
    /// </summary>
    /// <param name="request">실패한 환산 요청입니다.</param>
    /// <param name="problem">실패 종류와 설명입니다.</param>
    /// <returns>기록만 수행하므로 반환값은 없습니다.</returns>
    void RecordFailure(ConversionRequest request, DomainError problem);
}

// 가장 단순한 Strategy: 기준 통화 자신은 1, 그 외에는 표에 정확히 존재하는 통화만 선택합니다.
sealed class ExactCurrencyRateSelectionPolicy : IRateSelectionPolicy
{
    /// <summary>
    /// 같은 통화에는 경계 규칙으로 1을 적용하고, 다른 통화는 표에서 정확히 일치하는 환율을 찾습니다.
    /// </summary>
    /// <param name="table">검색할 기준 통화별 환율 표입니다.</param>
    /// <param name="target">선택하려는 목표 통화입니다.</param>
    /// <returns>같은 통화면 1, 표에 있으면 해당 환율, 없으면 UnsupportedCurrency 실패를 반환합니다.</returns>
    public Result<decimal> SelectRate(ExchangeRateTable table, Currency target)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(target);

        if (target == table.BaseCurrency)
        {
            return Result<decimal>.Success(1m);
        }

        // out var는 찾은 환율을 새 지역 변수 rate에 받으며, ?: 조건 연산자는 bool에 따라 두 Result 중 하나를 고릅니다.
        return table.TryGetRate(target, out var rate)
            ? Result<decimal>.Success(rate)
            : Result<decimal>.Failure(
                new DomainError(
                    ErrorKind.UnsupportedCurrency,
                    $"{table.BaseCurrency.Code}에서 {target.Code}(으)로 가는 환율이 없습니다."));
    }
}

// Application Service는 한 유스케이스의 순서를 조정합니다. HTTP·JSON·콘솔 구현 세부사항은 알지 못합니다.
sealed class ConvertCurrencyService
{
    private readonly IExchangeRateGateway _gateway;
    private readonly IRateSelectionPolicy _selectionPolicy;
    private readonly IConversionAudit _audit;

    /// <summary>
    /// 환율 조회, 선택 정책, 감사 기록 역할을 외부에서 주입받아 환산 유스케이스를 조립합니다.
    /// </summary>
    /// <param name="gateway">외부 환율을 Domain 모델로 가져오는 Port 구현입니다.</param>
    /// <param name="selectionPolicy">가져온 표에서 적용 환율을 고르는 Strategy입니다.</param>
    /// <param name="audit">성공과 실패를 기록하는 관찰 Port 구현입니다.</param>
    /// <remarks>생성자는 의존성을 보관하므로 반환값은 없습니다. DI 덕분에 테스트에서는 가짜 구현을 넣을 수 있습니다.</remarks>
    public ConvertCurrencyService(
        IExchangeRateGateway gateway,
        IRateSelectionPolicy selectionPolicy,
        IConversionAudit audit)
    {
        // ?? throw는 왼쪽 의존성이 null일 때 즉시 구성 오류를 던지고, nameof는 매개변수 이름을 안전하게 문자열로 얻습니다.
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _selectionPolicy = selectionPolicy ?? throw new ArgumentNullException(nameof(selectionPolicy));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    /// <summary>
    /// 환율 표 조회, 환율 선택, 금액 계산을 순서대로 수행하고 예상 실패는 Result로 전달합니다.
    /// </summary>
    /// <param name="request">검증된 원본 금액과 목표 통화가 들어 있는 환산 요청입니다.</param>
    /// <param name="cancellationToken">대기 중인 HTTP 작업을 포함해 전체 유스케이스를 취소할 신호입니다.</param>
    /// <returns>성공하면 환산 영수증을, 조회 또는 정책 실패면 오류를 담아 완료되는 Task를 반환합니다.</returns>
    public async Task<Result<ConversionReceipt>> ConvertAsync(
        ConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // async/await는 네트워크를 기다리는 동안 스레드를 붙잡지 않고, 완료 뒤 같은 흐름에서 결과를 이어 처리합니다.
        var tableResult = await _gateway.GetLatestRatesAsync(
            request.Source.Currency,
            cancellationToken);

        if (!tableResult.IsSuccess)
        {
            // !는 앞선 성공 검사로 Problem이 반드시 있음을 컴파일러에 알려 주는 null-forgiving 연산자입니다.
            var problem = tableResult.Problem!;
            _audit.RecordFailure(request, problem);
            return Result<ConversionReceipt>.Failure(problem);
        }

        var table = tableResult.Value!;
        var rateResult = _selectionPolicy.SelectRate(table, request.Target);
        if (!rateResult.IsSuccess)
        {
            var problem = rateResult.Problem!;
            _audit.RecordFailure(request, problem);
            return Result<ConversionReceipt>.Failure(problem);
        }

        var rate = rateResult.Value;

        decimal convertedAmount;
        try
        {
            // checked는 아주 큰 입력과 환율의 곱이 decimal 범위를 넘을 때 잘못된 값 대신 예외로 감지하게 합니다.
            convertedAmount = checked(request.Source.Amount * rate);
        }
        catch (OverflowException)
        {
            var problem = new DomainError(
                ErrorKind.Validation,
                "환산 결과가 decimal로 표현할 수 있는 범위를 벗어났습니다.");
            _audit.RecordFailure(request, problem);
            return Result<ConversionReceipt>.Failure(problem);
        }

        var convertedMoneyResult = Money.Create(
            convertedAmount,
            request.Target);

        if (!convertedMoneyResult.IsSuccess)
        {
            var problem = convertedMoneyResult.Problem!;
            _audit.RecordFailure(request, problem);
            return Result<ConversionReceipt>.Failure(problem);
        }

        var receipt = new ConversionReceipt(
            request.Source,
            convertedMoneyResult.Value!,
            rate,
            table.RateDate);

        _audit.RecordSuccess(receipt);
        return Result<ConversionReceipt>.Success(receipt);
    }
}
