namespace BoundedChannelExercise;

internal static class Program
{
    /// <summary>
    /// 예제 의존성을 조립하고 데모 또는 자체 테스트를 실행합니다.
    /// args는 --self-test 같은 명령행 옵션이며, 반환값 0은 성공이고 1은 검증 실패입니다.
    /// </summary>
    // async/await는 큐 공간과 Worker 종료를 기다리는 동안 실행 스레드를 붙잡지 않게 합니다.
    private static async Task<int> Main(string[] args)
    {
        // Contains는 배열에 특정 옵션이 있는지 검사하는 LINQ 메서드입니다. 대소문자 차이는 무시합니다.
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            return await SelfTests.RunAsync();
        }

        // 이곳은 Composition Root입니다. 구체 Adapter와 Strategy를 선택해 Application 객체에 생성자 주입합니다.
        // capacity:는 parameter 이름을 적는 named argument이고, new(...)는 왼쪽 형식으로 생성자 형식을 추론합니다.
        BoundedChannelReportJobQueue queue = new(capacity: 2);
        InMemoryProcessingRecordRepository repository = new();

        // [] collection expression은 여러 구현을 배열로 묶는 안정 C# 문법입니다.
        IReportRenderer[] renderers =
        [
            new CsvReportRenderer(),
            new PdfReportRenderer(),
        ];

        ReportSubmissionService submissionService = new(queue);
        ReportWorkerPool workerPool = new(queue, renderers, repository);

        // using 선언은 Main이 끝날 때 Dispose를 자동 호출합니다. CancellationTokenSource는 10초 뒤 중단하는 안전장치입니다.
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        Task workers = workerPool.RunAsync(workerCount: 2, timeout.Token);

        // 튜플은 작은 데모 입력의 필드 이름과 값을 별도 클래스 없이 한 묶음으로 보관합니다.
        (string? JobId, string? CustomerId, ReportFormat Format)[] requests =
        [
            ("JOB-001", "CUSTOMER-A", ReportFormat.Csv),
            ("JOB-002", "CUSTOMER-B", ReportFormat.Pdf),
            ("JOB-003", "CUSTOMER-BLOCKED", ReportFormat.Pdf),
            ("JOB-004", "CUSTOMER-D", ReportFormat.Csv),
            ("", "CUSTOMER-E", ReportFormat.Csv),
        ];

        try
        {
            // foreach는 입력을 처음부터 끝까지 한 건씩 순회합니다. 실패한 입력은 큐에 넣지 않고 다음 입력을 계속 봅니다.
            foreach ((string? jobId, string? customerId, ReportFormat format) in requests)
            {
                Result<ReportJob> submitted = await submissionService.SubmitAsync(
                    jobId,
                    customerId,
                    format,
                    timeout.Token);

                if (submitted.IsSuccess)
                {
                    Console.WriteLine($"[ACCEPTED] {submitted.Value.JobId} ({submitted.Value.Format})");
                }
                else
                {
                    Console.WriteLine($"[REJECTED] {submitted.Problem.Code}");
                }
            }
        }
        finally
        {
            // finally는 생산 성공/실패 모두에서 실행됩니다. 먼저 Writer를 닫고, await로 Worker 실패까지 반드시 관찰합니다.
            queue.TryComplete();
            await workers;
        }

        IReadOnlyList<ProcessingRecord> records = await repository.GetAllAsync(timeout.Token);
        foreach (ProcessingRecord record in records)
        {
            // switch expression은 상태별 문자열을 고릅니다. _ fallback은 새 enum도 unknown으로 받으므로 새 상태 테스트를 별도로 추가해야 합니다.
            string detail = record.Status switch
            {
                ProcessingStatus.Succeeded => record.OutputPath!,
                ProcessingStatus.Failed => record.ErrorCode!,
                _ => "unknown",
            };

            Console.WriteLine($"[RESULT:{record.Status.ToString().ToUpperInvariant()}] {record.JobId} -> {detail}");
        }

        int succeeded = records.Count(record => record.Status == ProcessingStatus.Succeeded);
        int failed = records.Count(record => record.Status == ProcessingStatus.Failed);
        Console.WriteLine($"[SUMMARY] accepted={records.Count}, succeeded={succeeded}, failed={failed}");

        return records.Count == 4 && succeeded == 3 && failed == 1 ? 0 : 1;
    }
}
