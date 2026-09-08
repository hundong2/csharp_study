using System.Collections.Concurrent;
using System.Threading.Channels;

namespace BoundedChannelExercise;

// Channel<T>는 생산자와 소비자 사이에서 데이터를 안전하게 전달하는 .NET 동시성 자료구조입니다.
public sealed class BoundedChannelReportJobQueue : IReportJobQueue
{
    private readonly Channel<ReportJob> _channel;

    /// <summary>
    /// Channel 내부 대기 버퍼에 상한과 backpressure가 있는 작업 큐를 만듭니다.
    /// capacity는 소비자가 아직 가져가지 않은 작업을 버퍼에 최대 몇 개 보관할지 정하는 양수입니다.
    /// 생성자는 큐를 초기화하므로 반환값은 없습니다.
    /// </summary>
    public BoundedChannelReportJobQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "큐 용량은 1 이상이어야 합니다.");
        }

        _channel = Channel.CreateBounded<ReportJob>(
            new BoundedChannelOptions(capacity)
            {
                // Wait는 가득 찬 항목을 버리지 않고 생산자의 WriteAsync를 기다리게 하는 backpressure 정책입니다.
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
                // false이면 쓰기 호출 스택에서 소비자 후속 작업을 바로 실행하지 않아 예기치 않은 재진입을 피합니다.
                AllowSynchronousContinuations = false,
            });
    }

    /// <summary>
    /// 작업 한 건을 bounded Channel에 쓰며 공간이 없으면 비동기로 기다립니다.
    /// job은 검증된 작업, cancellationToken은 빈자리 대기를 취소할 신호입니다.
    /// 반환값은 쓰기가 완료될 때 끝나는 ValueTask이며 별도 결과 값은 없습니다.
    /// </summary>
    public ValueTask EnqueueAsync(ReportJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        // ValueTask는 즉시 끝날 수도 있는 짧은 비동기 작업에서 Task 객체 할당을 줄일 수 있습니다. 한 번만 await해야 안전합니다.
        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    /// <summary>
    /// Channel의 모든 작업을 완료 시점까지 비동기 스트림으로 노출합니다.
    /// cancellationToken은 새 항목을 기다리는 소비자를 중단할 신호입니다.
    /// 반환값은 여러 Worker가 경쟁하여 나눠 읽을 수 있는 IAsyncEnumerable입니다.
    /// </summary>
    public IAsyncEnumerable<ReportJob> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Writer를 닫아 새 작업을 거부하고 Reader가 남은 작업 뒤 종료되게 합니다.
    /// error는 생산 파이프라인 실패를 전달할 예외이며 정상 완료에서는 null입니다.
    /// 반환값은 처음 닫기에 성공했으면 true, 이미 닫혔다면 false입니다.
    /// </summary>
    public bool TryComplete(Exception? error = null)
    {
        return _channel.Writer.TryComplete(error);
    }
}

// CSV 생성 세부사항을 맡는 Strategy입니다. 실제 파일 I/O 대신 재현 가능한 논리 경로를 반환합니다.
public sealed class CsvReportRenderer : IReportRenderer
{
    public ReportFormat Format => ReportFormat.Csv;

    /// <summary>
    /// CSV 작업을 결정적인 출력 경로로 변환합니다.
    /// job은 처리할 작업, cancellationToken은 렌더링 중단 요청입니다.
    /// 반환값은 생성된 CSV의 논리 경로를 가진 성공 Result입니다.
    /// </summary>
    public async Task<Result<GeneratedReport>> RenderAsync(ReportJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();

        // Task.Yield는 실제 지연 시간을 추측하지 않고 비동기 양보 지점을 만들어 여러 Worker가 협력하는 예제를 보여 줍니다.
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        return Result<GeneratedReport>.Success(
            new GeneratedReport(job.JobId, Format, $"reports/{job.JobId}.csv"));
    }
}

// PDF 생성 Strategy는 한 고객에 대한 예상 가능한 데모 실패를 Result로 돌려 실패 격리를 보여 줍니다.
public sealed class PdfReportRenderer : IReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    /// <summary>
    /// PDF 작업을 처리하고 정책상 차단된 고객이면 안전한 실패를 반환합니다.
    /// job은 처리할 작업, cancellationToken은 렌더링 중단 요청입니다.
    /// 반환값은 PDF 경로를 가진 성공 Result 또는 template_blocked 실패 Result입니다.
    /// </summary>
    public async Task<Result<GeneratedReport>> RenderAsync(ReportJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();

        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(job.CustomerId, "CUSTOMER-BLOCKED", StringComparison.Ordinal))
        {
            return Result<GeneratedReport>.Failure(
                new Problem("renderer.template_blocked", "이 고객의 PDF 템플릿은 현재 사용할 수 없습니다."));
        }

        return Result<GeneratedReport>.Success(
            new GeneratedReport(job.JobId, Format, $"reports/{job.JobId}.pdf"));
    }
}

// ConcurrentQueue는 여러 Worker가 동시에 저장해도 내부 컬렉션이 손상되지 않는 process-local Repository Adapter입니다.
public sealed class InMemoryProcessingRecordRepository : IProcessingRecordRepository
{
    // new()는 왼쪽 변수 형식에서 생성할 형식을 추론하는 target-typed new 문법입니다.
    private readonly ConcurrentQueue<ProcessingRecord> _records = new();

    /// <summary>
    /// 처리 기록을 thread-safe 메모리 컬렉션에 추가합니다.
    /// record는 저장할 결과, cancellationToken은 저장 직전 중단을 확인할 신호입니다.
    /// 반환값은 이미 완료된 ValueTask이며 별도 결과 값은 없습니다.
    /// </summary>
    public ValueTask SaveAsync(ProcessingRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        _records.Enqueue(record);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 현재 처리 기록을 정렬된 스냅샷으로 반환합니다.
    /// cancellationToken은 조회 시작 전 중단을 확인할 신호입니다.
    /// 반환값은 JobId와 WorkerName 순서의 읽기 전용 목록입니다.
    /// </summary>
    public Task<IReadOnlyList<ProcessingRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // OrderBy/ThenBy는 동시 처리 때문에 달라질 수 있는 저장 순서를 화면과 테스트에서 결정적인 순서로 바꿉니다.
        IReadOnlyList<ProcessingRecord> snapshot = _records
            .OrderBy(record => record.JobId, StringComparer.Ordinal)
            .ThenBy(record => record.WorkerName, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult(snapshot);
    }
}
