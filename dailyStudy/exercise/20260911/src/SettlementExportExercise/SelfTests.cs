using System.Collections;
using System.Globalization;

// file-scoped namespace는 이 파일의 테스트 러너와 테스트 대역을 같은 이름 공간에 넣습니다.
namespace SettlementExportExercise;

// 외부 테스트 패키지 없이 핵심 불변식과 수명 주기 경계를 실행하는 작은 자체 테스트 러너입니다.
internal static class SelfTests
{
    // target-typed `new(...)`는 왼쪽 DateOnly 형식에서 생성할 형식을 추론합니다.
    private static readonly DateOnly BusinessDate = new(2026, 9, 11);

    /// <summary>
    /// 모든 자체 테스트를 순서대로 실행하고 각 결과를 콘솔에 표시합니다.
    /// 매개변수는 없습니다.
    /// 반환값은 전부 통과하면 0, 하나라도 실패하면 1인 프로세스 종료 코드입니다.
    /// </summary>
    public static async Task<int> RunAsync()
    {
        // Func<Task>는 나중에 실행할 비동기 테스트 메서드를 값으로 보관하는 delegate 형식입니다.
        // tuple 배열은 테스트 이름과 실행 함수를 가볍게 한 쌍으로 묶습니다.
        (string Name, Func<Task> Body)[] tests =
        [
            ("요청 검증과 C# 14 field 정규화가 의존성 앞에서 동작한다", RequestValidationStopsBeforeDependenciesAsync),
            ("Domain 한 줄·금액 scale·culture가 직렬화 결과를 일치시킨다", DomainSerializationInvariantsKeepOutputsConsistentAsync),
            ("성공한 CSV는 정렬·escaping 뒤 commit되고 Dispose된다", SuccessfulCsvIsOrderedEscapedCommittedAndDisposedAsync),
            ("파이프 Strategy는 구분자와 역슬래시를 보존한다", PipeFormatterEscapesDelimiterAsync),
            ("Ready 정산이 없으면 세션을 열지 않는다", NoReadyRowsAvoidsSessionAsync),
            ("Formatter 예외는 그대로 전파되고 staged 세션은 abort된다", FormatterFailureAbortsAndDisposesAsync),
            ("쓰기 예외는 그대로 전파되고 staged 세션은 abort된다", WriteFailureAbortsAndDisposesAsync),
            ("이미 취소된 요청은 의존성을 호출하지 않는다", PreCanceledRequestTouchesNothingAsync),
            ("Repository 반환 직후 취소는 빈 Result보다 우선한다", CancellationAfterRepositoryReturnWinsOverEmptyResultAsync),
            ("쓰기 도중 취소는 부분 staging을 공개하지 않는다", CancellationDuringWriteAbortsAsync),
            ("commit 대기 중 취소는 공개 없이 abort한다", CancellationWhileCommitSuspendedAbortsAsync),
            ("원자 공개 뒤 늦은 취소는 성공을 뒤집지 않는다", CancellationAfterCommitKeepsSuccessAsync),
            ("공개 뒤 commit 응답 실패는 결과 불확실성을 드러낸다", CommitResponseFailureAfterPublishIsObservableAsync),
            ("공개 뒤 Dispose 실패는 결과 불확실성을 드러낸다", DisposeFailureAfterPublishIsObservableAsync),
            ("같은 목적지는 덮어쓰지 않고 첫 파일을 보존한다", DuplicateDestinationPreservesFirstAsync),
            ("동시 목적지 commit은 정확히 한 요청만 성공한다", ConcurrentDestinationCommitHasOneWinnerAsync),
            ("Repository 계약 위반은 세션을 열기 전에 실패한다", RepositoryContractViolationFailsBeforeSessionAsync),
            ("DisposeAsync는 멱등이고 terminal 세션 재사용을 막는다", DisposeIsIdempotentAndTerminalAsync),
            ("Formatter 누락·중복·null은 시작 시 실패한다", BadFormatterConfigurationFailsFastAsync),
        ];

        int passed = 0;

        foreach ((string name, Func<Task> body) in tests)
        {
            try
            {
                await body();
                passed++;
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[FAIL] {name}: {exception.GetType().Name} - {exception.Message}");
            }
        }

        Console.WriteLine($"self-test {passed}/{tests.Length} 통과");
        return passed == tests.Length ? 0 : 1;
    }

    /// <summary>
    /// 잘못된 이름과 형식이 Repository 전에 거부되고 정상 이름은 field accessor에서 Trim되는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task RequestValidationStopsBeforeDependenciesAsync()
    {
        // var는 오른쪽 생성식에서 정확한 형식을 추론합니다. 형식이 명확하고 반복이 긴 지역 변수에만 사용했습니다.
        var repository = new CountingRepository(new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]));
        var factory = new ProbeSessionFactory();
        SettlementExportService service = CreateService(repository, factory);

        Result<SettlementExportReceipt> missingName = await service.ExportAsync(
            "   ",
            BusinessDate,
            ExportFormat.Csv,
            CancellationToken.None);
        Result<SettlementExportReceipt> pathName = await service.ExportAsync(
            "../escape",
            BusinessDate,
            ExportFormat.Csv,
            CancellationToken.None);
        Result<SettlementExportReceipt> badFormat = await service.ExportAsync(
            "daily",
            BusinessDate,
            (ExportFormat)999,
            CancellationToken.None);
        Result<SettlementExportReceipt> reservedName = await service.ExportAsync(
            "CON",
            BusinessDate,
            ExportFormat.Csv,
            CancellationToken.None);

        Assert(!missingName.IsSuccess && missingName.Problem.Code == "export.name_required", "공백 이름 오류가 잘못됐습니다.");
        Assert(!pathName.IsSuccess && pathName.Problem.Code == "export.name_invalid", "경로 형태 이름을 거부해야 합니다.");
        Assert(!badFormat.IsSuccess && badFormat.Problem.Code == "export.format_invalid", "정의되지 않은 형식을 거부해야 합니다.");
        Assert(!reservedName.IsSuccess && reservedName.Problem.Code == "export.name_reserved", "예약 장치 이름을 거부해야 합니다.");
        Assert(repository.Calls == 0, "잘못된 요청은 Repository를 호출하면 안 됩니다.");
        Assert(factory.OpenedCount == 0, "잘못된 요청은 출력 세션을 열면 안 됩니다.");

        Result<SettlementExportRequest> normalized = SettlementExportRequest.Create(
            "  daily_export  ",
            BusinessDate,
            ExportFormat.Csv);

        Assert(normalized.IsSuccess, "공백만 양끝에 있는 안전한 이름은 성공해야 합니다.");
        Assert(normalized.Value.ExportName == "daily_export", "C# 14 field accessor가 이름을 Trim하지 않았습니다.");
    }

    /// <summary>
    /// ID 제어 문자와 과도한 금액 scale을 거부하고, 허용 금액은 culture와 무관하게 파일·receipt·로그에 일치하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task DomainSerializationInvariantsKeepOutputsConsistentAsync()
    {
        Result<SettlementEntry> multiLineId = SettlementEntry.Create(
            "PAY\nINJECT",
            "줄바꿈 ID 상점",
            10m,
            BusinessDate,
            SettlementStatus.Ready);
        Result<SettlementEntry> tooPrecise = SettlementEntry.Create(
            "PAY-BAD",
            "정밀도 오류 상점",
            0.001m,
            BusinessDate,
            SettlementStatus.Ready);

        Assert(!multiLineId.IsSuccess, "Domain-valid ID가 session의 한 줄 계약을 깨면 안 됩니다.");
        Assert(multiLineId.Problem.Code == "settlement.id_control_character", "ID 제어 문자 오류 코드가 잘못됐습니다.");
        Assert(!tooPrecise.IsSuccess, "소수 셋째 자리 금액을 허용하면 파일에서 값이 달라질 수 있습니다.");
        Assert(tooPrecise.Problem.Code == "settlement.amount_scale_invalid", "금액 scale 오류 코드가 잘못됐습니다.");

        var factory = new InMemorySettlementExportSessionFactory();
        SettlementExportService service = CreateService(
            new InMemorySettlementRepository([Ready("PAY-001", "정상 상점", 10.10m)]),
            factory);

        Result<SettlementExportReceipt> result = await service.ExportAsync(
            "amount_scale",
            BusinessDate,
            ExportFormat.Csv,
            CancellationToken.None);

        Assert(result.IsSuccess, "소수 둘째 자리 금액은 내보낼 수 있어야 합니다.");
        Assert(result.Value.TotalAmount == 10.10m, "receipt 금액이 원래 Domain 값과 달라졌습니다.");
        Assert(
            factory.GetPublishedLines("amount_scale.csv")[1] == "PAY-001,정상 상점,10.10,2026-09-11",
            "CSV 금액이 receipt와 다른 값으로 표현됐습니다.");

        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            // 현재 culture를 쉼표 소수점 환경으로 바꿔도 공개 로그는 점 소수점을 유지해야 합니다.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert(
                Program.FormatExportedLine(result.Value) == "[EXPORTED] amount_scale.csv rows=1 total=10.10",
                "콘솔 영수증이 PC culture에 따라 달라졌습니다.");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>
    /// 실제 Adapter로 CSV의 정렬·인용·합계·commit·Dispose 결과가 모두 일치하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task SuccessfulCsvIsOrderedEscapedCommittedAndDisposedAsync()
    {
        SettlementEntry[] entries =
        [
            Ready("PAY-002", "\"바다\"상점", 7_500m),
            Ready("PAY-001", "서울,상점", 12_000.50m),
            CreateEntry("PAY-003", "보류", 3_000m, BusinessDate, SettlementStatus.Held),
            CreateEntry("PAY-004", "다음 날", 4_000m, BusinessDate.AddDays(1), SettlementStatus.Ready),
        ];

        var repository = new InMemorySettlementRepository(entries);
        var factory = new InMemorySettlementExportSessionFactory();
        SettlementExportService service = CreateService(repository, factory);

        Result<SettlementExportReceipt> result = await service.ExportAsync(
            "daily",
            BusinessDate,
            ExportFormat.Csv,
            CancellationToken.None);

        string[] expected =
        [
            "settlement_id,merchant_name,amount,business_date",
            "PAY-001,\"서울,상점\",12000.50,2026-09-11",
            "PAY-002,\"\"\"바다\"\"상점\",7500.00,2026-09-11",
        ];

        Assert(result.IsSuccess, "유효한 CSV 내보내기가 실패했습니다.");
        Assert(result.Value.DestinationName == "daily.csv", "목적지 이름이 잘못됐습니다.");
        Assert(result.Value.ExportedCount == 2, "Ready이면서 같은 날짜인 두 건만 내보내야 합니다.");
        Assert(result.Value.TotalAmount == 19_500.50m, "영수증 합계가 잘못됐습니다.");
        Assert(factory.GetPublishedLines("daily.csv").SequenceEqual(expected, StringComparer.Ordinal), "CSV 내용이 예상과 다릅니다.");
        Assert(factory.OpenedCount == 1, "세션은 한 번 열려야 합니다.");
        Assert(factory.CommittedCount == 1, "성공 세션은 한 번 commit되어야 합니다.");
        Assert(factory.AbortedCount == 0, "commit된 세션을 abort하면 안 됩니다.");
        Assert(factory.DisposedCount == 1, "return 전에 DisposeAsync가 완료되어야 합니다.");
        Assert(factory.PublishedCount == 1, "최종 파일 하나만 보여야 합니다.");
    }

    /// <summary>
    /// PipeDelimited Strategy가 파이프와 역슬래시를 데이터로 복원 가능하게 escaping하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task PipeFormatterEscapesDelimiterAsync()
    {
        var repository = new InMemorySettlementRepository([Ready("PAY-001", "A|B\\C", 10m)]);
        var factory = new InMemorySettlementExportSessionFactory();
        SettlementExportService service = CreateService(repository, factory);

        Result<SettlementExportReceipt> result = await service.ExportAsync(
            "pipe_export",
            BusinessDate,
            ExportFormat.PipeDelimited,
            CancellationToken.None);

        string[] expected =
        [
            "settlement_id|merchant_name|amount|business_date",
            "PAY-001|A\\|B\\\\C|10.00|2026-09-11",
        ];

        Assert(result.IsSuccess, "파이프 내보내기가 실패했습니다.");
        Assert(result.Value.DestinationName == "pipe_export.txt", "파이프 Strategy 확장자가 잘못됐습니다.");
        Assert(factory.GetPublishedLines("pipe_export.txt").SequenceEqual(expected, StringComparer.Ordinal), "파이프 escaping이 잘못됐습니다.");
    }

    /// <summary>
    /// 같은 날짜에 Ready 항목이 없을 때 예상 가능한 Result를 반환하고 자원을 열지 않는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task NoReadyRowsAvoidsSessionAsync()
    {
        var repository = new InMemorySettlementRepository(
            [CreateEntry("PAY-001", "보류", 10m, BusinessDate, SettlementStatus.Held)]);
        var factory = new ProbeSessionFactory();
        SettlementExportService service = CreateService(repository, factory);

        Result<SettlementExportReceipt> result = await service.ExportAsync(
            "empty",
            BusinessDate,
            ExportFormat.Csv,
            CancellationToken.None);

        Assert(!result.IsSuccess, "Ready 항목이 없는데 성공하면 안 됩니다.");
        Assert(result.Problem.Code == "export.no_ready_settlements", "빈 결과 오류 코드가 잘못됐습니다.");
        Assert(factory.OpenedCount == 0, "쓸 데이터가 없으면 세션을 열지 않아야 합니다.");
    }

    /// <summary>
    /// 순수 변환 Strategy에서 생긴 예상 밖 예외가 숨겨지지 않고 await using이 staged 세션을 정리하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task FormatterFailureAbortsAndDisposesAsync()
    {
        var expectedException = new InvalidOperationException("formatter-bug");
        var repository = new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]);
        var factory = new ProbeSessionFactory();
        ISettlementFormatter[] formatters =
        [
            new ThrowingFormatter(ExportFormat.Csv, "csv", expectedException),
            new PipeSettlementFormatter(),
        ];
        var service = new SettlementExportService(repository, factory, formatters);

        InvalidOperationException caught = await AssertThrowsAsync<InvalidOperationException>(
            () => service.ExportAsync("broken", BusinessDate, ExportFormat.Csv, CancellationToken.None));

        Assert(ReferenceEquals(caught, expectedException), "Formatter 예외를 다른 예외로 바꾸거나 삼키면 안 됩니다.");
        Assert(factory.OpenedCount == 1, "Formatter 실패 전 세션은 열려야 합니다.");
        Assert(factory.AbortedCount == 1, "부분 staging은 abort되어야 합니다.");
        Assert(factory.DisposedCount == 1, "예외 경로에서도 DisposeAsync가 완료되어야 합니다.");
        Assert(factory.CommittedCount == 0 && factory.PublishedCount == 0, "Formatter 실패 파일을 공개하면 안 됩니다.");
    }

    /// <summary>
    /// Adapter의 두 번째 쓰기에서 I/O 예외가 나면 같은 예외가 전파되고 부분 파일이 공개되지 않는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task WriteFailureAbortsAndDisposesAsync()
    {
        var expectedException = new IOException("disk-full");
        var factory = new ProbeSessionFactory(
            beforeWrite: (writeNumber, _) =>
                writeNumber == 2
                    ? ValueTask.FromException(expectedException)
                    : ValueTask.CompletedTask);
        SettlementExportService service = CreateService(
            new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]),
            factory);

        IOException caught = await AssertThrowsAsync<IOException>(
            () => service.ExportAsync("io_failure", BusinessDate, ExportFormat.Csv, CancellationToken.None));

        Assert(ReferenceEquals(caught, expectedException), "원래 I/O 예외를 보존해야 합니다.");
        Assert(factory.AbortedCount == 1 && factory.DisposedCount == 1, "쓰기 실패 세션을 abort하고 Dispose해야 합니다.");
        Assert(factory.CommittedCount == 0 && factory.PublishedCount == 0, "부분 파일을 공개하면 안 됩니다.");
    }

    /// <summary>
    /// 처음부터 취소된 토큰이 입력이 잘못됐더라도 우선 적용되고 어떤 의존성도 호출하지 않는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task PreCanceledRequestTouchesNothingAsync()
    {
        var repository = new CountingRepository(new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]));
        var factory = new ProbeSessionFactory();
        SettlementExportService service = CreateService(repository, factory);
        // `using var`는 현재 메서드가 끝날 때 IDisposable인 CancellationTokenSource를 자동 Dispose하는 using declaration입니다.
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException caught = await AssertThrowsAsync<OperationCanceledException>(
            () => service.ExportAsync("   ", BusinessDate, ExportFormat.Csv, cancellation.Token));

        Assert(caught.CancellationToken == cancellation.Token, "호출자의 취소 토큰을 보존해야 합니다.");
        Assert(repository.Calls == 0, "선취소 요청은 Repository를 호출하면 안 됩니다.");
        Assert(factory.OpenedCount == 0, "선취소 요청은 세션을 열면 안 됩니다.");
    }

    /// <summary>
    /// Repository가 빈 목록을 반환하는 순간 취소되면 no-data Result가 아니라 원래 토큰의 취소 예외가 우선하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task CancellationAfterRepositoryReturnWinsOverEmptyResultAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new CancelingRepository(cancellation);
        var factory = new ProbeSessionFactory();
        SettlementExportService service = CreateService(repository, factory);

        OperationCanceledException caught = await AssertThrowsAsync<OperationCanceledException>(
            () => service.ExportAsync("cancel_after_read", BusinessDate, ExportFormat.Csv, cancellation.Token));

        Assert(caught.CancellationToken == cancellation.Token, "Repository 반환 경합에서도 호출자 토큰을 보존해야 합니다.");
        Assert(repository.Calls == 1, "Repository 조회 경계에서 한 번 취소해야 합니다.");
        Assert(factory.OpenedCount == 0, "조회 직후 취소는 출력 세션을 열면 안 됩니다.");
    }

    /// <summary>
    /// 헤더 다음 첫 데이터 줄 쓰기 직전에 취소되면 staging만 폐기되고 취소 토큰이 보존되는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task CancellationDuringWriteAbortsAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var factory = new ProbeSessionFactory(
            beforeWrite: (writeNumber, token) =>
            {
                if (writeNumber == 2)
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }

                return ValueTask.CompletedTask;
            });
        SettlementExportService service = CreateService(
            new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]),
            factory);

        OperationCanceledException caught = await AssertThrowsAsync<OperationCanceledException>(
            () => service.ExportAsync("cancel_write", BusinessDate, ExportFormat.Csv, cancellation.Token));

        Assert(caught.CancellationToken == cancellation.Token, "쓰기 중 취소 토큰이 달라졌습니다.");
        Assert(factory.AbortedCount == 1 && factory.DisposedCount == 1, "쓰기 취소 뒤 staging을 정리해야 합니다.");
        Assert(factory.CommittedCount == 0 && factory.PublishedCount == 0, "취소된 부분 파일을 공개하면 안 됩니다.");
    }

    /// <summary>
    /// 모든 줄을 쓴 뒤 CommitAsync가 gate에서 실제 대기하는 동안 취소되면 파일 없이 abort되는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task CancellationWhileCommitSuspendedAbortsAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var reachedCommit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCommitToContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new ProbeSessionFactory(
            beforeCommit: async token =>
            {
                reachedCommit.TrySetResult(true);
                await allowCommitToContinue.Task;
                token.ThrowIfCancellationRequested();
            });
        SettlementExportService service = CreateService(
            new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]),
            factory);

        Task<Result<SettlementExportReceipt>> exportTask = service.ExportAsync(
            "cancel_commit",
            BusinessDate,
            ExportFormat.Csv,
            cancellation.Token);

        await reachedCommit.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        allowCommitToContinue.TrySetResult(true);

        OperationCanceledException caught = await AssertThrowsAsync<OperationCanceledException>(
            () => exportTask);

        Assert(caught.CancellationToken == cancellation.Token, "commit 대기 중 취소 토큰이 달라졌습니다.");
        Assert(factory.AbortedCount == 1 && factory.DisposedCount == 1, "commit 대기 중 취소는 abort와 Dispose가 필요합니다.");
        Assert(factory.CommittedCount == 0 && factory.PublishedCount == 0, "원자 공개 전에 취소된 파일을 보이면 안 됩니다.");
    }

    /// <summary>
    /// Adapter가 원자 공개를 마친 직후 토큰이 취소되어도 서비스가 성공 영수증을 유지하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task CancellationAfterCommitKeepsSuccessAsync()
    {
        using var cancellation = new CancellationTokenSource();
        var factory = new ProbeSessionFactory(afterCommit: cancellation.Cancel);
        SettlementExportService service = CreateService(
            new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]),
            factory);

        Result<SettlementExportReceipt> result = await service.ExportAsync(
            "late_cancel",
            BusinessDate,
            ExportFormat.Csv,
            cancellation.Token);

        Assert(cancellation.IsCancellationRequested, "테스트가 commit 뒤 취소를 발생시키지 못했습니다.");
        Assert(result.IsSuccess, "이미 공개된 commit을 취소 실패로 뒤집으면 안 됩니다.");
        Assert(factory.CommittedCount == 1 && factory.PublishedCount == 1, "commit 결과가 보여야 합니다.");
        Assert(factory.AbortedCount == 0 && factory.DisposedCount == 1, "commit된 세션은 abort 없이 Dispose해야 합니다.");
    }

    /// <summary>
    /// 원자 공개 뒤 commit 응답 단계에서 예외가 나면 호출은 실패해도 최종 결과가 이미 보일 수 있음을 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task CommitResponseFailureAfterPublishIsObservableAsync()
    {
        var expectedException = new IOException("commit-response-lost");
        var factory = new ProbeSessionFactory(afterCommit: () => throw expectedException);
        SettlementExportService service = CreateService(
            new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]),
            factory);

        IOException caught = await AssertThrowsAsync<IOException>(
            () => service.ExportAsync("uncertain_commit", BusinessDate, ExportFormat.Csv, CancellationToken.None));

        Assert(ReferenceEquals(caught, expectedException), "commit 응답 예외를 보존해야 합니다.");
        Assert(factory.CommittedCount == 1 && factory.PublishedCount == 1, "응답 실패 전에 공개된 결과가 보여야 합니다.");
        Assert(factory.AbortedCount == 0 && factory.DisposedCount == 1, "공개된 세션을 abort하지 말고 Dispose해야 합니다.");
    }

    /// <summary>
    /// commit 성공 뒤 DisposeAsync가 실패하면 성공 Result 대신 정리 예외가 보이지만 파일은 이미 공개됐음을 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task DisposeFailureAfterPublishIsObservableAsync()
    {
        var expectedException = new IOException("dispose-failed");
        var factory = new ProbeSessionFactory(disposeException: expectedException);
        SettlementExportService service = CreateService(
            new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]),
            factory);

        IOException caught = await AssertThrowsAsync<IOException>(
            () => service.ExportAsync("uncertain_dispose", BusinessDate, ExportFormat.Csv, CancellationToken.None));

        Assert(ReferenceEquals(caught, expectedException), "DisposeAsync 예외를 다른 결과로 숨기면 안 됩니다.");
        Assert(factory.CommittedCount == 1 && factory.PublishedCount == 1, "Dispose 실패 전 commit 결과가 보여야 합니다.");
        Assert(factory.AbortedCount == 0 && factory.DisposedCount == 1, "commit된 결과를 abort하면 안 됩니다.");
    }

    /// <summary>
    /// 같은 목적지로 두 번 내보낼 때 두 번째 commit이 첫 파일을 덮어쓰지 않는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task DuplicateDestinationPreservesFirstAsync()
    {
        var factory = new InMemorySettlementExportSessionFactory();
        SettlementExportService firstService = CreateService(
            new InMemorySettlementRepository([Ready("PAY-001", "첫 상점", 10m)]),
            factory);
        SettlementExportService secondService = CreateService(
            new InMemorySettlementRepository([Ready("PAY-002", "두 번째 상점", 20m)]),
            factory);

        Result<SettlementExportReceipt> first = await firstService.ExportAsync(
            "same",
            BusinessDate,
            ExportFormat.Csv,
            CancellationToken.None);
        string[] firstSnapshot = factory.GetPublishedLines("same.csv").ToArray();

        await AssertThrowsAsync<IOException>(
            () => secondService.ExportAsync("same", BusinessDate, ExportFormat.Csv, CancellationToken.None));

        Assert(first.IsSuccess, "첫 내보내기는 성공해야 합니다.");
        Assert(factory.GetPublishedLines("same.csv").SequenceEqual(firstSnapshot, StringComparer.Ordinal), "두 번째 요청이 첫 파일을 덮어썼습니다.");
        Assert(factory.OpenedCount == 2, "두 요청 모두 세션을 열어야 합니다.");
        Assert(factory.CommittedCount == 1 && factory.PublishedCount == 1, "첫 파일 하나만 commit되어야 합니다.");
        Assert(factory.AbortedCount == 1 && factory.DisposedCount == 2, "실패한 두 번째 staging만 abort해야 합니다.");
    }

    /// <summary>
    /// 같은 최종 이름의 두 독립 세션을 gate에서 함께 commit해 정확히 한 파일만 공개되는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task ConcurrentDestinationCommitHasOneWinnerAsync()
    {
        var factory = new InMemorySettlementExportSessionFactory();
        ISettlementExportSession first = await factory.OpenAsync("race.csv", CancellationToken.None);
        ISettlementExportSession second = await factory.OpenAsync("race.csv", CancellationToken.None);
        await first.WriteLineAsync("first", CancellationToken.None);
        await second.WriteLineAsync("second", CancellationToken.None);

        // RunContinuationsAsynchronously는 gate를 연 스레드 안에서 두 continuation을 길게 연쇄 실행하지 않게 합니다.
        var releaseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int arrivals = 0;
        Action signalArrival = () =>
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                releaseGate.TrySetResult(true);
            }
        };

        Task<Exception?> firstAttempt = CommitAtSharedGateAsync(first, releaseGate.Task, signalArrival);
        Task<Exception?> secondAttempt = CommitAtSharedGateAsync(second, releaseGate.Task, signalArrival);
        Exception?[] outcomes = await Task.WhenAll(firstAttempt, secondAttempt).WaitAsync(TimeSpan.FromSeconds(5));

        Assert(outcomes.Count(outcome => outcome is null) == 1, "동시 commit 중 정확히 하나만 성공해야 합니다.");
        Assert(outcomes.Count(outcome => outcome is IOException) == 1, "패자는 중복 목적지 IOException이어야 합니다.");
        Assert(factory.PublishedCount == 1 && factory.CommittedCount == 1, "최종 파일과 commit은 하나여야 합니다.");
        Assert(factory.AbortedCount == 1 && factory.DisposedCount == 2, "패자만 abort하고 두 세션 모두 Dispose해야 합니다.");
    }

    /// <summary>
    /// null 목록·항목, 잘못된 날짜·상태·중복과 재열거 변화를 모두 최초 스냅샷에서 방어하는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task RepositoryContractViolationFailsBeforeSessionAsync()
    {
        SettlementEntry held = CreateEntry("PAY-001", "보류", 10m, BusinessDate, SettlementStatus.Held);
        SettlementEntry wrongDate = CreateEntry(
            "PAY-002",
            "다른 날짜",
            10m,
            BusinessDate.AddDays(1),
            SettlementStatus.Ready);
        SettlementEntry ready = Ready("PAY-003", "정상", 10m);

        // `null!`는 Adapter가 null 계약을 어기는 런타임 상황을 일부러 만들 때만 nullable 경고를 억제합니다.
        (string Name, IReadOnlyList<SettlementEntry>? Rows)[] invalidResults =
        [
            ("null list", null),
            ("null item", [null!]),
            ("held", [held]),
            ("wrong date", [wrongDate]),
            ("duplicate", [ready, ready]),
        ];

        foreach ((string name, IReadOnlyList<SettlementEntry>? rows) in invalidResults)
        {
            var factory = new ProbeSessionFactory();
            SettlementExportService service = CreateService(new StubRepository(rows), factory);

            await AssertThrowsAsync<InvalidOperationException>(
                () => service.ExportAsync("bad_adapter", BusinessDate, ExportFormat.Csv, CancellationToken.None));

            Assert(factory.OpenedCount == 0, $"{name}: Repository 계약 위반은 출력 세션 전에 실패해야 합니다.");
        }

        var changingRows = new ChangingReadOnlyList(
            firstEnumeration: [ready],
            laterEnumerations: [held]);
        var stableFactory = new InMemorySettlementExportSessionFactory();
        SettlementExportService stableService = CreateService(new StubRepository(changingRows), stableFactory);

        Result<SettlementExportReceipt> result = await stableService.ExportAsync(
            "snapshot_once",
            BusinessDate,
            ExportFormat.Csv,
            CancellationToken.None);

        Assert(result.IsSuccess, "최초 Repository 스냅샷의 정상 행은 성공해야 합니다.");
        Assert(changingRows.EnumerationCount == 1, "Repository 결과를 검증 뒤 다시 열거하면 TOCTOU가 생깁니다.");
        Assert(
            stableFactory.GetPublishedLines("snapshot_once.csv")[1].StartsWith("PAY-003,", StringComparison.Ordinal),
            "검증한 최초 스냅샷과 다른 행을 출력했습니다.");
    }

    /// <summary>
    /// 직접 연 세션을 commit 없이 두 번 Dispose해도 abort와 Dispose가 한 번뿐이고 재사용이 거부되는지 확인합니다.
    /// 매개변수는 없고 반환값은 검증 완료를 나타내는 Task입니다.
    /// </summary>
    private static async Task DisposeIsIdempotentAndTerminalAsync()
    {
        var factory = new InMemorySettlementExportSessionFactory();
        ISettlementExportSession session = await factory.OpenAsync("manual.csv", CancellationToken.None);
        await session.WriteLineAsync("header", CancellationToken.None);

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert(factory.AbortedCount == 1, "commit 없는 세션은 정확히 한 번 abort되어야 합니다.");
        Assert(factory.DisposedCount == 1, "DisposeAsync 반복 호출은 한 번만 기록되어야 합니다.");
        Assert(factory.PublishedCount == 0, "commit하지 않은 staged 파일은 보이면 안 됩니다.");

        await AssertThrowsAsync<InvalidOperationException>(
            () => session.WriteLineAsync("late", CancellationToken.None).AsTask());
        await AssertThrowsAsync<InvalidOperationException>(
            () => session.CommitAsync(CancellationToken.None).AsTask());
    }

    /// <summary>
    /// Strategy 누락·중복·null·잘못된 metadata를 거부하고 검증한 getter 값은 불변 snapshot으로 쓰는지 확인합니다.
    /// 매개변수는 없고 반환값은 모든 비동기 검증이 끝날 때 완료되는 Task입니다.
    /// </summary>
    private static async Task BadFormatterConfigurationFailsFastAsync()
    {
        var repository = new InMemorySettlementRepository([Ready("PAY-001", "상점", 10m)]);
        var factory = new ProbeSessionFactory();

        AssertThrows<ArgumentException>(
            () => _ = new SettlementExportService(
                repository,
                factory,
                [new CsvSettlementFormatter()]));

        AssertThrows<ArgumentException>(
            () => _ = new SettlementExportService(
                repository,
                factory,
                [new CsvSettlementFormatter(), new CsvSettlementFormatter(), new PipeSettlementFormatter()]));

        // null-forgiving `!`는 일부러 잘못된 런타임 구성을 만드는 테스트에서만 컴파일러 경고를 억제합니다.
        ISettlementFormatter[] containsNull =
        [
            new CsvSettlementFormatter(),
            null!,
            new PipeSettlementFormatter(),
        ];

        AssertThrows<ArgumentException>(
            () => _ = new SettlementExportService(repository, factory, containsNull));

        AssertThrows<ArgumentException>(
            () => _ = new SettlementExportService(
                repository,
                factory,
                [new MetadataFormatter((ExportFormat)999, "csv", "header"), new PipeSettlementFormatter()]));

        AssertThrows<ArgumentException>(
            () => _ = new SettlementExportService(
                repository,
                factory,
                [new MetadataFormatter(ExportFormat.Csv, "../csv", "header"), new PipeSettlementFormatter()]));

        AssertThrows<ArgumentException>(
            () => _ = new SettlementExportService(
                repository,
                factory,
                [new MetadataFormatter(ExportFormat.Csv, "verylongext", "header"), new PipeSettlementFormatter()]));

        AssertThrows<ArgumentException>(
            () => _ = new SettlementExportService(
                repository,
                factory,
                [new MetadataFormatter(ExportFormat.Csv, "csv", "bad\nheader"), new PipeSettlementFormatter()]));

        var driftingFormatter = new DriftingMetadataFormatter();
        var stableFactory = new InMemorySettlementExportSessionFactory();
        var stableService = new SettlementExportService(
            repository,
            stableFactory,
            [driftingFormatter, new PipeSettlementFormatter()]);

        Result<SettlementExportReceipt> result = await stableService.ExportAsync(
            "metadata_snapshot",
            BusinessDate,
            ExportFormat.Csv,
            CancellationToken.None);

        Assert(result.IsSuccess, "검증 시점의 안전한 Formatter metadata로 실행해야 합니다.");
        Assert(result.Value.DestinationName == "metadata_snapshot.csv", "검증 뒤 변한 위험한 확장자를 다시 읽었습니다.");
        Assert(stableFactory.GetPublishedLines("metadata_snapshot.csv")[0] == "safe_header", "검증 뒤 변한 헤더를 다시 읽었습니다.");
        Assert(driftingFormatter.ExtensionReads == 1 && driftingFormatter.HeaderReads == 1, "Formatter metadata는 생성 시 한 번만 snapshot해야 합니다.");
    }

    /// <summary>
    /// 공통 실제 Formatter 두 개를 사용해 테스트 대상 Application Service를 조립합니다.
    /// repository는 조회 Port, factory는 출력 세션 Factory입니다.
    /// 반환값은 테스트에서 바로 호출할 SettlementExportService입니다.
    /// </summary>
    private static SettlementExportService CreateService(
        ISettlementRepository repository,
        ISettlementExportSessionFactory factory)
    {
        ISettlementFormatter[] formatters =
        [
            new CsvSettlementFormatter(),
            new PipeSettlementFormatter(),
        ];

        return new SettlementExportService(repository, factory, formatters);
    }

    /// <summary>
    /// 같은 테스트 영업일의 Ready 정산 항목을 간결하게 만듭니다.
    /// id는 정산 키, merchantName은 상점 이름, amount는 지급액입니다.
    /// 반환값은 Domain 검증을 통과한 SettlementEntry입니다.
    /// </summary>
    private static SettlementEntry Ready(string id, string merchantName, decimal amount)
    {
        return CreateEntry(id, merchantName, amount, BusinessDate, SettlementStatus.Ready);
    }

    /// <summary>
    /// 원시 테스트 값을 Domain 팩터리로 검증하고 실패하면 테스트 준비 오류로 바꿉니다.
    /// id는 정산 키, merchantName은 상점명, amount는 지급액, date는 영업일, status는 상태입니다.
    /// 반환값은 유효한 SettlementEntry입니다.
    /// </summary>
    private static SettlementEntry CreateEntry(
        string id,
        string merchantName,
        decimal amount,
        DateOnly date,
        SettlementStatus status)
    {
        Result<SettlementEntry> created = SettlementEntry.Create(id, merchantName, amount, date, status);
        if (!created.IsSuccess)
        {
            throw new InvalidOperationException($"테스트 정산 생성 실패: {created.Problem.Code}");
        }

        return created.Value;
    }

    /// <summary>
    /// 두 commit 시도를 같은 gate까지 도착시킨 뒤 예외를 값으로 수집하고 세션을 반드시 정리합니다.
    /// session은 독립 staged 세션, releaseTask는 공동 시작 신호, signalArrival은 도착 수를 기록하는 callback입니다.
    /// 반환값은 성공이면 null, commit 실패면 원래 Exception을 담은 Task입니다.
    /// </summary>
    private static async Task<Exception?> CommitAtSharedGateAsync(
        ISettlementExportSession session,
        Task releaseTask,
        Action signalArrival)
    {
        signalArrival();
        await releaseTask;

        try
        {
            await session.CommitAsync(CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            // 경쟁 테스트는 두 Task를 모두 관찰해야 하므로 예외를 숨기지 않고 결과 값으로 모읍니다.
            return exception;
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>
    /// 조건이 거짓이면 테스트를 실패시키는 간단한 assertion을 수행합니다.
    /// condition은 반드시 참이어야 할 조건, message는 실패 이유입니다.
    /// 반환값은 없으며 조건이 거짓이면 InvalidOperationException을 던집니다.
    /// </summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// 동기 코드가 기대한 예외 형식을 던지는지 확인합니다.
    /// action은 실행할 코드이며 TException은 기대하는 예외 형식입니다.
    /// 반환값은 잡은 예외이고 아무 예외가 없거나 형식이 다르면 테스트를 실패시킵니다.
    /// </summary>
    private static TException AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"{typeof(TException).Name} 예외가 필요합니다.");
    }

    /// <summary>
    /// 비동기 코드가 기대한 예외 형식을 던지는지 확인합니다.
    /// action은 실행할 비동기 코드이며 TException은 기대하는 예외 형식입니다.
    /// 반환값은 잡은 예외를 담은 Task이고 아무 예외가 없거나 형식이 다르면 테스트를 실패시킵니다.
    /// </summary>
    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"{typeof(TException).Name} 예외가 필요합니다.");
    }

    // Repository Decorator는 실제 조회 동작을 유지하면서 호출 횟수라는 관찰 기능만 덧붙입니다.
    private sealed class CountingRepository : ISettlementRepository
    {
        private readonly ISettlementRepository _inner;

        public int Calls { get; private set; }

        /// <summary>
        /// 감쌀 Repository를 보관합니다.
        /// inner는 실제 조회를 수행할 Repository입니다.
        /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public CountingRepository(ISettlementRepository inner)
        {
            _inner = inner;
        }

        /// <summary>
        /// 조회 횟수를 올린 뒤 내부 Repository에 같은 요청을 전달합니다.
        /// businessDate는 대상 날짜, cancellationToken은 중단 신호입니다.
        /// 반환값은 내부 Repository의 조회 Task입니다.
        /// </summary>
        public Task<IReadOnlyList<SettlementEntry>> ListReadyAsync(
            DateOnly businessDate,
            CancellationToken cancellationToken)
        {
            Calls++;
            return _inner.ListReadyAsync(businessDate, cancellationToken);
        }
    }

    // Canceling Repository는 조회가 끝난 직후 토큰이 바뀌는 경합을 실제 지연 없이 만듭니다.
    private sealed class CancelingRepository : ISettlementRepository
    {
        private readonly CancellationTokenSource _cancellation;

        public int Calls { get; private set; }

        /// <summary>
        /// 조회 반환 경계에서 취소할 source를 보관합니다.
        /// cancellation은 서비스에도 같은 Token이 전달될 CancellationTokenSource입니다.
        /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public CancelingRepository(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        /// <summary>
        /// 호출 횟수를 기록하고 토큰을 취소한 직후 빈 목록을 반환합니다.
        /// businessDate는 사용하지 않는 조회 날짜, cancellationToken은 서비스가 전달한 동일 중단 신호입니다.
        /// 반환값은 빈 정산 목록을 담은 완료 Task입니다.
        /// </summary>
        public Task<IReadOnlyList<SettlementEntry>> ListReadyAsync(
            DateOnly businessDate,
            CancellationToken cancellationToken)
        {
            Calls++;
            _cancellation.Cancel();
            IReadOnlyList<SettlementEntry> empty = Array.Empty<SettlementEntry>();
            return Task.FromResult(empty);
        }
    }

    // Stub Repository는 고의로 잘못된 Adapter 결과도 주입하여 Application의 방어 검사를 시험합니다.
    private sealed class StubRepository : ISettlementRepository
    {
        private readonly IReadOnlyList<SettlementEntry>? _rows;

        /// <summary>
        /// 호출 때 그대로 반환할 고정 정산 목록을 보관합니다.
        /// rows는 Repository 계약 검사용 결과입니다.
        /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public StubRepository(IReadOnlyList<SettlementEntry>? rows)
        {
            _rows = rows;
        }

        /// <summary>
        /// 테스트가 지정한 목록을 필터하지 않고 반환합니다.
        /// businessDate는 실제로 사용하지 않는 요청 날짜, cancellationToken은 중단 신호입니다.
        /// 반환값은 고정 목록을 담은 Task입니다.
        /// </summary>
        public Task<IReadOnlyList<SettlementEntry>> ListReadyAsync(
            DateOnly businessDate,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // `!`는 일부러 null 계약 위반도 만드는 테스트 대역에서만 nullable 경고를 억제합니다.
            return Task.FromResult(_rows!);
        }
    }

    // 이 목록은 열거할 때마다 결과를 바꿔 검증과 사용 사이 재열거 TOCTOU를 결정적으로 드러냅니다.
    private sealed class ChangingReadOnlyList : IReadOnlyList<SettlementEntry>
    {
        private readonly SettlementEntry[] _firstEnumeration;
        private readonly SettlementEntry[] _laterEnumerations;

        public int EnumerationCount { get; private set; }

        public int Count => _firstEnumeration.Length;

        public SettlementEntry this[int index] => _firstEnumeration[index];

        /// <summary>
        /// 첫 열거 결과와 이후 열거 결과를 서로 다른 배열 스냅샷으로 보관합니다.
        /// firstEnumeration은 첫 결과, laterEnumerations는 두 번째 이후 결과입니다.
        /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public ChangingReadOnlyList(
            IEnumerable<SettlementEntry> firstEnumeration,
            IEnumerable<SettlementEntry> laterEnumerations)
        {
            _firstEnumeration = firstEnumeration.ToArray();
            _laterEnumerations = laterEnumerations.ToArray();
        }

        /// <summary>
        /// 호출 횟수를 기록하고 첫 호출과 이후 호출에 서로 다른 generic enumerator를 반환합니다.
        /// 매개변수는 없습니다.
        /// 반환값은 현재 호출 순서에 맞는 SettlementEntry 열거자입니다.
        /// </summary>
        public IEnumerator<SettlementEntry> GetEnumerator()
        {
            EnumerationCount++;
            SettlementEntry[] selected = EnumerationCount == 1
                ? _firstEnumeration
                : _laterEnumerations;

            return ((IEnumerable<SettlementEntry>)selected).GetEnumerator();
        }

        /// <summary>
        /// 비 generic IEnumerable 호출도 같은 열거 횟수와 결과 규칙을 사용하게 generic 메서드로 전달합니다.
        /// 매개변수는 없습니다.
        /// 반환값은 현재 호출 순서에 맞는 비 generic 열거자입니다.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    // Throwing Formatter는 헤더를 쓴 뒤 행 변환에서 정확한 예외가 발생하도록 만드는 테스트 Strategy입니다.
    private sealed class ThrowingFormatter : ISettlementFormatter
    {
        private readonly Exception _exception;

        public ExportFormat Format { get; }

        public string FileExtension { get; }

        public string Header => "header";

        /// <summary>
        /// 실패시킬 형식·확장자·예외를 보관합니다.
        /// format은 선택 키, extension은 안전한 확장자, exception은 FormatRow에서 그대로 던질 객체입니다.
        /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public ThrowingFormatter(ExportFormat format, string extension, Exception exception)
        {
            Format = format;
            FileExtension = extension;
            _exception = exception;
        }

        /// <summary>
        /// Formatter 버그 경로를 재현하기 위해 준비한 예외를 그대로 던집니다.
        /// entry는 사용하지 않지만 실제 계약과 같은 정산 항목입니다.
        /// 정상 반환값은 없으며 항상 생성자에서 받은 예외를 던집니다.
        /// </summary>
        public string FormatRow(SettlementEntry entry)
        {
            throw _exception;
        }
    }

    // Metadata Formatter는 생성자 구성 검사를 원하는 값으로 통과시키거나 실패시키는 단순 테스트 Strategy입니다.
    private sealed class MetadataFormatter : ISettlementFormatter
    {
        public ExportFormat Format { get; }

        public string FileExtension { get; }

        public string Header { get; }

        /// <summary>
        /// 테스트할 format, extension, header metadata를 그대로 보관합니다.
        /// format은 선택 키, extension은 확장자 후보, header는 첫 줄 후보입니다.
        /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public MetadataFormatter(ExportFormat format, string extension, string header)
        {
            Format = format;
            FileExtension = extension;
            Header = header;
        }

        /// <summary>
        /// 구성 검사 뒤 실행까지 필요한 유효한 한 줄을 만듭니다.
        /// entry는 검증된 정산 항목입니다.
        /// 반환값은 정산 ID 하나를 담은 줄입니다.
        /// </summary>
        public string FormatRow(SettlementEntry entry)
        {
            return entry.Id;
        }
    }

    // Drifting Formatter는 getter를 다시 읽으면 안전한 metadata가 위험한 값으로 바뀌는 TOCTOU 테스트 대역입니다.
    private sealed class DriftingMetadataFormatter : ISettlementFormatter
    {
        public int ExtensionReads { get; private set; }

        public int HeaderReads { get; private set; }

        public ExportFormat Format => ExportFormat.Csv;

        public string FileExtension
        {
            get
            {
                ExtensionReads++;
                return ExtensionReads == 1 ? "csv" : "../escaped";
            }
        }

        public string Header
        {
            get
            {
                HeaderReads++;
                return HeaderReads == 1 ? "safe_header" : "bad\nheader";
            }
        }

        /// <summary>
        /// metadata snapshot 검증 뒤 실제 행 변환에는 유효한 한 줄을 반환합니다.
        /// entry는 검증된 정산 항목입니다.
        /// 반환값은 정산 ID 하나를 담은 줄입니다.
        /// </summary>
        public string FormatRow(SettlementEntry entry)
        {
            return entry.Id;
        }
    }

    // Probe Factory는 실제 시간 지연 없이 쓰기·commit 경계에 오류나 취소를 정확히 주입합니다.
    private sealed class ProbeSessionFactory : ISettlementExportSessionFactory
    {
        private readonly Func<int, CancellationToken, ValueTask>? _beforeWrite;
        private readonly Func<CancellationToken, ValueTask>? _beforeCommit;
        private readonly Action? _afterCommit;
        private readonly Exception? _disposeException;

        public int OpenedCount { get; private set; }

        public int CommittedCount { get; private set; }

        public int AbortedCount { get; private set; }

        public int DisposedCount { get; private set; }

        public int PublishedCount { get; private set; }

        /// <summary>
        /// 쓰기 전·commit 전·commit 후 callback과 선택적 정리 예외를 보관합니다.
        /// beforeWrite는 쓰기 경계, beforeCommit은 공개 전, afterCommit은 공개 직후, disposeException은 정리 실패입니다.
        /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public ProbeSessionFactory(
            Func<int, CancellationToken, ValueTask>? beforeWrite = null,
            Func<CancellationToken, ValueTask>? beforeCommit = null,
            Action? afterCommit = null,
            Exception? disposeException = null)
        {
            _beforeWrite = beforeWrite;
            _beforeCommit = beforeCommit;
            _afterCommit = afterCommit;
            _disposeException = disposeException;
        }

        /// <summary>
        /// callback을 공유하는 새 Probe 세션을 만들고 열기 횟수를 기록합니다.
        /// destinationName은 최종 이름, cancellationToken은 열기 전 중단 신호입니다.
        /// 반환값은 테스트용 ISettlementExportSession을 담은 Task입니다.
        /// </summary>
        public Task<ISettlementExportSession> OpenAsync(
            string destinationName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenedCount++;

            ISettlementExportSession session = new ProbeSession(this, destinationName);
            return Task.FromResult(session);
        }

        /// <summary>
        /// 한 줄 쓰기 직전 callback을 실행해 오류나 취소를 결정적으로 주입합니다.
        /// writeNumber는 1부터 시작하는 쓰기 번호, cancellationToken은 서비스가 전달한 토큰입니다.
        /// 반환값은 callback 완료를 나타내는 ValueTask입니다.
        /// </summary>
        private ValueTask BeforeWriteAsync(int writeNumber, CancellationToken cancellationToken)
        {
            return _beforeWrite is null
                ? ValueTask.CompletedTask
                : _beforeWrite(writeNumber, cancellationToken);
        }

        /// <summary>
        /// 원자 공개 직전 callback을 실행해 commit 실패나 취소를 주입합니다.
        /// cancellationToken은 서비스가 전달한 토큰입니다.
        /// 반환값은 callback 완료를 나타내는 ValueTask입니다.
        /// </summary>
        private ValueTask BeforeCommitAsync(CancellationToken cancellationToken)
        {
            return _beforeCommit is null
                ? ValueTask.CompletedTask
                : _beforeCommit(cancellationToken);
        }

        /// <summary>
        /// Probe 세션이 공개 지점을 지난 사실을 기록하고 commit 후 callback을 실행합니다.
        /// 매개변수와 반환값은 없습니다.
        /// </summary>
        private void RecordCommit()
        {
            CommittedCount++;
            PublishedCount++;
            // `?.` null-conditional 호출은 callback이 있을 때만 Invoke하고 없으면 아무 일도 하지 않습니다.
            _afterCommit?.Invoke();
        }

        /// <summary>
        /// commit 전에 Dispose된 staged 세션 수를 기록합니다.
        /// 매개변수와 반환값은 없습니다.
        /// </summary>
        private void RecordAbort()
        {
            AbortedCount++;
        }

        /// <summary>
        /// DisposeAsync를 처음 시도한 세션 수를 기록하고 테스트가 요청한 정리 예외를 발생시킵니다.
        /// 매개변수와 반환값은 없습니다.
        /// </summary>
        private void RecordDispose()
        {
            DisposedCount++;

            if (_disposeException is not null)
            {
                throw _disposeException;
            }
        }

        // Probe 세션은 서비스의 자원 소유권만 검사하므로 Active·Committed·Disposed 세 상태면 충분합니다.
        private sealed class ProbeSession : ISettlementExportSession
        {
            private readonly ProbeSessionFactory _owner;
            private readonly List<string> _lines = [];
            private bool _committed;
            private bool _disposed;

            public string DestinationName { get; }

            /// <summary>
            /// 소유 Factory와 목적지 이름을 보관합니다.
            /// owner는 callback과 카운터를 가진 Factory, destinationName은 최종 이름입니다.
            /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
            /// </summary>
            public ProbeSession(ProbeSessionFactory owner, string destinationName)
            {
                _owner = owner;
                DestinationName = destinationName;
            }

            /// <summary>
            /// callback을 기다린 뒤 취소를 확인하고 staged 줄을 추가합니다.
            /// line은 쓸 텍스트, cancellationToken은 중단 신호입니다.
            /// 반환값은 쓰기 완료를 나타내는 ValueTask입니다.
            /// </summary>
            public async ValueTask WriteLineAsync(string line, CancellationToken cancellationToken)
            {
                EnsureActive();
                int writeNumber = _lines.Count + 1;
                await _owner.BeforeWriteAsync(writeNumber, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _lines.Add(line);
            }

            /// <summary>
            /// 공개 전 callback과 취소를 통과하면 commit을 기록하고 공개 후 callback을 실행합니다.
            /// cancellationToken은 원자 공개 전까지만 적용할 중단 신호입니다.
            /// 반환값은 commit 완료를 나타내는 ValueTask입니다.
            /// </summary>
            public async ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                EnsureActive();
                await _owner.BeforeCommitAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                _committed = true;
                _owner.RecordCommit();
            }

            /// <summary>
            /// 첫 정리에서 commit되지 않은 staging은 abort하고 이후 호출은 아무 일도 하지 않습니다.
            /// 매개변수는 없습니다.
            /// 반환값은 즉시 완료되는 ValueTask입니다.
            /// </summary>
            public ValueTask DisposeAsync()
            {
                if (_disposed)
                {
                    return ValueTask.CompletedTask;
                }

                if (!_committed)
                {
                    _lines.Clear();
                    _owner.RecordAbort();
                }

                _disposed = true;
                _owner.RecordDispose();
                return ValueTask.CompletedTask;
            }

            /// <summary>
            /// commit 또는 Dispose 뒤 세션 재사용을 막습니다.
            /// 매개변수와 반환값은 없으며 terminal 상태에서는 InvalidOperationException을 던집니다.
            /// </summary>
            private void EnsureActive()
            {
                if (_committed || _disposed)
                {
                    throw new InvalidOperationException("terminal Probe 세션은 재사용할 수 없습니다.");
                }
            }
        }
    }
}
