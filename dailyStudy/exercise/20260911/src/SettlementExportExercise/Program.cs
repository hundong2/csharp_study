using System.Globalization;

// file-scoped namespace는 이 파일의 모든 형식을 같은 이름 공간에 넣으면서 중첩 중괄호를 줄입니다.
namespace SettlementExportExercise;

internal static class Program
{
    /// <summary>
    /// 예제 의존성을 조립하고 정산 내보내기 데모 또는 자체 테스트를 실행합니다.
    /// args는 --self-test 같은 명령행 옵션이며, 반환값 0은 성공이고 1은 데모 검증 실패입니다.
    /// </summary>
    // async Main은 외부 저장소와 출력 세션의 비동기 작업을 스레드를 막지 않고 기다리는 실제 진입점 형태입니다.
    private static async Task<int> Main(string[] args)
    {
        // Contains의 comparer는 `--SELF-TEST`처럼 대소문자가 달라도 같은 옵션으로 인식하게 합니다.
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return await SelfTests.RunAsync();
        }

        // target-typed `new(...)`는 왼쪽 DateOnly 형식에서 생성할 형식을 추론해 중복 이름을 줄입니다.
        DateOnly businessDate = new(2026, 9, 11);

        // collection expression `[...]`은 정해진 초기 항목을 간결하게 배열로 만듭니다.
        // 입력 순서를 일부러 PAY-002, PAY-001로 두어 서비스의 결정적 ID 정렬도 데모에서 확인합니다.
        SettlementEntry[] entries =
        [
            CreateEntry("PAY-002", "\"바다\"상점", 7_500m, businessDate, SettlementStatus.Ready),
            CreateEntry("PAY-001", "서울,상점", 12_000.50m, businessDate, SettlementStatus.Ready),
            CreateEntry("PAY-003", "보류 상점", 9_000m, businessDate, SettlementStatus.Held),
            CreateEntry("PAY-004", "다음 날 상점", 3_000m, businessDate.AddDays(1), SettlementStatus.Ready),
        ];

        InMemorySettlementRepository repository = new(entries);
        InMemorySettlementExportSessionFactory sessionFactory = new();

        // 이곳이 Composition Root입니다. 구체 Adapter와 Strategy를 만들고 Application Service에 생성자 주입합니다.
        ISettlementFormatter[] formatters =
        [
            new CsvSettlementFormatter(),
            new PipeSettlementFormatter(),
        ];

        SettlementExportService service = new(repository, sessionFactory, formatters);

        Result<SettlementExportReceipt> exported = await service.ExportAsync(
            "settlement-20260911",
            businessDate,
            ExportFormat.Csv,
            CancellationToken.None);

        if (!exported.IsSuccess)
        {
            Console.WriteLine($"[ERROR] {exported.Problem.Code}");
            return 1;
        }

        SettlementExportReceipt receipt = exported.Value;
        Console.WriteLine(FormatExportedLine(receipt));

        Result<SettlementExportReceipt> rejected = await service.ExportAsync(
            "../escape",
            businessDate,
            ExportFormat.Csv,
            CancellationToken.None);

        Console.WriteLine(rejected.IsSuccess
            ? "[ERROR] 위험한 이름이 검증을 통과했습니다."
            : $"[REJECTED] {rejected.Problem.Code}");

        IReadOnlyList<string> publishedLines = sessionFactory.GetPublishedLines(receipt.DestinationName);
        Console.WriteLine("[FILE]");

        // foreach는 공개된 파일 스냅샷을 첫 줄부터 순서대로 출력합니다.
        foreach (string line in publishedLines)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine(
            $"[LIFECYCLE] opened={sessionFactory.OpenedCount} committed={sessionFactory.CommittedCount} " +
            $"aborted={sessionFactory.AbortedCount} disposed={sessionFactory.DisposedCount} " +
            $"published={sessionFactory.PublishedCount}");

        string[] expectedLines =
        [
            "settlement_id,merchant_name,amount,business_date",
            "PAY-001,\"서울,상점\",12000.50,2026-09-11",
            "PAY-002,\"\"\"바다\"\"상점\",7500.00,2026-09-11",
        ];

        bool isExpected =
            !rejected.IsSuccess &&
            rejected.Problem.Code == "export.name_invalid" &&
            receipt.ExportedCount == 2 &&
            receipt.TotalAmount == 19_500.50m &&
            publishedLines.SequenceEqual(expectedLines, StringComparer.Ordinal) &&
            sessionFactory.OpenedCount == 1 &&
            sessionFactory.CommittedCount == 1 &&
            sessionFactory.AbortedCount == 0 &&
            sessionFactory.DisposedCount == 1 &&
            sessionFactory.PublishedCount == 1;

        // 삼항 연산자 `?:`는 최종 검증 조건에 따라 두 종료 코드 중 하나를 고릅니다.
        return isExpected ? 0 : 1;
    }

    /// <summary>
    /// 데모용 원시 값을 Domain 팩터리로 검증해 정산 항목을 만듭니다.
    /// id는 정산 키, merchantName은 상점명, amount는 지급액, date는 영업일, status는 처리 상태입니다.
    /// 반환값은 유효한 SettlementEntry이며 준비 데이터가 잘못되면 구성 예외를 던집니다.
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
            throw new InvalidOperationException($"데모 정산 생성 실패: {created.Problem.Code}");
        }

        return created.Value;
    }

    /// <summary>
    /// 성공 영수증을 PC 언어 설정과 무관한 한 줄 로그로 변환합니다.
    /// receipt는 공개된 목적지·행 수·합계를 가진 성공 영수증입니다.
    /// 반환값은 금액 소수점이 항상 점으로 표현되는 EXPORTED 로그 문자열입니다.
    /// </summary>
    internal static string FormatExportedLine(SettlementExportReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        // FormattableString.Invariant는 보간 문자열의 숫자 형식에 InvariantCulture를 적용합니다.
        return FormattableString.Invariant(
            $"[EXPORTED] {receipt.DestinationName} rows={receipt.ExportedCount} total={receipt.TotalAmount:0.00}");
    }
}
