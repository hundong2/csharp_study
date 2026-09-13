using InventoryWatcherExercise.Application;
using InventoryWatcherExercise.Domain;
using InventoryWatcherExercise.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InventoryWatcherExercise.Tests;

/// <summary>
/// 외부 테스트 패키지 없이 핵심 도메인·애플리케이션·호스팅 계약을 검증하는 작은 테스트 러너입니다.
/// </summary>
public static class SelfTests
{
    /// <summary>
    /// 등록된 자체 테스트를 차례로 실행하고 통과/실패 결과를 콘솔에 요약합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>모두 통과하면 프로세스 종료 코드 0, 하나라도 실패하면 1을 반환합니다.</returns>
    public static async Task<int> RunAllAsync()
    {
        // Func<Task>는 "나중에 호출할 비동기 테스트 메서드"를 값처럼 보관하는 델리게이트 형식이다.
        // 튜플은 테스트 이름과 실행 함수를 한 쌍으로 묶어 별도 클래스를 만들지 않게 해 준다.
        (string Name, Func<Task> Run)[] tests =
        [
            ("유효한 재고 생성과 정규화", ValidInventoryItemIsNormalizedAsync),
            ("빈 SKU 도메인 검증", EmptySkuReturnsFailureAsync),
            ("음수 현재 수량 도메인 검증", NegativeQuantityReturnsFailureAsync),
            ("음수 재주문 기준 도메인 검증", NegativeReorderPointReturnsFailureAsync),
            ("임계값 Strategy 세 단계 분류", StrategyClassifiesAllLevelsAsync),
            ("값 객체 불변식 방어", ValueObjectsProtectInvariantsAsync),
            ("회차 보고서 방어적 복사", ReportTakesDefensiveSnapshotAsync),
            ("저재고 필터와 결정적 정렬", CycleFiltersAndOrdersAlertsAsync),
            ("경고 없음 시 Sink 미호출", HealthyInventorySkipsSinkAsync),
            ("취소 신호의 손실 없는 전파", CancellationRemainsDistinctAsync),
            ("Strategy 중간 취소 관찰", CancellationDuringClassificationIsObservedAsync),
            ("null 목록 Repository 계약 위반", NullRepositoryListThrowsAsync),
            ("null 항목 Repository 계약 위반", NullRepositoryItemThrowsAsync),
            ("중복 SKU Repository 계약 위반", DuplicateSkuThrowsAsync),
            ("정의되지 않은 Strategy 결과 거부", InvalidStrategyLevelThrowsAsync),
            ("잘못된 Options 시작 시 검증", InvalidOptionsFailAtStartupAsync),
            ("RunOnce 모든 결과의 Scope 경계", RunOnceManagesScopeAcrossOutcomesAsync),
            ("PeriodicTimer 취소와 해제", PeriodicTimerCancellationAndDisposalAsync),
            ("Worker 최대 회차 뒤 종료", WorkerStopsAtConfiguredCycleLimitAsync),
            ("Tick source 조기 종료", WorkerStopsWhenTickSourceEndsAsync),
            ("Tick 직후 취소 시 Scope 미생성", WorkerCancellationAfterTickSkipsScopeAsync),
            ("Worker 실패의 non-zero 종료 상태", WorkerFailureSetsNonZeroExitStatusAsync),
            ("다른 토큰 취소의 실패 분류", UnrelatedCancellationSetsNonZeroExitStatusAsync),
        ];

        var failed = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run();
                // 문자열 보간 `$"...{값}..."`은 테스트 이름과 실제 값을 한 문장에 넣는다.
                Console.WriteLine($"[PASS] {test.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine($"[FAIL] {test.Name}: {exception.GetType().Name} - {exception.Message}");
            }
        }

        Console.WriteLine($"자체 테스트: {tests.Length - failed}/{tests.Length} 통과");
        // 삼항 연산자 `조건 ? 참일 때 값 : 거짓일 때 값`으로 성공과 실패 종료 코드를 고른다.
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// 유효한 입력이 성공 Result와 공백 제거·대문자 SKU를 만드는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>검증을 동기적으로 마친 완료 Task를 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
    private static Task ValidInventoryItemIsNormalizedAsync()
    {
        var result = InventoryItem.Create(" cab-100 ", " 케이블 ", 3, 5);

        Assert(result.IsSuccess, "유효한 재고는 성공해야 합니다.");
        AssertEqual("CAB-100", result.Value.Sku, "SKU 정규화가 다릅니다.");
        AssertEqual("케이블", result.Value.Name, "상품 이름의 바깥 공백이 제거되어야 합니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 빈·허용되지 않은 SKU와 제어 문자가 든 이름이 실패 Result로 반환되는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>검증을 동기적으로 마친 완료 Task를 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
    private static Task EmptySkuReturnsFailureAsync()
    {
        var result = InventoryItem.Create("  ", "케이블", 3, 5);

        Assert(result.IsFailure, "빈 SKU는 실패해야 합니다.");
        AssertContains("SKU", result.Error, "실패 이유가 SKU를 설명해야 합니다.");
        Assert(
            InventoryItem.Create("BAD/SKU", "케이블", 3, 5).IsFailure,
            "허용 목록 밖의 SKU 문자는 로그·키 경계를 지키기 위해 실패해야 합니다.");
        Assert(
            InventoryItem.Create("CAB-100", "정상\n위조 로그", 3, 5).IsFailure,
            "상품 이름의 줄바꿈 제어 문자는 로그 삽입을 막기 위해 실패해야 합니다.");
        // `\u2028`처럼 `\u` 뒤 16진수 네 자리는 눈에 잘 안 보이는 Unicode 문자를 정확히 적는 escape 문법이다.
        Assert(
            InventoryItem.Create("CAB-100", "정상\u2028위조 로그", 3, 5).IsFailure,
            "Unicode 줄 구분자도 새 로그 줄처럼 보일 수 있어 실패해야 합니다.");
        Assert(
            InventoryItem.Create("CAB-100", "정상\u202Etxt.exe", 3, 5).IsFailure,
            "Unicode 방향 제어 문자는 화면의 읽는 순서를 속일 수 있어 실패해야 합니다.");
        Assert(
            InventoryItem.Create("CAB-100", "개발자\u200D도구", 3, 5).IsSuccess,
            "양방향 제어가 아닌 ZWJ까지 과도하게 거부하면 안 됩니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 음수와 상한 초과 현재 수량이 도메인 규칙에 따라 실패 Result가 되는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>검증을 동기적으로 마친 완료 Task를 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
    private static Task NegativeQuantityReturnsFailureAsync()
    {
        var result = InventoryItem.Create("CAB-100", "케이블", -1, 5);

        Assert(result.IsFailure, "음수 현재 수량은 실패해야 합니다.");
        AssertContains("현재 수량", result.Error, "오류가 잘못된 필드를 알려야 합니다.");
        Assert(
            InventoryItem.Create("CAB-100", "케이블", InventoryItem.MaximumQuantity + 1, 5).IsFailure,
            "현재 수량 상한을 넘으면 계산 overflow를 막기 위해 실패해야 합니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 음수와 상한 초과 재주문 기준이 도메인 규칙에 따라 실패 Result가 되는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>검증을 동기적으로 마친 완료 Task를 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
    private static Task NegativeReorderPointReturnsFailureAsync()
    {
        var result = InventoryItem.Create("CAB-100", "케이블", 1, -1);

        Assert(result.IsFailure, "음수 재주문 기준은 실패해야 합니다.");
        AssertContains("재주문 기준", result.Error, "오류가 잘못된 필드를 알려야 합니다.");
        Assert(
            InventoryItem.Create("CAB-100", "케이블", 1, InventoryItem.MaximumReorderPoint + 1).IsFailure,
            "재주문 기준 상한을 넘으면 보충량 계산 overflow를 막기 위해 실패해야 합니다.");

        var boundary = InventoryItem.Create("MAX-1", "상한 상품", 0, InventoryItem.MaximumReorderPoint);
        Assert(boundary.IsSuccess, "재주문 기준 상한 자체는 유효해야 합니다.");
        var boundaryAlert = new StockAlert(boundary.Value, StockLevel.Critical);
        AssertEqual(
            InventoryItem.MaximumQuantity,
            boundaryAlert.BaseReorderShortfallQuantity,
            "상한 경계의 기본 기준 부족분도 도메인 상한 안이어야 합니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 기본 Strategy가 품절·부족·정상을 경계값까지 올바르게 구분하는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>검증을 동기적으로 마친 완료 Task를 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
    private static Task StrategyClassifiesAllLevelsAsync()
    {
        var strategy = new DefaultStockThresholdStrategy();

        AssertEqual(StockLevel.Critical, strategy.Classify(CreateItem("A", 0, 5)), "수량 0은 Critical이어야 합니다.");
        AssertEqual(StockLevel.Low, strategy.Classify(CreateItem("B", 5, 5)), "재주문 기준과 같은 수량은 Low여야 합니다.");
        AssertEqual(StockLevel.Healthy, strategy.Classify(CreateItem("C", 6, 5)), "기준보다 많은 수량은 Healthy여야 합니다.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Result, StockAlert, WatchCycleReport가 외부에서 모순된 상태로 만들어지는 것을 거부하는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>모든 값 객체 불변식 검증을 동기적으로 마친 완료 Task를 반환합니다.</returns>
    private static Task ValueObjectsProtectInvariantsAsync()
    {
        // `이름: 값`은 named argument 문법으로, null이 어떤 선택 인수인지 호출부에서 분명하게 보여 준다.
        AssertThrows<ArgumentNullException>(
            () => Result<string>.Success(null!),
            expectedMessagePart: null);
        AssertThrows<ArgumentException>(
            () => Result<string>.Failure("  "),
            "오류 메시지");

        var failure = Result<string>.Failure("입력 오류");
        AssertThrows<InvalidOperationException>(
            () => _ = failure.Value,
            "성공 값");

        var item = CreateItem("LOW-1", 1, 2);
        AssertThrows<ArgumentNullException>(
            () => _ = new StockAlert(null!, StockLevel.Low),
            expectedMessagePart: null);
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ = new StockAlert(item, StockLevel.Healthy),
            "Low 또는 Critical");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ = new StockAlert(item, (StockLevel)999),
            "Low 또는 Critical");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ = new WatchCycleReport(-1, Array.Empty<StockAlert>()),
            "0 이상");
        AssertThrows<ArgumentNullException>(
            () => _ = new WatchCycleReport(0, null!),
            expectedMessagePart: null);

        return Task.CompletedTask;
    }

    /// <summary>
    /// WatchCycleReport가 받은 배열을 복사해 원본의 후속 변경과 분리되는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>방어적 복사 검증을 동기적으로 마친 완료 Task를 반환합니다.</returns>
    private static Task ReportTakesDefensiveSnapshotAsync()
    {
        var low = new StockAlert(CreateItem("LOW-1", 1, 2), StockLevel.Low);
        var critical = new StockAlert(CreateItem("OUT-1", 0, 2), StockLevel.Critical);
        var source = new[] { low, critical };
        var report = new WatchCycleReport(2, source);

        source[0] = new StockAlert(CreateItem("LOW-2", 1, 2), StockLevel.Low);

        AssertEqual("OUT-1", report.Alerts[0].Item.Sku, "보고서는 Critical을 먼저 두는 결정적 순서를 보장해야 합니다.");
        AssertEqual("LOW-1", report.Alerts[1].Item.Sku, "원본 배열 변경이 완료 보고서를 바꾸면 안 됩니다.");
        AssertThrows<ArgumentException>(
            () => _ = new WatchCycleReport(2, [low, low]),
            "같은 SKU");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Application Service가 정상 재고를 빼고 긴급도 내림차순·SKU 오름차순으로 경고를 고정하는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>한 회차와 Sink 전송 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task CycleFiltersAndOrdersAlertsAsync()
    {
        IReadOnlyList<InventoryItem> snapshot =
        [
            CreateItem("LOW-B", 2, 5),
            CreateItem("OK-Z", 9, 5),
            CreateItem("OUT-C", 0, 5),
            CreateItem("LOW-A", 5, 5),
        ];
        var repository = new StubRepository(_ => Task.FromResult(snapshot));
        var sink = new RecordingAlertSink();
        var cycle = new InventoryWatchCycle(repository, new DefaultStockThresholdStrategy(), sink);

        var report = await cycle.RunAsync(CancellationToken.None);

        AssertEqual(4, report.InspectedCount, "전체 네 건을 검사해야 합니다.");
        AssertSequenceEqual(
            ["OUT-C", "LOW-A", "LOW-B"],
            report.Alerts.Select(alert => alert.Item.Sku),
            "경고의 필터 또는 정렬 순서가 다릅니다.");
        AssertEqual(1, sink.Batches.Count, "한 회차 경고는 Sink에 한 번 전달되어야 합니다.");
        AssertSequenceEqual(
            report.Alerts.Select(alert => alert.Item.Sku),
            sink.Batches[0].Select(alert => alert.Item.Sku),
            "보고서와 Sink에 전달된 경고 순서가 같아야 합니다.");
    }

    /// <summary>
    /// 모든 재고가 정상일 때 빈 보고서만 반환하고 외부 Sink는 호출하지 않는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>빈 경고 경로와 Sink 호출 횟수 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task HealthyInventorySkipsSinkAsync()
    {
        IReadOnlyList<InventoryItem> snapshot = [CreateItem("OK-1", 6, 5)];
        var sink = new RecordingAlertSink();
        var cycle = new InventoryWatchCycle(
            new StubRepository(_ => Task.FromResult(snapshot)),
            new DefaultStockThresholdStrategy(),
            sink);

        var report = await cycle.RunAsync(CancellationToken.None);

        AssertEqual(1, report.InspectedCount, "정상 재고도 검사 건수에는 포함되어야 합니다.");
        AssertEqual(0, report.Alerts.Count, "정상 재고는 경고가 아니어야 합니다.");
        AssertEqual(0, sink.Batches.Count, "경고가 없으면 외부 Sink를 호출하지 않아야 합니다.");
    }

    /// <summary>
    /// 취소 토큰이 Repository까지 전달되고 OperationCanceledException이 다른 예외로 바뀌지 않는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>취소 예외를 관찰하고 검증할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task CancellationRemainsDistinctAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new StubRepository(token =>
        {
            cancellation.Cancel();
            return Task.FromCanceled<IReadOnlyList<InventoryItem>>(token);
        });
        var cycle = new InventoryWatchCycle(
            repository,
            new DefaultStockThresholdStrategy(),
            new RecordingAlertSink());

        // `이름: 값`은 named argument 문법으로, null이 어떤 선택 인수인지 호출부에서 분명하게 보여 준다.
        var exception = await AssertThrowsAsync<OperationCanceledException>(
            () => cycle.RunAsync(cancellation.Token),
            expectedMessagePart: null);

        AssertEqual(cancellation.Token, repository.LastToken, "호출자의 토큰이 Repository에 그대로 전달되어야 합니다.");
        AssertEqual(cancellation.Token, exception.CancellationToken, "취소 예외가 같은 토큰을 보존해야 합니다.");
    }

    /// <summary>
    /// 마지막 Strategy 판정 중 취소되어도 Healthy 결과로 성공 처리하거나 Sink를 호출하지 않는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>판정 중 취소 예외와 Sink 미호출을 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task CancellationDuringClassificationIsObservedAsync()
    {
        using var cancellation = new CancellationTokenSource();
        IReadOnlyList<InventoryItem> snapshot = [CreateItem("OK-1", 6, 5)];
        var sink = new RecordingAlertSink();
        var cycle = new InventoryWatchCycle(
            new StubRepository(_ => Task.FromResult(snapshot)),
            new CancelingStrategy(cancellation),
            sink);

        var exception = await AssertThrowsAsync<OperationCanceledException>(
            () => cycle.RunAsync(cancellation.Token),
            expectedMessagePart: null);

        AssertEqual(cancellation.Token, exception.CancellationToken, "판정 뒤 확인도 호출자의 토큰을 보존해야 합니다.");
        AssertEqual(0, sink.Batches.Count, "취소된 회차는 Sink를 호출하지 않아야 합니다.");
    }

    /// <summary>
    /// Repository가 null 목록을 반환하면 Application Service가 계약 위반 예외를 내는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>계약 위반 예외를 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task NullRepositoryListThrowsAsync()
    {
        var repository = new StubRepository(
            _ => Task.FromResult<IReadOnlyList<InventoryItem>>(null!));
        var cycle = new InventoryWatchCycle(
            repository,
            new DefaultStockThresholdStrategy(),
            new RecordingAlertSink());

        await AssertThrowsAsync<InvalidOperationException>(
            () => cycle.RunAsync(CancellationToken.None),
            "목록은 null");
    }

    /// <summary>
    /// Repository 목록 안의 null 항목을 조용히 무시하지 않고 계약 위반으로 드러내는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>계약 위반 예외를 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task NullRepositoryItemThrowsAsync()
    {
        IReadOnlyList<InventoryItem> snapshot = [CreateItem("A", 1, 1), null!];
        var repository = new StubRepository(_ => Task.FromResult(snapshot));
        var cycle = new InventoryWatchCycle(
            repository,
            new DefaultStockThresholdStrategy(),
            new RecordingAlertSink());

        await AssertThrowsAsync<InvalidOperationException>(
            () => cycle.RunAsync(CancellationToken.None),
            "항목은 null");
    }

    /// <summary>
    /// 같은 SKU가 두 번 오면 이중 경고 대신 Repository 계약 위반을 알리는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>중복 계약 위반 예외를 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task DuplicateSkuThrowsAsync()
    {
        IReadOnlyList<InventoryItem> snapshot =
        [
            CreateItem("same", 1, 2),
            CreateItem(" SAME ", 2, 3),
        ];
        var repository = new StubRepository(_ => Task.FromResult(snapshot));
        var cycle = new InventoryWatchCycle(
            repository,
            new DefaultStockThresholdStrategy(),
            new RecordingAlertSink());

        await AssertThrowsAsync<InvalidOperationException>(
            () => cycle.RunAsync(CancellationToken.None),
            "중복");
    }

    /// <summary>
    /// Strategy가 정의되지 않은 enum 숫자를 반환하면 경고로 오인하지 않고 계약 위반으로 거부하는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>Strategy 계약 위반 예외와 Sink 미호출을 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task InvalidStrategyLevelThrowsAsync()
    {
        IReadOnlyList<InventoryItem> snapshot = [CreateItem("LOW-1", 1, 2)];
        var sink = new RecordingAlertSink();
        var cycle = new InventoryWatchCycle(
            new StubRepository(_ => Task.FromResult(snapshot)),
            new InvalidLevelStrategy(),
            sink);

        await AssertThrowsAsync<InvalidOperationException>(
            () => cycle.RunAsync(CancellationToken.None),
            "Strategy 계약 위반");
        AssertEqual(0, sink.Batches.Count, "잘못된 Strategy 결과는 Sink에 도달하면 안 됩니다.");
    }

    /// <summary>
    /// 범위를 벗어난 설정이 첫 타이머 실행이 아니라 Generic Host 시작 단계에서 거부되는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>OptionsValidationException을 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task InvalidOptionsFailAtStartupAsync()
    {
        // 객체/딕셔너리 initializer는 생성 직후 속성과 키 값을 이름으로 한꺼번에 채우는 문법이다.
        var settings = new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        };
        var builder = Host.CreateApplicationBuilder(settings);
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{InventoryWatcherOptions.SectionName}:IntervalMilliseconds"] = "0",
                [$"{InventoryWatcherOptions.SectionName}:MaxCycles"] = "2",
            });
        builder.Services.AddInventoryWatcher(builder.Configuration);
        // 옵션을 읽는 Worker를 제거해도 시작이 실패해야 ValidateOnStart 자체를 검증한 것이다.
        builder.Services.RemoveAll<IHostedService>();

        using var host = builder.Build();
        await AssertThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(),
            "IntervalMilliseconds");

        var typoBuilder = Host.CreateApplicationBuilder(settings);
        typoBuilder.Logging.ClearProviders();
        typoBuilder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{InventoryWatcherOptions.SectionName}:IntervalMilliseconds"] = "25",
                [$"{InventoryWatcherOptions.SectionName}:MaxCycles"] = "2",
                [$"{InventoryWatcherOptions.SectionName}:MaxCylces"] = "2",
            });
        typoBuilder.Services.AddInventoryWatcher(typoBuilder.Configuration);
        typoBuilder.Services.RemoveAll<IHostedService>();

        using var typoHost = typoBuilder.Build();
        await AssertThrowsAsync<InvalidOperationException>(
            () => typoHost.StartAsync(),
            "MaxCylces");

        var emptyConfiguration = new ConfigurationBuilder().Build();
        AssertThrows<InvalidOperationException>(
            () => new ServiceCollection().AddInventoryWatcher(emptyConfiguration),
            InventoryWatcherOptions.SectionName);
    }

    /// <summary>
    /// RunOnceAsync가 사전 취소에는 Scope를 만들지 않고 성공·실패·회차 중 취소에는 만든 Scope를 모두 해제하는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>네 가지 실행 결과의 Scope 수명 검증이 끝날 때 완료되는 Task를 반환합니다.</returns>
    private static async Task RunOnceManagesScopeAcrossOutcomesAsync()
    {
        var counters = new ProbeCounters();
        var services = new ServiceCollection();
        services.AddSingleton(counters);
        services.AddScoped<IInventoryWatchCycle, ScopedProbeCycle>();

        await using var provider = services.BuildServiceProvider();
        using var lifetime = new TestApplicationLifetime();
        using var worker = new InventoryWatcherWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new ImmediateTickSource(),
            lifetime,
            new WorkerExitStatus(),
            Options.Create(new InventoryWatcherOptions { IntervalMilliseconds = 25, MaxCycles = 2 }),
            NullLogger<InventoryWatcherWorker>.Instance);

        using var alreadyCanceled = new CancellationTokenSource();
        alreadyCanceled.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(
            () => worker.RunOnceAsync(alreadyCanceled.Token),
            expectedMessagePart: null);
        AssertEqual(0, counters.Created, "사전 취소된 회차는 Scope를 만들지 않아야 합니다.");

        await worker.RunOnceAsync(CancellationToken.None);
        await worker.RunOnceAsync(CancellationToken.None);

        AssertEqual(2, counters.Created, "회차마다 Scoped 서비스가 새로 생성되어야 합니다.");
        AssertEqual(2, counters.Ran, "각 Scoped 서비스가 한 번씩 실행되어야 합니다.");
        AssertEqual(2, counters.Disposed, "각 회차가 끝날 때 Scope와 서비스가 해제되어야 합니다.");

        counters.Outcome = ProbeOutcome.Failure;
        await AssertThrowsAsync<InvalidOperationException>(
            () => worker.RunOnceAsync(CancellationToken.None),
            "의도한 회차 실패");
        AssertEqual(3, counters.Disposed, "예외가 발생한 회차의 Scope도 해제되어야 합니다.");

        using var duringCycleCancellation = new CancellationTokenSource();
        counters.Outcome = ProbeOutcome.Cancellation;
        counters.CancellationSource = duringCycleCancellation;
        await AssertThrowsAsync<OperationCanceledException>(
            () => worker.RunOnceAsync(duringCycleCancellation.Token),
            expectedMessagePart: null);
        AssertEqual(4, counters.Disposed, "회차 중 취소된 Scope도 해제되어야 합니다.");
    }

    /// <summary>
    /// 실제 PeriodicTimer 대기가 토큰 취소에는 예외로 끝나고 타이머 해제에는 false로 끝나는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>실제 타이머의 취소 및 해제 경계를 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task PeriodicTimerCancellationAndDisposalAsync()
    {
        var options = Options.Create(
            new InventoryWatcherOptions { IntervalMilliseconds = 60_000, MaxCycles = 1 });

        await using (var cancelableTimer = new PeriodicTimerTickSource(options))
        {
            using var cancellation = new CancellationTokenSource();
            var canceledWait = cancelableTimer.WaitForNextTickAsync(cancellation.Token).AsTask();
            cancellation.Cancel();
            var exception = await AssertThrowsAsync<OperationCanceledException>(
                () => canceledWait,
                expectedMessagePart: null);
            AssertEqual(cancellation.Token, exception.CancellationToken, "PeriodicTimer 취소 예외가 입력 토큰을 보존해야 합니다.");
        }

        var disposedTimer = new PeriodicTimerTickSource(options);
        var disposedWait = disposedTimer.WaitForNextTickAsync(CancellationToken.None).AsTask();
        await disposedTimer.DisposeAsync();
        AssertEqual(false, await disposedWait, "타이머 해제는 기다리던 호출에 false를 반환해야 합니다.");
    }

    /// <summary>
    /// 실제 Generic Host에서 Worker가 설정한 두 회차만 실행하고 StopApplication을 호출하는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>호스트가 스스로 종료되고 회차 수를 검증할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task WorkerStopsAtConfiguredCycleLimitAsync()
    {
        var settings = new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        };
        var builder = Host.CreateApplicationBuilder(settings);
        builder.Logging.ClearProviders();

        var counters = new ProbeCounters();
        var ticks = new ImmediateTickSource();
        builder.Services.AddSingleton(counters);
        builder.Services.AddSingleton<ITickSource>(ticks);
        builder.Services.AddSingleton<IOptions<InventoryWatcherOptions>>(
            Options.Create(new InventoryWatcherOptions { IntervalMilliseconds = 25, MaxCycles = 2 }));
        var exitStatus = new WorkerExitStatus();
        builder.Services.AddSingleton(exitStatus);
        builder.Services.AddScoped<IInventoryWatchCycle, ScopedProbeCycle>();
        builder.Services.AddHostedService<InventoryWatcherWorker>();

        using var host = builder.Build();
        await host.RunAsync().WaitAsync(TimeSpan.FromSeconds(3));

        AssertEqual(2, ticks.CallCount, "Worker는 두 번의 Tick만 요청해야 합니다.");
        AssertEqual(2, counters.Ran, "Worker는 설정한 두 회차만 실행해야 합니다.");
        AssertEqual(2, counters.Disposed, "Worker가 만든 두 Scope가 모두 해제되어야 합니다.");
        AssertEqual(0, exitStatus.ExitCode, "정상 완료한 Worker 종료 코드는 0이어야 합니다.");
    }

    /// <summary>
    /// Tick source가 최대 회차 전에 false를 반환하면 추가 Scope 없이 실제 완료 횟수로 정상 종료하는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>조기 Tick 종료 뒤 Host와 Scope 상태를 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task WorkerStopsWhenTickSourceEndsAsync()
    {
        var settings = new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        };
        var builder = Host.CreateApplicationBuilder(settings);
        builder.Logging.ClearProviders();

        var counters = new ProbeCounters();
        var ticks = new SequenceTickSource(true, false);
        builder.Services.AddSingleton(counters);
        builder.Services.AddSingleton<ITickSource>(ticks);
        builder.Services.AddSingleton<IOptions<InventoryWatcherOptions>>(
            Options.Create(new InventoryWatcherOptions { IntervalMilliseconds = 25, MaxCycles = 3 }));
        var exitStatus = new WorkerExitStatus();
        builder.Services.AddSingleton(exitStatus);
        builder.Services.AddScoped<IInventoryWatchCycle, ScopedProbeCycle>();
        builder.Services.AddHostedService<InventoryWatcherWorker>();

        using var host = builder.Build();
        await host.RunAsync().WaitAsync(TimeSpan.FromSeconds(3));

        AssertEqual(2, ticks.CallCount, "true 뒤 false까지 두 번의 Tick을 요청해야 합니다.");
        AssertEqual(1, counters.Ran, "false Tick 뒤에는 새 회차를 실행하지 않아야 합니다.");
        AssertEqual(1, counters.Disposed, "실행한 한 회차의 Scope만 해제되어야 합니다.");
        AssertEqual(0, exitStatus.ExitCode, "Tick source의 정상 종료는 실패 코드가 아니어야 합니다.");
    }

    /// <summary>
    /// Tick을 받은 직후 Host 종료가 요청되면 새 Scoped 회차를 시작하지 않는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>Host 취소 뒤 Scope 미생성과 정상 종료 상태를 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task WorkerCancellationAfterTickSkipsScopeAsync()
    {
        var settings = new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        };
        var builder = Host.CreateApplicationBuilder(settings);
        builder.Logging.ClearProviders();

        var counters = new ProbeCounters();
        var exitStatus = new WorkerExitStatus();
        builder.Services.AddSingleton(counters);
        builder.Services.AddSingleton<ITickSource, StopApplicationThenTickSource>();
        builder.Services.AddSingleton<IOptions<InventoryWatcherOptions>>(
            Options.Create(new InventoryWatcherOptions { IntervalMilliseconds = 25, MaxCycles = 2 }));
        builder.Services.AddSingleton(exitStatus);
        builder.Services.AddScoped<IInventoryWatchCycle, ScopedProbeCycle>();
        builder.Services.AddHostedService<InventoryWatcherWorker>();

        using var host = builder.Build();
        await host.RunAsync().WaitAsync(TimeSpan.FromSeconds(3));

        AssertEqual(0, counters.Created, "Tick 직후 취소되면 새 Scope를 만들지 않아야 합니다.");
        AssertEqual(0, counters.Ran, "취소 뒤 회차를 시작하면 안 됩니다.");
        AssertEqual(0, exitStatus.ExitCode, "Host가 요청한 취소는 Worker 실패가 아니어야 합니다.");
    }

    /// <summary>
    /// Scoped 회차의 처리되지 않은 실패가 Scope를 해제하고 .NET 10에서도 non-zero 종료 상태로 남는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>Host 종료 뒤 실패 상태와 Scope 해제를 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task WorkerFailureSetsNonZeroExitStatusAsync()
    {
        var settings = new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        };
        var builder = Host.CreateApplicationBuilder(settings);
        builder.Logging.ClearProviders();

        var counters = new ProbeCounters { Outcome = ProbeOutcome.Failure };
        var exitStatus = new WorkerExitStatus();
        builder.Services.AddSingleton(counters);
        builder.Services.AddSingleton<ITickSource>(new ImmediateTickSource());
        builder.Services.AddSingleton<IOptions<InventoryWatcherOptions>>(
            Options.Create(new InventoryWatcherOptions { IntervalMilliseconds = 25, MaxCycles = 2 }));
        builder.Services.AddSingleton(exitStatus);
        builder.Services.AddScoped<IInventoryWatchCycle, ScopedProbeCycle>();
        builder.Services.AddHostedService<InventoryWatcherWorker>();

        using var host = builder.Build();
        await host.RunAsync().WaitAsync(TimeSpan.FromSeconds(3));

        AssertEqual(1, exitStatus.ExitCode, "처리하지 못한 Worker 예외는 종료 코드 1로 기록되어야 합니다.");
        AssertEqual(1, counters.Ran, "실패 뒤 추가 회차를 실행하면 안 됩니다.");
        AssertEqual(1, counters.Disposed, "실패한 회차의 Scope도 Host 종료 전에 해제되어야 합니다.");
    }

    /// <summary>
    /// Host 종료와 겹쳐도 다른 토큰의 OperationCanceledException은 정상 종료로 숨기지 않는지 검증합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>다른 취소 원인의 실패 상태와 Scope 해제를 확인할 때 완료되는 Task를 반환합니다.</returns>
    private static async Task UnrelatedCancellationSetsNonZeroExitStatusAsync()
    {
        var settings = new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = Environments.Production,
        };
        var builder = Host.CreateApplicationBuilder(settings);
        builder.Logging.ClearProviders();

        var counters = new ProbeCounters { Outcome = ProbeOutcome.UnrelatedCancellation };
        var exitStatus = new WorkerExitStatus();
        builder.Services.AddSingleton(counters);
        builder.Services.AddSingleton<ITickSource>(new ImmediateTickSource());
        builder.Services.AddSingleton<IOptions<InventoryWatcherOptions>>(
            Options.Create(new InventoryWatcherOptions { IntervalMilliseconds = 25, MaxCycles = 2 }));
        builder.Services.AddSingleton(exitStatus);
        builder.Services.AddScoped<IInventoryWatchCycle, ScopedProbeCycle>();
        builder.Services.AddHostedService<InventoryWatcherWorker>();

        using var host = builder.Build();
        counters.BeforeUnrelatedCancellation = host.Services
            .GetRequiredService<IHostApplicationLifetime>()
            .StopApplication;
        await host.RunAsync().WaitAsync(TimeSpan.FromSeconds(3));

        AssertEqual(1, exitStatus.ExitCode, "다른 토큰의 취소 예외는 Worker 실패 코드 1이어야 합니다.");
        AssertEqual(1, counters.Disposed, "다른 취소 원인의 실패에서도 Scope가 해제되어야 합니다.");
    }

    /// <summary>
    /// 테스트 입력을 도메인 팩터리로 만들고, 테스트 자체의 잘못된 시드는 즉시 예외로 드러냅니다.
    /// </summary>
    /// <param name="sku">테스트에서 사용할 상품 식별자입니다.</param>
    /// <param name="quantity">테스트에서 사용할 현재 수량입니다.</param>
    /// <param name="reorderPoint">테스트에서 사용할 재주문 기준입니다.</param>
    /// <returns>도메인 검증을 통과한 재고 항목을 반환합니다.</returns>
    private static InventoryItem CreateItem(string sku, int quantity, int reorderPoint)
    {
        var result = InventoryItem.Create(sku, $"상품-{sku.Trim()}", quantity, reorderPoint);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"테스트 시드 오류: {result.Error}");
        }

        return result.Value;
    }

    /// <summary>
    /// 조건이 거짓이면 읽기 쉬운 테스트 실패 예외를 발생시킵니다.
    /// </summary>
    /// <param name="condition">반드시 참이어야 하는 검증 조건입니다.</param>
    /// <param name="message">조건이 거짓일 때 표시할 실패 이유입니다.</param>
    /// <returns>조건이 참이면 값을 반환하지 않고 정상 종료합니다.</returns>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 기대값과 실제 값이 같은지 기본 동등성 비교기로 검증합니다.
    /// </summary>
    /// <typeparam name="T">비교할 두 값의 공통 형식입니다.</typeparam>
    /// <param name="expected">테스트가 기대하는 값입니다.</param>
    /// <param name="actual">실제 코드가 만든 값입니다.</param>
    /// <param name="message">두 값이 다를 때 표시할 실패 이유입니다.</param>
    /// <returns>두 값이 같으면 값을 반환하지 않고 정상 종료합니다.</returns>
    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} 기대={expected}, 실제={actual}");
        }
    }

    /// <summary>
    /// 두 열거 가능한 값의 항목과 순서가 모두 같은지 검증합니다.
    /// </summary>
    /// <typeparam name="T">목록 안에서 비교할 항목 형식입니다.</typeparam>
    /// <param name="expected">기대하는 항목 순서입니다.</param>
    /// <param name="actual">실제 코드가 만든 항목 순서입니다.</param>
    /// <param name="message">항목 또는 순서가 다를 때 표시할 실패 이유입니다.</param>
    /// <returns>두 시퀀스가 같으면 값을 반환하지 않고 정상 종료합니다.</returns>
    private static void AssertSequenceEqual<T>(
        IEnumerable<T> expected,
        IEnumerable<T> actual,
        string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"{message} 기대=[{string.Join(", ", expected)}], 실제=[{string.Join(", ", actual)}]");
        }
    }

    /// <summary>
    /// 문자열에 기대한 문구가 포함되는지 검증하며 null도 안전하게 실패로 처리합니다.
    /// </summary>
    /// <param name="expectedPart">포함되어야 할 문구입니다.</param>
    /// <param name="actual">검사할 nullable 문자열입니다.</param>
    /// <param name="message">문구가 없을 때 표시할 실패 이유입니다.</param>
    /// <returns>문구가 포함되어 있으면 값을 반환하지 않고 정상 종료합니다.</returns>
    private static void AssertContains(string expectedPart, string? actual, string message)
    {
        if (actual is null || !actual.Contains(expectedPart, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message} 실제={actual ?? "<null>"}");
        }
    }

    /// <summary>
    /// 동기 작업이 지정한 예외 형식을 던지는지와 선택적 메시지 일부를 검증합니다.
    /// </summary>
    /// <typeparam name="TException">기대하는 예외 형식입니다.</typeparam>
    /// <param name="action">예외가 발생해야 하는 동기 작업입니다.</param>
    /// <param name="expectedMessagePart">메시지에 포함되어야 할 문구이며, 메시지를 검사하지 않으면 null입니다.</param>
    /// <returns>추가 속성을 검사할 수 있도록 잡은 기대 예외를 반환합니다.</returns>
    private static TException AssertThrows<TException>(Action action, string? expectedMessagePart)
        where TException : Exception
    {
        // `where TException : Exception`은 이 generic 형식 인수가 Exception 계열이어야 한다는 제약이다.
        try
        {
            action();
        }
        catch (TException exception)
        {
            if (expectedMessagePart is not null)
            {
                AssertContains(expectedMessagePart, exception.Message, "예외 메시지가 계약 위반 원인을 설명해야 합니다.");
            }

            return exception;
        }

        // typeof는 generic 형식 인수가 런타임에서 어떤 Type인지 얻어 실패 메시지에 실제 예외 이름을 넣는다.
        throw new InvalidOperationException($"{typeof(TException).Name} 예외가 발생해야 합니다.");
    }

    /// <summary>
    /// 비동기 작업이 지정한 예외 형식을 던지는지와 선택적 메시지 일부를 검증합니다.
    /// </summary>
    /// <typeparam name="TException">기대하는 예외 형식입니다.</typeparam>
    /// <param name="action">예외가 발생해야 하는 비동기 작업입니다.</param>
    /// <param name="expectedMessagePart">메시지에 포함되어야 할 문구이며, 메시지를 검사하지 않으면 null입니다.</param>
    /// <returns>추가 속성을 검사할 수 있도록 잡은 기대 예외를 반환합니다.</returns>
    private static async Task<TException> AssertThrowsAsync<TException>(
        Func<Task> action,
        string? expectedMessagePart)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            if (expectedMessagePart is not null)
            {
                AssertContains(expectedMessagePart, exception.Message, "예외 메시지가 계약 위반 원인을 설명해야 합니다.");
            }

            return exception;
        }

        throw new InvalidOperationException($"{typeof(TException).Name} 예외가 발생해야 합니다.");
    }

    /// <summary>
    /// 판정 중 취소를 일으킨 뒤 Healthy를 반환해 Application Service의 마지막 취소 확인을 시험하는 Strategy입니다.
    /// </summary>
    private sealed class CancelingStrategy : IStockThresholdStrategy
    {
        private readonly CancellationTokenSource _cancellation;

        /// <summary>
        /// 판정 시 취소할 소스를 저장합니다. 생성자는 의존성만 저장하며 값을 반환하지 않습니다.
        /// </summary>
        /// <param name="cancellation">판정 도중 취소 신호를 발생시킬 테스트용 소스입니다.</param>
        public CancelingStrategy(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        /// <summary>
        /// 전달된 항목을 받은 뒤 토큰을 취소하고 Healthy를 반환합니다.
        /// </summary>
        /// <param name="item">호출 계약을 확인할 null이 아닌 재고 항목입니다.</param>
        /// <returns>취소 확인이 누락되면 성공으로 오인될 Healthy를 반환합니다.</returns>
        public StockLevel Classify(InventoryItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _cancellation.Cancel();
            return StockLevel.Healthy;
        }
    }

    /// <summary>
    /// 정의되지 않은 enum 숫자를 반환해 Strategy 출력 Port 검증을 시험하는 가짜 구현입니다.
    /// </summary>
    private sealed class InvalidLevelStrategy : IStockThresholdStrategy
    {
        /// <summary>
        /// 항목을 받은 뒤 계약에 없는 재고 단계 숫자를 반환합니다.
        /// </summary>
        /// <param name="item">호출 계약을 확인할 null이 아닌 재고 항목입니다.</param>
        /// <returns>실제 코드가 거부해야 하는 정의되지 않은 StockLevel 값을 반환합니다.</returns>
        public StockLevel Classify(InventoryItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return (StockLevel)999;
        }
    }

    /// <summary>
    /// 테스트가 원하는 응답을 함수로 주입할 수 있는 가짜 Repository입니다.
    /// </summary>
    private sealed class StubRepository : IInventoryRepository
    {
        private readonly Func<CancellationToken, Task<IReadOnlyList<InventoryItem>>> _handler;

        /// <summary>
        /// 호출할 때 사용할 응답 함수를 저장합니다. 생성자는 값을 반환하지 않습니다.
        /// </summary>
        /// <param name="handler">전달된 취소 토큰으로 목록 또는 예외를 만드는 테스트 함수입니다.</param>
        public StubRepository(Func<CancellationToken, Task<IReadOnlyList<InventoryItem>>> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>마지막 호출에서 받은 토큰이며 호출 전에는 기본 토큰입니다.</summary>
        public CancellationToken LastToken { get; private set; }

        /// <summary>
        /// 토큰을 기록한 뒤 테스트가 주입한 응답 함수를 호출합니다.
        /// </summary>
        /// <param name="cancellationToken">Application Service가 전달한 취소 신호입니다.</param>
        /// <returns>테스트별로 준비한 재고 목록 또는 실패 Task를 반환합니다.</returns>
        public Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken cancellationToken)
        {
            LastToken = cancellationToken;
            return _handler(cancellationToken);
        }
    }

    /// <summary>
    /// 받은 경고 묶음을 복사해 두어 테스트가 나중에 검사할 수 있는 가짜 Sink입니다.
    /// </summary>
    private sealed class RecordingAlertSink : ILowStockAlertSink
    {
        private readonly List<IReadOnlyList<StockAlert>> _batches = [];

        /// <summary>지금까지 전달받은 경고 묶음을 호출 순서대로 노출합니다.</summary>
        public IReadOnlyList<IReadOnlyList<StockAlert>> Batches => _batches;

        /// <summary>
        /// 취소를 확인하고 경고 배열의 복사본을 기록합니다.
        /// </summary>
        /// <param name="alerts">Application Service가 보낸 정렬된 경고입니다.</param>
        /// <param name="cancellationToken">기록 전에 확인할 취소 신호입니다.</param>
        /// <returns>기록이 끝난 완료 Task를 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
        public Task PublishAsync(IReadOnlyList<StockAlert> alerts, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _batches.Add(alerts.ToArray());
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Scope 생성·실행·해제 횟수를 스레드 안전하게 모으는 테스트용 계수기입니다.
    /// </summary>
    private sealed class ProbeCounters
    {
        private int _created;
        private int _ran;
        private int _disposed;

        /// <summary>다음 Scoped 회차가 성공, 일반 실패, 취소 중 어떤 결과를 낼지 지정합니다.</summary>
        public ProbeOutcome Outcome { get; set; }

        /// <summary>취소 결과에서 회차 중간에 신호를 발생시킬 소스이며 다른 결과에는 null입니다.</summary>
        public CancellationTokenSource? CancellationSource { get; set; }

        /// <summary>다른 토큰 취소 예외를 던지기 전에 Host 종료를 요청할 테스트 callback입니다.</summary>
        public Action? BeforeUnrelatedCancellation { get; set; }

        /// <summary>지금까지 생성된 Scoped 서비스 수를 반환합니다.</summary>
        // Volatile.Read와 ref는 다른 스레드가 갱신한 실제 필드 위치에서 최신 계수 값을 읽는다.
        public int Created => Volatile.Read(ref _created);

        /// <summary>지금까지 실행된 Scoped 서비스 수를 반환합니다.</summary>
        public int Ran => Volatile.Read(ref _ran);

        /// <summary>지금까지 해제된 Scoped 서비스 수를 반환합니다.</summary>
        public int Disposed => Volatile.Read(ref _disposed);

        /// <summary>
        /// Scoped 서비스가 생성될 때 생성 횟수를 원자적으로 하나 올립니다.
        /// 매개변수는 없습니다.
        /// </summary>
        /// <returns>값을 반환하지 않고 내부 생성 횟수만 변경합니다.</returns>
        public void RecordCreated()
        {
            // Interlocked.Increment는 Worker와 Host가 다른 스레드에서 움직여도 증가 연산을 잃지 않게 원자적으로 처리한다.
            Interlocked.Increment(ref _created);
        }

        /// <summary>
        /// Scoped 서비스가 실행될 때 실행 횟수를 원자적으로 하나 올립니다.
        /// 매개변수는 없습니다.
        /// </summary>
        /// <returns>값을 반환하지 않고 내부 실행 횟수만 변경합니다.</returns>
        public void RecordRun()
        {
            Interlocked.Increment(ref _ran);
        }

        /// <summary>
        /// Scoped 서비스가 해제될 때 해제 횟수를 원자적으로 하나 올립니다.
        /// 매개변수는 없습니다.
        /// </summary>
        /// <returns>값을 반환하지 않고 내부 해제 횟수만 변경합니다.</returns>
        public void RecordDisposed()
        {
            Interlocked.Increment(ref _disposed);
        }
    }

    /// <summary>
    /// Scoped 회차 가짜 구현이 만들어 낼 결과를 결정합니다.
    /// </summary>
    private enum ProbeOutcome
    {
        /// <summary>빈 성공 보고서를 반환합니다.</summary>
        Success,

        /// <summary>일반 예외를 던집니다.</summary>
        Failure,

        /// <summary>호출자의 토큰을 회차 안에서 취소합니다.</summary>
        Cancellation,

        /// <summary>Host 종료와 겹친 다른 토큰의 취소 예외를 던집니다.</summary>
        UnrelatedCancellation,
    }

    /// <summary>
    /// 생성과 비동기 해제를 계수해 DI Scope 수명을 눈으로 검증하게 해 주는 가짜 회차 서비스입니다.
    /// </summary>
    private sealed class ScopedProbeCycle : IInventoryWatchCycle, IAsyncDisposable
    {
        private readonly ProbeCounters _counters;

        /// <summary>
        /// 새 Scoped 인스턴스 생성을 기록하고 공용 계수기를 저장합니다. 생성자는 값을 반환하지 않습니다.
        /// </summary>
        /// <param name="counters">여러 인스턴스의 생성·실행·해제 횟수를 합칠 Singleton 계수기입니다.</param>
        public ScopedProbeCycle(ProbeCounters counters)
        {
            _counters = counters ?? throw new ArgumentNullException(nameof(counters));
            _counters.RecordCreated();
        }

        /// <summary>
        /// 한 회차 실행을 기록하고 테스트가 지정한 성공·실패·취소 결과를 만듭니다.
        /// </summary>
        /// <param name="cancellationToken">실행 전에 확인할 호스트 종료 신호입니다.</param>
        /// <returns>성공 모드에서는 검사와 경고가 0건인 완료 보고서를 반환하고, 다른 모드에서는 예외를 던집니다.</returns>
        public async Task<WatchCycleReport> RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _counters.RecordRun();

            if (_counters.Outcome == ProbeOutcome.Failure)
            {
                throw new InvalidOperationException("의도한 회차 실패");
            }

            if (_counters.Outcome == ProbeOutcome.Cancellation)
            {
                var source = _counters.CancellationSource
                    ?? throw new InvalidOperationException("취소 테스트에는 CancellationTokenSource가 필요합니다.");
                source.Cancel();
            }

            if (_counters.Outcome == ProbeOutcome.UnrelatedCancellation)
            {
                var beforeCancellation = _counters.BeforeUnrelatedCancellation
                    ?? throw new InvalidOperationException("다른 취소 테스트에는 Host 종료 callback이 필요합니다.");
                beforeCancellation();

                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Yield();
                }

                using var unrelatedCancellation = new CancellationTokenSource();
                unrelatedCancellation.Cancel();
                throw new OperationCanceledException(
                    "Host 토큰과 다른 원인의 의도한 취소입니다.",
                    innerException: null,
                    unrelatedCancellation.Token);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new WatchCycleReport(0, Array.Empty<StockAlert>());
        }

        /// <summary>
        /// Scope가 끝나 이 인스턴스가 해제되었음을 계수기에 기록합니다.
        /// 매개변수는 없습니다.
        /// </summary>
        /// <returns>기록이 끝난 완료 ValueTask를 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
        public ValueTask DisposeAsync()
        {
            _counters.RecordDisposed();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// 실제 시간을 기다리지 않고 매 호출마다 Tick을 발생시키는 결정적 테스트 시간 포트입니다.
    /// </summary>
    private sealed class ImmediateTickSource : ITickSource
    {
        private int _callCount;

        /// <summary>지금까지 Worker가 다음 Tick을 요청한 횟수를 반환합니다.</summary>
        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>
        /// Host 시작 흐름에 실행권을 한 번 양보한 뒤 즉시 다음 Tick을 반환합니다.
        /// </summary>
        /// <param name="cancellationToken">양보 전후에 확인할 호스트 종료 신호입니다.</param>
        /// <returns>항상 다음 주기가 있음을 뜻하는 true를 비동기로 반환합니다.</returns>
        public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            return true;
        }
    }

    /// <summary>
    /// 준비된 true/false 값을 순서대로 반환해 조기 타이머 종료를 실제 시간 없이 재현하는 시간 포트입니다.
    /// </summary>
    private sealed class SequenceTickSource : ITickSource
    {
        private readonly Queue<bool> _ticks;
        private int _callCount;

        /// <summary>
        /// 호출할 때 차례로 반환할 Tick 값들을 복사해 저장합니다. 생성자는 값을 반환하지 않습니다.
        /// </summary>
        /// <param name="ticks">순서대로 반환할 Tick 값이며, 모두 소진되면 false를 반환합니다.</param>
        // params는 호출자가 배열을 직접 만들지 않고 여러 값을 나열할 수 있게 하는 매개변수 문법이다.
        public SequenceTickSource(params bool[] ticks)
        {
            ArgumentNullException.ThrowIfNull(ticks);
            _ticks = new Queue<bool>(ticks);
        }

        /// <summary>지금까지 Worker가 다음 Tick을 요청한 횟수를 반환합니다.</summary>
        public int CallCount => Volatile.Read(ref _callCount);

        /// <summary>
        /// 준비된 다음 값을 꺼내고, 값이 없으면 타이머 종료를 뜻하는 false를 반환합니다.
        /// </summary>
        /// <param name="cancellationToken">값을 꺼내기 전에 확인할 호스트 종료 신호입니다.</param>
        /// <returns>준비된 다음 Tick 값 또는 값이 없을 때 false를 완료된 ValueTask로 반환합니다.</returns>
        public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            var next = _ticks.Count > 0 && _ticks.Dequeue();
            return ValueTask.FromResult(next);
        }
    }

    /// <summary>
    /// true Tick을 반환하기 직전에 Host 종료를 요청해 Tick과 Scope 생성 사이의 취소 경계를 시험합니다.
    /// </summary>
    private sealed class StopApplicationThenTickSource : ITickSource
    {
        private readonly IHostApplicationLifetime _applicationLifetime;

        /// <summary>
        /// 종료 요청을 전달할 Host 수명 제어기를 저장합니다. 생성자는 의존성만 저장하며 값을 반환하지 않습니다.
        /// </summary>
        /// <param name="applicationLifetime">Tick 직전에 StopApplication을 호출할 Host 수명 제어기입니다.</param>
        public StopApplicationThenTickSource(IHostApplicationLifetime applicationLifetime)
        {
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        }

        /// <summary>
        /// 현재 토큰을 확인하고 Host 종료를 요청한 뒤 그 취소가 전달되면 의도적으로 true Tick을 반환합니다.
        /// </summary>
        /// <param name="cancellationToken">종료 요청 전 상태를 확인할 Worker 취소 신호입니다.</param>
        /// <returns>Worker가 Tick 직후 취소를 다시 확인하게 만드는 true를 반환합니다.</returns>
        public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _applicationLifetime.StopApplication();

            // Host가 BackgroundService의 stoppingToken까지 취소할 실행 기회를 얻도록 비동기로 양보한다.
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
            }

            // 실제 PeriodicTimer는 취소 예외를 던지지만, 이 가짜는 방어적인 Tick 직후 확인 자체를 시험하려고 true를 돌려준다.
            return true;
        }
    }

    /// <summary>
    /// 수동 RunOnceAsync 테스트에 필요한 최소 Host 수명 제어기입니다.
    /// </summary>
    private sealed class TestApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        // target-typed new()는 왼쪽 필드 형식이 분명할 때 생성자 형식 이름을 반복하지 않는 문법이다.
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        /// <summary>수동 테스트에서는 이미 시작된 것으로 취급하는 취소되지 않는 토큰입니다.</summary>
        public CancellationToken ApplicationStarted => CancellationToken.None;

        /// <summary>StopApplication 호출 여부를 나타내는 토큰입니다.</summary>
        public CancellationToken ApplicationStopping => _stopping.Token;

        /// <summary>테스트용 종료 완료 여부를 나타내는 토큰입니다.</summary>
        public CancellationToken ApplicationStopped => _stopped.Token;

        /// <summary>
        /// 테스트 호스트의 중지·종료 토큰을 취소해 종료 요청을 기록합니다.
        /// 매개변수는 없습니다.
        /// </summary>
        /// <returns>값을 반환하지 않고 수명 토큰 상태만 변경합니다.</returns>
        public void StopApplication()
        {
            _stopping.Cancel();
            _stopped.Cancel();
        }

        /// <summary>
        /// 테스트 수명 제어기가 소유한 두 CancellationTokenSource 자원을 해제합니다.
        /// 매개변수는 없으며, 값을 반환하지 않고 자원만 정리합니다.
        /// </summary>
        public void Dispose()
        {
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }
}
