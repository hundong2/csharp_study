using SupplierQuoteExercise.Application.Ports;
using SupplierQuoteExercise.Domain;

namespace SupplierQuoteExercise.Application;

/// <summary>
/// 여러 공급업체 요청 시작, 완료 순서 수집, 정책 선택, 최종 저장을 조율하는 Application Service입니다.
/// 각 세부 구현은 Port 뒤에 두어 유스케이스의 순서와 오류 경계만 책임집니다.
/// </summary>
public sealed class CollectSupplierQuotesService
{
    private readonly IReadOnlyList<ISupplierQuoteGateway> _gateways;
    private readonly IQuoteSelectionPolicy _selectionPolicy;
    private readonly IQuoteRoundRepository _repository;

    /// <summary>
    /// 유스케이스에 필요한 Gateway, 선택 Strategy, Repository Port를 생성자 주입으로 받습니다.
    /// 세 파라미터로 협력자를 초기화하며 생성자는 별도의 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="gateways">동시에 견적을 요청할 하나 이상의 공급업체 Port입니다.</param>
    /// <param name="selectionPolicy">성공 견적 중 최종 하나를 고르는 Strategy입니다.</param>
    /// <param name="repository">완성 report를 저장할 Repository Port입니다.</param>
    public CollectSupplierQuotesService(
        IEnumerable<ISupplierQuoteGateway> gateways,
        IQuoteSelectionPolicy selectionPolicy,
        IQuoteRoundRepository repository)
    {
        ArgumentNullException.ThrowIfNull(gateways);
        // var는 오른쪽 식에서 형식이 분명할 때 compiler가 정적 형식을 추론하며 dynamic이 아닙니다.
        // ToArray는 호출자가 원본 목록을 나중에 바꿔도 service 의존성이 변하지 않도록 snapshot을 만듭니다.
        var gatewaySnapshot = gateways.ToArray();
        if (gatewaySnapshot.Length == 0)
        {
            throw new ArgumentException("공급업체 gateway가 하나 이상 필요합니다.", nameof(gateways));
        }

        // `gateway => ...`는 각 원소를 검사하는 짧은 이름 없는 함수인 lambda입니다.
        // `is null`은 reference가 실제 null인지 검사하는 constant pattern입니다.
        if (gatewaySnapshot.Any(gateway => gateway is null))
        {
            throw new ArgumentException("gateway 목록에 null을 넣을 수 없습니다.", nameof(gateways));
        }

        if (gatewaySnapshot.Any(gateway => string.IsNullOrWhiteSpace(gateway.SupplierId)))
        {
            throw new ArgumentException("공급업체 ID는 비어 있거나 공백일 수 없습니다.", nameof(gateways));
        }

        var distinctIdCount = gatewaySnapshot
            .Select(gateway => gateway.SupplierId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctIdCount != gatewaySnapshot.Length)
        {
            throw new ArgumentException("공급업체 ID는 서로 달라야 합니다.", nameof(gateways));
        }

        _gateways = gatewaySnapshot;
        // ?? throw는 필수 의존성이 null인 잘못된 조립을 생성 시점에 바로 거절합니다.
        _selectionPolicy = selectionPolicy ?? throw new ArgumentNullException(nameof(selectionPolicy));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// 모든 공급업체 요청을 먼저 시작하고, 끝난 순서대로 부분 결과를 알린 뒤 최종 report를 저장합니다.
    /// </summary>
    /// <param name="request">이미 Domain 검증을 통과한 상품 견적 요청입니다.</param>
    /// <param name="onCompleted">각 공급업체가 끝날 때 즉시 호출할 선택적 callback입니다. `?`와 기본값 null은 생략 가능함을 뜻합니다.</param>
    /// <param name="cancellationToken">호출자가 전체 수집을 중단할 때 보내는 신호입니다. default는 취소 신호가 없다는 뜻입니다.</param>
    /// <returns>모든 예상 가능한 성공·실패와 선택 결과를 담은 최종 report를 반환합니다.</returns>
    public async Task<QuoteRoundReport> CollectAsync(
        QuoteRequest request,
        Action<QuoteAttempt>? onCompleted = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // using var는 method가 성공·실패·취소 중 어떻게 끝나도 linked CTS를 Dispose하게 하는 문법입니다.
        // caller 취소와 내부 첫 실패를 하나의 신호로 묶어 아직 끝나지 않은 sibling Gateway를 중단합니다.
        using var siblingCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Select의 lambda는 gateway마다 호출 Task를 하나 만들며, ToArray가 즉시 열거하므로 모두 먼저 시작됩니다.
        // 여기서 await하지 않고 Task 배열을 완성해야 느린 첫 공급업체가 다음 요청 시작을 막지 않습니다.
        var pendingTasks = _gateways
            .Select(gateway => RequestOneAsync(gateway, request, siblingCancellation.Token))
            .ToArray();
        var attempts = new List<QuoteAttempt>(pendingTasks.Length);

        try
        {
            // Task.WhenEach는 입력 Task를 완료되는 순서대로 내보내는 IAsyncEnumerable<Task<T>>를 만듭니다.
            // await foreach는 비동기 sequence에서 다음 완료 Task를 기다리되 현재 thread를 붙잡지 않는 문법입니다.
            await foreach (var completedTask in Task
                               .WhenEach(pendingTasks)
                               .WithCancellation(siblingCancellation.Token))
            {
                // 완료 Task를 다시 await하면 결과를 얻고, 취소나 예상 밖 예외는 Result로 숨기지 않고 전파됩니다.
                var attempt = await completedTask;
                attempts.Add(attempt);
                // ?.Invoke는 callback이 null이 아닐 때만 호출하는 null-conditional invocation입니다.
                onCompleted?.Invoke(attempt);
            }
        }
        catch
        {
            // 첫 Gateway/callback 예외가 전체 호출의 원인입니다. sibling을 취소한 뒤 모두 관찰해
            // background Task와 unobserved fault를 남기지 않되, 아래 throw로 첫 예외의 stack을 보존합니다.
            await CancelAndDrainAsync(siblingCancellation, pendingTasks);
            throw;
        }

        // 실패 Result는 report에 남기되, 선택 Strategy에는 성공 견적만 전달합니다.
        var successfulQuotes = attempts
            .Where(attempt => attempt.Outcome.IsSuccess)
            .Select(attempt => attempt.Outcome.Value)
            .ToArray();
        var selectedQuote = _selectionPolicy.Select(request, successfulQuotes);
        var report = QuoteRoundReport.Create(request, attempts, selectedQuote);

        await _repository.SaveAsync(report, cancellationToken);
        return report;
    }

    /// <summary>
    /// 한 Gateway 호출 결과에 공급업체 ID를 붙여 완료 순서에서 식별 가능한 QuoteAttempt로 바꿉니다.
    /// </summary>
    /// <param name="gateway">이번 호출을 수행할 구체 공급업체 Port입니다.</param>
    /// <param name="request">모든 공급업체에 동일하게 전달할 검증된 요청입니다.</param>
    /// <param name="cancellationToken">외부 호출 대기를 중단할 수 있는 신호입니다.</param>
    /// <returns>공급업체 ID와 Result를 묶은 QuoteAttempt Task를 반환합니다.</returns>
    private static async Task<QuoteAttempt> RequestOneAsync(
        ISupplierQuoteGateway gateway,
        QuoteRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await gateway.RequestQuoteAsync(request, cancellationToken);
        if (outcome.IsSuccess
            && !StringComparer.Ordinal.Equals(gateway.SupplierId, outcome.Value.SupplierId))
        {
            return new QuoteAttempt(
                gateway.SupplierId,
                Result<SupplierQuote>.Failure(
                    Error.External(
                        "supplier_quote.supplier_id_mismatch",
                        $"Gateway '{gateway.SupplierId}'가 다른 공급업체 ID "
                        + $"'{outcome.Value.SupplierId}'의 견적을 반환했습니다.")));
        }

        return new QuoteAttempt(gateway.SupplierId, outcome);
    }

    /// <summary>
    /// 첫 실패 뒤 linked token을 취소하고 모든 sibling Task를 끝까지 await해 fault를 관찰합니다.
    /// </summary>
    /// <param name="cancellationSource">아직 실행 중인 Gateway에 취소를 전파할 linked source입니다.</param>
    /// <param name="tasks">성공·실패·취소 여부와 관계없이 모두 관찰해야 할 Gateway Task 배열입니다.</param>
    /// <returns>모든 Task가 terminal 상태가 되고 그 예외가 관찰되면 완료되는 Task를 반환합니다.</returns>
    private static async Task CancelAndDrainAsync(
        CancellationTokenSource cancellationSource,
        IEnumerable<Task<QuoteAttempt>> tasks)
    {
        try
        {
            await cancellationSource.CancelAsync();
        }
        catch (Exception)
        {
            // 취소 callback의 부수 예외가 catch에 들어온 원래 Gateway/callback 예외를 가리지 않게 합니다.
        }

        foreach (var task in tasks)
        {
            try
            {
                await task;
            }
            catch (Exception)
            {
                // sibling의 fault/cancellation은 여기서 관찰했습니다. 호출자에게는 최초 원인을 다시 던집니다.
            }
        }
    }
}
