using System.Threading.Channels;

namespace BoundedChannelExercise;

internal static class SelfTests
{
    /// <summary>
    /// 외부 테스트 패키지 없이 핵심 동시성 규칙을 차례로 검증합니다.
    /// 매개변수는 없으며, 반환값 0은 모든 테스트 통과이고 1은 하나 이상의 실패입니다.
    /// </summary>
    public static async Task<int> RunAsync()
    {
        // Func<Task>는 "호출하면 비동기 테스트가 시작되는 함수"를 값처럼 목록에 담는 delegate 형식입니다.
        (string Name, Func<Task> Test)[] tests =
        [
            ("입력 검증은 Result를 반환한다", ValidationReturnsResultAsync),
            ("거부된 입력은 큐에 들어가지 않는다", RejectedSubmissionNeverReachesQueueAsync),
            ("가득 찬 큐는 생산자에게 backpressure를 건다", FullQueueWaitsForReaderAsync),
            ("대기 중인 생산자는 취소할 수 있다", BlockedWriterCanBeCanceledAsync),
            ("2-Worker 구성은 승인 작업을 한 번씩 처리한다", WorkersProcessAcceptedJobsOnceAsync),
            ("Worker 수는 최대 동시 처리 수를 제한한다", WorkerCountLimitsConcurrencyAsync),
            ("한 작업의 예상 실패는 다음 작업을 막지 않는다", ExpectedFailureIsIsolatedAsync),
            ("완료된 큐는 남은 작업을 drain한다", CompletionDrainsBufferedJobsAsync),
            ("오류 완료는 버퍼 drain 뒤 원인을 전파한다", ErrorCompletionDrainsThenPropagatesAsync),
            ("생산 오류는 진행 중인 형제 작업을 보존한다", ProducerErrorPreservesInflightSiblingAsync),
            ("완료된 큐는 새 쓰기를 거부한다", CompletedQueueRejectsNewWritesAsync),
            ("강제 취소는 대기 중인 Worker를 중단한다", CancellationStopsWaitingWorkerAsync),
            ("예상 밖 Worker 실패는 형제 Worker를 중단한다", UnexpectedFailureStopsSiblingWorkersAsync),
            ("빠진 Strategy는 실패 기록으로 남는다", MissingStrategyIsRecordedAsync),
        ];

        int passed = 0;
        foreach ((string name, Func<Task> test) in tests)
        {
            try
            {
                // WaitAsync는 회귀로 비동기 대기가 풀리지 않아도 한 테스트가 전체 실행을 영원히 멈추지 않게 합니다.
                await test().WaitAsync(TimeSpan.FromSeconds(5));
                passed++;
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                // 테스트 경계에서는 실패 원인을 보여 줘야 하므로 예외 형식과 안전한 메시지를 출력합니다.
                Console.WriteLine($"[FAIL] {name}: {exception.GetType().Name} - {exception.Message}");
            }
        }

        Console.WriteLine($"self-test {passed}/{tests.Length} 통과");
        return passed == tests.Length ? 0 : 1;
    }

    /// <summary>
    /// 필수 값 누락과 정의되지 않은 enum이 예외 대신 실패 Result가 되는지 확인합니다.
    /// 매개변수는 없으며, 반환값은 동기 검증이 끝났음을 나타내는 완료된 Task입니다.
    /// </summary>
    private static Task ValidationReturnsResultAsync()
    {
        Result<ReportJob> missingId = ReportJob.Create(" ", "CUSTOMER-A", ReportFormat.Csv);
        Assert(!missingId.IsSuccess, "빈 작업 ID는 실패해야 합니다.");
        Assert(missingId.Problem.Code == "job.id_required", "빈 작업 ID 오류 코드가 다릅니다.");

        // 명시적 cast는 숫자를 enum으로 바꿉니다. 외부 역직렬화처럼 잘못된 숫자가 들어오는 경계를 재현합니다.
        Result<ReportJob> invalidFormat = ReportJob.Create("JOB-001", "CUSTOMER-A", (ReportFormat)999);
        Assert(!invalidFormat.IsSuccess, "정의되지 않은 형식은 실패해야 합니다.");
        Assert(invalidFormat.Problem.Code == "job.format_invalid", "형식 오류 코드가 다릅니다.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Submission Service가 유효하지 않은 입력을 큐에 쓰지 않는지 검증합니다.
    /// 매개변수는 없으며, 반환값은 빈 큐의 Worker 종료와 검증이 끝날 때 완료되는 Task입니다.
    /// </summary>
    private static async Task RejectedSubmissionNeverReachesQueueAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 1);
        InMemoryProcessingRecordRepository repository = new();
        ReportSubmissionService service = new(queue);
        ReportWorkerPool pool = new(queue, [new CsvReportRenderer()], repository);

        Result<ReportJob> result = await service.SubmitAsync(null, "CUSTOMER-A", ReportFormat.Csv, CancellationToken.None);
        Assert(!result.IsSuccess, "null 작업 ID는 거부되어야 합니다.");

        Assert(queue.TryComplete(), "첫 완료 호출은 성공해야 합니다.");
        await pool.RunAsync(workerCount: 1, CancellationToken.None);

        IReadOnlyList<ProcessingRecord> records = await repository.GetAllAsync(CancellationToken.None);
        Assert(records.Count == 0, "거부된 입력은 처리 기록을 만들면 안 됩니다.");
    }

    /// <summary>
    /// capacity 1 큐에서 두 번째 쓰기가 Reader가 공간을 만들기 전까지 완료되지 않는지 검증합니다.
    /// 매개변수는 없으며, 반환값은 두 작업의 순서와 정상 완료를 모두 확인할 때 끝나는 Task입니다.
    /// </summary>
    private static async Task FullQueueWaitsForReaderAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 1);
        ReportJob first = CreateJob("JOB-001", ReportFormat.Csv);
        ReportJob second = CreateJob("JOB-002", ReportFormat.Csv);

        await queue.EnqueueAsync(first, CancellationToken.None);

        // AsTask는 ValueTask를 여러 상태 검사에 쓰기 위한 Task로 바꿉니다. 변환한 뒤 원래 ValueTask를 다시 await하지 않습니다.
        Task pendingWrite = queue.EnqueueAsync(second, CancellationToken.None).AsTask();
        Assert(!pendingWrite.IsCompleted, "큐가 가득 차면 두 번째 쓰기는 대기해야 합니다.");

        // await using은 비동기 열거자를 테스트가 끝날 때 DisposeAsync로 정리합니다.
        await using IAsyncEnumerator<ReportJob> reader = queue
            .ReadAllAsync(CancellationToken.None)
            .GetAsyncEnumerator();

        Assert(await reader.MoveNextAsync(), "첫 작업을 읽어야 합니다.");
        Assert(reader.Current.JobId == "JOB-001", "첫 번째 작업 순서가 다릅니다.");

        await pendingWrite;
        Assert(await reader.MoveNextAsync(), "대기하던 두 번째 작업을 읽어야 합니다.");
        Assert(reader.Current.JobId == "JOB-002", "두 번째 작업 순서가 다릅니다.");

        Assert(queue.TryComplete(), "큐 완료가 성공해야 합니다.");
        Assert(!await reader.MoveNextAsync(), "완료된 빈 큐는 열거를 끝내야 합니다.");
    }

    /// <summary>
    /// 가득 찬 큐에서 기다리는 생산자에게 CancellationToken이 전달되는지 검증합니다.
    /// 매개변수는 없으며, 반환값은 취소된 작업이 큐에 남지 않았음을 확인할 때 끝나는 Task입니다.
    /// </summary>
    private static async Task BlockedWriterCanBeCanceledAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 1);
        await queue.EnqueueAsync(CreateJob("JOB-001", ReportFormat.Csv), CancellationToken.None);

        using CancellationTokenSource cancellation = new();
        Task blockedWrite = queue
            .EnqueueAsync(CreateJob("JOB-002", ReportFormat.Csv), cancellation.Token)
            .AsTask();

        Assert(!blockedWrite.IsCompleted, "두 번째 생산자는 공간을 기다려야 합니다.");
        cancellation.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(
            () => blockedWrite,
            "취소된 생산자는 OperationCanceledException을 유지해야 합니다.");

        queue.TryComplete();
        List<string> remainingIds = [];

        // await foreach는 완료 전까지 남은 항목을 비동기로 읽으며, 취소된 두 번째 쓰기는 나타나지 않아야 합니다.
        await foreach (ReportJob job in queue.ReadAllAsync(CancellationToken.None))
        {
            remainingIds.Add(job.JobId);
        }

        Assert(remainingIds.SequenceEqual(["JOB-001"]), "취소된 쓰기는 큐에 들어가면 안 됩니다.");
    }

    /// <summary>
    /// 두 Worker가 여러 작업을 경쟁 소비하되 각 승인된 작업을 한 번만 기록하는지 확인합니다.
    /// 매개변수는 없으며, 반환값은 여섯 작업의 처리와 중복 검사가 끝날 때 완료되는 Task입니다.
    /// </summary>
    private static async Task WorkersProcessAcceptedJobsOnceAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 2);
        InMemoryProcessingRecordRepository repository = new();
        IReportRenderer[] renderers = [new CsvReportRenderer(), new PdfReportRenderer()];
        ReportWorkerPool pool = new(queue, renderers, repository);
        Task workers = pool.RunAsync(workerCount: 2, CancellationToken.None);

        for (int index = 1; index <= 6; index++)
        {
            // %는 나머지 연산자입니다. 짝수 작업은 PDF, 홀수 작업은 CSV로 번갈아 만듭니다.
            ReportFormat format = index % 2 == 0 ? ReportFormat.Pdf : ReportFormat.Csv;
            await queue.EnqueueAsync(CreateJob($"JOB-{index:000}", format), CancellationToken.None);
        }

        Assert(queue.TryComplete(), "생산 완료를 처음 알리는 호출은 성공해야 합니다.");
        await workers;

        IReadOnlyList<ProcessingRecord> records = await repository.GetAllAsync(CancellationToken.None);
        Assert(records.Count == 6, "승인된 작업마다 처리 기록이 하나 있어야 합니다.");

        int distinctIds = records.Select(record => record.JobId).Distinct(StringComparer.Ordinal).Count();
        Assert(distinctIds == 6, "같은 작업을 두 Worker가 중복 기록하면 안 됩니다.");
    }

    /// <summary>
    /// Worker 2개가 정확히 두 작업까지만 동시에 Renderer에 들어가고 세 번째는 기다리는지 검증합니다.
    /// 매개변수는 없으며, 반환값은 gate 전후의 동시성 상한과 세 작업 저장을 확인할 때 완료되는 Task입니다.
    /// </summary>
    private static async Task WorkerCountLimitsConcurrencyAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 3);
        InMemoryProcessingRecordRepository repository = new();
        GatedRenderer renderer = new(expectedConcurrentStarts: 2);
        ReportWorkerPool pool = new(queue, [renderer], repository);

        await queue.EnqueueAsync(CreateJob("JOB-001", ReportFormat.Csv), CancellationToken.None);
        await queue.EnqueueAsync(CreateJob("JOB-002", ReportFormat.Csv), CancellationToken.None);
        await queue.EnqueueAsync(CreateJob("JOB-003", ReportFormat.Csv), CancellationToken.None);
        queue.TryComplete();

        Task workers = pool.RunAsync(workerCount: 2, CancellationToken.None);
        await renderer.AllExpectedWorkersEntered;

        Assert(renderer.StartedCount == 2, "gate가 닫힌 동안 세 번째 작업은 시작하면 안 됩니다.");
        Assert(renderer.MaxActiveCount == 2, "Worker 2개는 최대 동시 처리를 2로 제한해야 합니다.");

        renderer.Release();
        await workers;

        IReadOnlyList<ProcessingRecord> records = await repository.GetAllAsync(CancellationToken.None);
        Assert(records.Count == 3, "gate를 연 뒤 세 작업이 모두 처리되어야 합니다.");
        Assert(renderer.MaxActiveCount == 2, "전체 실행에서도 동시 처리 상한을 넘으면 안 됩니다.");
    }

    /// <summary>
    /// 중간 작업이 실패 Result를 반환해도 단일 Worker가 다음 작업을 계속 처리하는지 확인합니다.
    /// 매개변수는 없으며, 반환값은 실패 한 건과 그 뒤 성공 한 건을 검증할 때 완료되는 Task입니다.
    /// </summary>
    private static async Task ExpectedFailureIsIsolatedAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 3);
        InMemoryProcessingRecordRepository repository = new();
        ReportWorkerPool pool = new(
            queue,
            [new CsvReportRenderer(), new PdfReportRenderer()],
            repository);

        await queue.EnqueueAsync(CreateJob("JOB-001", ReportFormat.Csv), CancellationToken.None);
        await queue.EnqueueAsync(CreateJob("JOB-002", ReportFormat.Pdf, "CUSTOMER-BLOCKED"), CancellationToken.None);
        await queue.EnqueueAsync(CreateJob("JOB-003", ReportFormat.Csv), CancellationToken.None);
        queue.TryComplete();

        await pool.RunAsync(workerCount: 1, CancellationToken.None);
        IReadOnlyList<ProcessingRecord> records = await repository.GetAllAsync(CancellationToken.None);

        Assert(records.Count == 3, "실패 뒤의 작업도 처리되어야 합니다.");
        Assert(records.Single(record => record.JobId == "JOB-002").Status == ProcessingStatus.Failed, "차단 PDF는 실패해야 합니다.");
        Assert(records.Single(record => record.JobId == "JOB-003").Status == ProcessingStatus.Succeeded, "실패 뒤 CSV는 성공해야 합니다.");
    }

    /// <summary>
    /// Writer 완료 뒤에도 버퍼에 있던 작업을 모두 처리한 다음 Worker가 종료되는지 확인합니다.
    /// 매개변수는 없으며, 반환값은 drain된 두 기록과 멱등적인 완료 호출을 검증할 때 끝나는 Task입니다.
    /// </summary>
    private static async Task CompletionDrainsBufferedJobsAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 2);
        InMemoryProcessingRecordRepository repository = new();
        ReportWorkerPool pool = new(queue, [new CsvReportRenderer()], repository);

        await queue.EnqueueAsync(CreateJob("JOB-001", ReportFormat.Csv), CancellationToken.None);
        await queue.EnqueueAsync(CreateJob("JOB-002", ReportFormat.Csv), CancellationToken.None);

        Assert(queue.TryComplete(), "첫 완료는 true여야 합니다.");
        Assert(!queue.TryComplete(), "두 번째 완료는 상태를 바꾸지 않고 false여야 합니다.");

        await pool.RunAsync(workerCount: 1, CancellationToken.None);
        IReadOnlyList<ProcessingRecord> records = await repository.GetAllAsync(CancellationToken.None);
        Assert(records.Count == 2, "완료 전에 버퍼에 있던 작업을 모두 drain해야 합니다.");
    }

    /// <summary>
    /// 생산 오류로 완료된 큐도 버퍼 항목을 먼저 제공한 뒤 같은 오류를 Reader에 전파하는지 확인합니다.
    /// 매개변수는 없으며, 반환값은 한 항목 drain과 InvalidOperationException 전파를 확인할 때 완료되는 Task입니다.
    /// </summary>
    private static async Task ErrorCompletionDrainsThenPropagatesAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 1);
        await queue.EnqueueAsync(CreateJob("JOB-001", ReportFormat.Csv), CancellationToken.None);

        InvalidOperationException producerFailure = new("producer-boom");
        Assert(queue.TryComplete(producerFailure), "오류를 가진 첫 완료는 성공해야 합니다.");

        await using IAsyncEnumerator<ReportJob> reader = queue
            .ReadAllAsync(CancellationToken.None)
            .GetAsyncEnumerator();

        Assert(await reader.MoveNextAsync(), "오류 완료 전 버퍼 항목은 먼저 drain해야 합니다.");
        Assert(reader.Current.JobId == "JOB-001", "drain된 작업 ID가 다릅니다.");

        InvalidOperationException observedFailure = await AssertThrowsAsync<InvalidOperationException>(
            () => reader.MoveNextAsync().AsTask(),
            "버퍼를 비운 뒤 생산 오류가 Reader에 전파되어야 합니다.");

        Assert(ReferenceEquals(producerFailure, observedFailure), "Reader는 같은 생산 오류 인스턴스를 전달해야 합니다.");
    }

    /// <summary>
    /// 한 Worker가 생산 완료 오류에 먼저 도달해도 다른 Worker의 진행 중 작업을 취소하지 않는지 검증합니다.
    /// 매개변수는 없으며, 반환값은 두 accepted 작업 저장과 동일 생산 오류 전파를 확인할 때 완료되는 Task입니다.
    /// </summary>
    private static async Task ProducerErrorPreservesInflightSiblingAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 2);
        SlowAndFastRenderer renderer = new();
        FastSaveSignalingRepository repository = new();
        ReportWorkerPool pool = new(queue, [renderer], repository);

        await queue.EnqueueAsync(CreateJob("JOB-SLOW", ReportFormat.Csv), CancellationToken.None);
        await queue.EnqueueAsync(CreateJob("JOB-FAST", ReportFormat.Csv), CancellationToken.None);

        InvalidOperationException producerFailure = new("producer-boom");
        queue.TryComplete(producerFailure);

        Task workers = pool.RunAsync(workerCount: 2, CancellationToken.None);
        await repository.FastRecordSaved;

        // FAST 저장 뒤 그 Worker는 Channel 완료 오류를 동기적으로 관찰합니다. 이 오류가 SLOW의 token을 취소하면 안 됩니다.
        Assert(!renderer.SlowWasCanceled, "생산 완료 오류가 진행 중인 형제 작업을 취소하면 안 됩니다.");
        renderer.ReleaseSlowJob();

        InvalidOperationException observedFailure = await AssertThrowsAsync<InvalidOperationException>(
            () => workers,
            "모든 진행 중 작업 뒤 생산 오류를 호출자에게 전달해야 합니다.");

        IReadOnlyList<ProcessingRecord> records = await repository.GetAllAsync(CancellationToken.None);
        Assert(records.Count == 2, "생산 오류 전에 accepted된 두 작업을 모두 저장해야 합니다.");
        Assert(ReferenceEquals(producerFailure, observedFailure), "Worker Pool은 같은 생산 오류 인스턴스를 전달해야 합니다.");
    }

    /// <summary>
    /// 정상 완료된 큐에 새 작업을 쓰면 lifecycle 오류가 즉시 드러나는지 확인합니다.
    /// 매개변수는 없으며, 반환값은 ChannelClosedException을 확인할 때 완료되는 Task입니다.
    /// </summary>
    private static async Task CompletedQueueRejectsNewWritesAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 1);
        Assert(queue.TryComplete(), "첫 완료는 성공해야 합니다.");

        Task writeAfterCompletion = queue
            .EnqueueAsync(CreateJob("JOB-001", ReportFormat.Csv), CancellationToken.None)
            .AsTask();

        await AssertThrowsAsync<ChannelClosedException>(
            () => writeAfterCompletion,
            "완료 뒤 쓰기는 ChannelClosedException으로 거부되어야 합니다.");
    }

    /// <summary>
    /// 열린 빈 큐에서 대기하는 Worker가 강제 취소 신호에 즉시 반응하는지 검증합니다.
    /// 매개변수는 없으며, 반환값은 취소 예외가 전파되었음을 확인할 때 완료되는 Task입니다.
    /// </summary>
    private static async Task CancellationStopsWaitingWorkerAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 1);
        InMemoryProcessingRecordRepository repository = new();
        ReportWorkerPool pool = new(queue, [new CsvReportRenderer()], repository);

        using CancellationTokenSource cancellation = new();
        Task worker = pool.RunAsync(workerCount: 1, cancellation.Token);
        cancellation.Cancel();

        await AssertThrowsAsync<OperationCanceledException>(
            () => worker,
            "강제 취소는 대기 중인 Worker까지 전파되어야 합니다.");

        queue.TryComplete();
    }

    /// <summary>
    /// Renderer의 예상 밖 예외가 열린 큐에서 기다리는 형제 Worker까지 중단하고 즉시 호출자에게 전파되는지 확인합니다.
    /// 매개변수는 없으며, 반환값은 원래 InvalidOperationException과 오류 완료를 확인할 때 완료되는 Task입니다.
    /// </summary>
    private static async Task UnexpectedFailureStopsSiblingWorkersAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 1);
        InMemoryProcessingRecordRepository repository = new();
        ReportWorkerPool pool = new(queue, [new ThrowingRenderer()], repository);

        await queue.EnqueueAsync(CreateJob("JOB-001", ReportFormat.Csv), CancellationToken.None);

        InvalidOperationException observedFailure = await AssertThrowsAsync<InvalidOperationException>(
            () => pool.RunAsync(workerCount: 2, CancellationToken.None),
            "예상 밖 Renderer 예외는 열린 큐에서도 즉시 호출자에게 전파되어야 합니다.");

        Assert(observedFailure.Message == "renderer-boom", "최초 Renderer 오류 메시지를 보존해야 합니다.");
        Assert(!queue.TryComplete(), "Worker supervisor가 이미 큐를 오류 완료했어야 합니다.");
    }

    /// <summary>
    /// 유효한 형식이지만 해당 Strategy가 구성되지 않았을 때 작업별 실패 기록을 남기는지 확인합니다.
    /// 매개변수는 없으며, 반환값은 worker.strategy_missing 오류 기록 검증이 끝날 때 완료되는 Task입니다.
    /// </summary>
    private static async Task MissingStrategyIsRecordedAsync()
    {
        BoundedChannelReportJobQueue queue = new(capacity: 1);
        InMemoryProcessingRecordRepository repository = new();
        ReportWorkerPool pool = new(queue, [new CsvReportRenderer()], repository);

        await queue.EnqueueAsync(CreateJob("JOB-001", ReportFormat.Pdf), CancellationToken.None);
        queue.TryComplete();

        await pool.RunAsync(workerCount: 1, CancellationToken.None);
        ProcessingRecord record = (await repository.GetAllAsync(CancellationToken.None)).Single();

        Assert(record.Status == ProcessingStatus.Failed, "빠진 Strategy는 실패로 기록해야 합니다.");
        Assert(record.ErrorCode == "worker.strategy_missing", "구성 누락 오류 코드가 다릅니다.");
    }

    // 이 fake는 생산 오류와 진행 중 작업의 경쟁을 재현하기 위해 SLOW 작업만 test-controlled gate에 세웁니다.
    private sealed class SlowAndFastRenderer : IReportRenderer
    {
        private readonly TaskCompletionSource<bool> _slowStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _releaseSlow =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _slowWasCanceled;

        public ReportFormat Format => ReportFormat.Csv;

        // ref는 필드의 값을 복사하지 않고 저장 위치 자체를 전달합니다.
        // Volatile.Read를 쓰면 다른 Worker가 기록한 최신 값을 현재 스레드가 관찰하도록 메모리 가시성을 보장합니다.
        public bool SlowWasCanceled => Volatile.Read(ref _slowWasCanceled) == 1;

        /// <summary>
        /// SLOW 작업은 gate에서 기다리고 FAST 작업은 SLOW가 시작된 뒤 즉시 성공시킵니다.
        /// job은 순서를 제어할 작업, cancellationToken은 SLOW gate 대기 중단 신호입니다.
        /// 반환값은 작업별 CSV 논리 경로를 가진 성공 Result이며 SLOW 취소는 그대로 전파됩니다.
        /// </summary>
        public async Task<Result<GeneratedReport>> RenderAsync(ReportJob job, CancellationToken cancellationToken)
        {
            if (job.JobId == "JOB-SLOW")
            {
                _slowStarted.TrySetResult(true);

                try
                {
                    await _releaseSlow.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Interlocked.Exchange는 ref로 전달한 저장 위치를 원자적으로 바꾸므로,
                    // 여러 Worker가 동시에 취소를 관찰해도 중간 상태나 갱신 유실 없이 플래그를 남깁니다.
                    Interlocked.Exchange(ref _slowWasCanceled, 1);
                    throw;
                }
            }
            else
            {
                // FAST가 먼저 끝나지 않게 하여 두 Worker가 각각 SLOW와 FAST를 처리하는 상태를 결정적으로 만듭니다.
                await _slowStarted.Task.WaitAsync(cancellationToken);
            }

            return Result<GeneratedReport>.Success(
                new GeneratedReport(job.JobId, Format, $"reports/{job.JobId}.csv"));
        }

        /// <summary>
        /// SLOW 작업의 gate를 열어 렌더링과 저장을 계속하게 합니다.
        /// 매개변수와 반환값은 없으며 여러 번 호출해도 첫 호출만 상태를 바꿉니다.
        /// </summary>
        public void ReleaseSlowJob()
        {
            _releaseSlow.TrySetResult(true);
        }
    }

    // FAST 기록 저장을 test-controlled 신호로 노출하면서 실제 thread-safe Repository에 위임하는 테스트 Adapter입니다.
    private sealed class FastSaveSignalingRepository : IProcessingRecordRepository
    {
        private readonly InMemoryProcessingRecordRepository _inner = new();
        private readonly TaskCompletionSource<bool> _fastRecordSaved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FastRecordSaved => _fastRecordSaved.Task;

        /// <summary>
        /// 처리 기록을 내부 Repository에 저장하고 FAST 기록이면 테스트 대기자를 깨웁니다.
        /// record는 저장할 결과, cancellationToken은 저장 중단 요청입니다.
        /// 반환값은 내부 저장과 신호 설정이 끝날 때 완료되는 ValueTask입니다.
        /// </summary>
        public async ValueTask SaveAsync(ProcessingRecord record, CancellationToken cancellationToken)
        {
            await _inner.SaveAsync(record, cancellationToken);

            if (record.JobId == "JOB-FAST")
            {
                _fastRecordSaved.TrySetResult(true);
            }
        }

        /// <summary>
        /// 내부 Repository가 가진 처리 기록 스냅샷을 읽습니다.
        /// cancellationToken은 조회 중단 요청입니다.
        /// 반환값은 JobId 순서의 읽기 전용 처리 기록 목록입니다.
        /// </summary>
        public Task<IReadOnlyList<ProcessingRecord>> GetAllAsync(CancellationToken cancellationToken)
        {
            return _inner.GetAllAsync(cancellationToken);
        }
    }

    // 이 fake는 실제 시간을 기다리지 않고 Worker 동시 진입 수를 관찰하는 테스트용 Strategy입니다.
    private sealed class GatedRenderer : IReportRenderer
    {
        private readonly int _expectedConcurrentStarts;

        // RunContinuationsAsynchronously는 gate를 여는 스레드 안에서 대기 작업의 후속 코드를 재진입 실행하지 않게 합니다.
        private readonly TaskCompletionSource<bool> _allExpectedWorkersEntered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _startedCount;
        private int _activeCount;
        private int _maxActiveCount;

        public ReportFormat Format => ReportFormat.Csv;

        // ref는 필드 저장 위치를 참조로 넘기고 Volatile.Read는 다른 스레드가 쓴 최신 값을 관찰하게 합니다.
        public int StartedCount => Volatile.Read(ref _startedCount);

        public int MaxActiveCount => Volatile.Read(ref _maxActiveCount);

        public Task AllExpectedWorkersEntered => _allExpectedWorkersEntered.Task;

        /// <summary>
        /// gate가 열리기 전에 도달해야 하는 Worker 수를 지정해 fake를 초기화합니다.
        /// expectedConcurrentStarts는 동시에 들어올 것으로 기대하는 양수 Worker 수입니다.
        /// 생성자는 상태를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public GatedRenderer(int expectedConcurrentStarts)
        {
            if (expectedConcurrentStarts <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedConcurrentStarts));
            }

            _expectedConcurrentStarts = expectedConcurrentStarts;
        }

        /// <summary>
        /// Renderer에 들어온 Worker 수를 기록하고 테스트 gate가 열릴 때까지 기다립니다.
        /// job은 처리할 작업, cancellationToken은 gate 대기 중단 신호입니다.
        /// 반환값은 gate 뒤 생성된 CSV 보고서를 가진 성공 Result입니다.
        /// </summary>
        public async Task<Result<GeneratedReport>> RenderAsync(ReportJob job, CancellationToken cancellationToken)
        {
            // Interlocked.Increment는 여러 Worker가 동시에 들어와도 증가 연산을 잃지 않도록 원자적으로 실행합니다.
            int active = Interlocked.Increment(ref _activeCount);
            UpdateMaximum(active);

            int started = Interlocked.Increment(ref _startedCount);
            if (started == _expectedConcurrentStarts)
            {
                _allExpectedWorkersEntered.TrySetResult(true);
            }

            try
            {
                await _release.Task.WaitAsync(cancellationToken);
                return Result<GeneratedReport>.Success(
                    new GeneratedReport(job.JobId, Format, $"reports/{job.JobId}.csv"));
            }
            finally
            {
                Interlocked.Decrement(ref _activeCount);
            }
        }

        /// <summary>
        /// gate에서 기다리는 모든 Renderer 호출을 진행시킵니다.
        /// 매개변수는 없고 반환값도 없으며, 여러 번 호출해도 첫 호출만 상태를 바꿉니다.
        /// </summary>
        public void Release()
        {
            _release.TrySetResult(true);
        }

        /// <summary>
        /// 여러 Worker가 경쟁해도 관측된 최대 동시 수가 더 작은 값으로 덮이지 않게 갱신합니다.
        /// candidate는 방금 관측한 active Worker 수이며 반환값은 없습니다.
        /// </summary>
        private void UpdateMaximum(int candidate)
        {
            while (true)
            {
                int observed = Volatile.Read(ref _maxActiveCount);
                if (candidate <= observed)
                {
                    return;
                }

                // CompareExchange는 값이 observed일 때만 candidate로 바꾸는 원자 연산입니다. 경쟁에서 지면 다시 읽습니다.
                if (Interlocked.CompareExchange(ref _maxActiveCount, candidate, observed) == observed)
                {
                    return;
                }
            }
        }
    }

    // 이 fake는 Worker supervisor가 예상 밖 Strategy 예외를 처리하는 경로를 재현합니다.
    private sealed class ThrowingRenderer : IReportRenderer
    {
        public ReportFormat Format => ReportFormat.Csv;

        /// <summary>
        /// 호출될 때 예상 밖 InvalidOperationException을 발생시켜 Worker 장애를 재현합니다.
        /// job은 실패시킬 작업, cancellationToken은 실제 구현과 같은 계약을 유지하기 위한 중단 신호입니다.
        /// 정상 반환값은 없으며 항상 예외를 던집니다.
        /// </summary>
        public Task<Result<GeneratedReport>> RenderAsync(ReportJob job, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(job);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("renderer-boom");
        }
    }

    /// <summary>
    /// 테스트에 필요한 유효한 작업을 만들고 생성 실패라면 즉시 테스트를 실패시킵니다.
    /// jobId는 작업 키, format은 목표 형식, customerId는 선택적 고객 키입니다.
    /// 반환값은 검증을 통과한 ReportJob입니다.
    /// </summary>
    private static ReportJob CreateJob(
        string jobId,
        ReportFormat format,
        string customerId = "CUSTOMER-A")
    {
        Result<ReportJob> created = ReportJob.Create(jobId, customerId, format);
        Assert(created.IsSuccess, $"테스트 준비 작업 생성 실패: {jobId}");
        return created.Value;
    }

    /// <summary>
    /// 비동기 동작이 기대한 예외 형식을 그대로 던지는지 확인합니다.
    /// action은 실행할 비동기 함수, message는 예외가 없을 때 사용할 실패 설명입니다.
    /// TException은 기대하는 예외 형식이며 Exception의 파생 형식이어야 합니다.
    /// 반환값은 실제로 잡은 TException이며 기대 예외가 없으면 검증 예외를 던집니다.
    /// </summary>
    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action, string message)
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

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// 조건이 거짓이면 자체 테스트 실패를 명확한 예외로 바꿉니다.
    /// condition은 반드시 참이어야 할 조건, message는 실패 원인 설명입니다.
    /// 반환값은 없으며 조건이 거짓일 때 InvalidOperationException을 던집니다.
    /// </summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
