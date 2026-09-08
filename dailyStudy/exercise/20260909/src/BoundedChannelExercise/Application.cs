namespace BoundedChannelExercise;

// Application은 Channel 구현을 직접 모르고 이 Port만 사용합니다. 테스트나 운영 Adapter를 바꿔 끼울 수 있어 DIP를 지킵니다.
public interface IReportJobQueue
{
    /// <summary>
    /// 작업 한 건을 큐에 비동기로 넣습니다.
    /// job은 검증된 작업, cancellationToken은 공간을 기다리는 생산자를 중단할 신호입니다.
    /// 반환값은 쓰기가 끝날 때 완료되는 ValueTask이며 별도 결과 값은 없습니다.
    /// </summary>
    ValueTask EnqueueAsync(ReportJob job, CancellationToken cancellationToken);

    /// <summary>
    /// 큐가 완료될 때까지 작업을 비동기 순서로 읽습니다.
    /// cancellationToken은 대기 중인 소비자를 즉시 중단할 신호입니다.
    /// 반환값은 도착하는 ReportJob을 하나씩 제공하는 비동기 스트림입니다.
    /// </summary>
    IAsyncEnumerable<ReportJob> ReadAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 더 이상 작업을 쓰지 않겠다고 큐에 알립니다.
    /// error는 생산 자체가 실패했을 때 소비자에게 전달할 선택적 예외이며 정상 완료라면 null입니다.
    /// 반환값은 이번 호출이 처음 완료시켰으면 true, 이미 완료된 큐라면 false입니다.
    /// </summary>
    bool TryComplete(Exception? error = null);
}

// 형식별 구현을 교체하는 Strategy 계약입니다. 같은 인스턴스가 여러 Worker에서 동시에 호출되므로 구현은 thread-safe해야 합니다.
public interface IReportRenderer
{
    ReportFormat Format { get; }

    /// <summary>
    /// 보고서 작업을 해당 형식의 출력 정보로 변환합니다.
    /// job은 처리할 작업, cancellationToken은 렌더링 중단 요청입니다.
    /// 반환값은 생성된 보고서의 성공 Result 또는 예상 가능한 렌더링 실패 Result입니다.
    /// </summary>
    Task<Result<GeneratedReport>> RenderAsync(ReportJob job, CancellationToken cancellationToken);
}

// 처리 기록 저장소 Port입니다. 여러 Worker가 동시에 SaveAsync를 호출하므로 구현은 동시 호출에 안전해야 합니다.
public interface IProcessingRecordRepository
{
    /// <summary>
    /// 작업 처리 결과 한 건을 저장합니다.
    /// record는 성공 또는 실패 기록, cancellationToken은 저장 중단 요청입니다.
    /// 반환값은 저장이 끝날 때 완료되는 ValueTask이며 별도 결과 값은 없습니다.
    /// </summary>
    ValueTask SaveAsync(ProcessingRecord record, CancellationToken cancellationToken);

    /// <summary>
    /// 지금까지 저장된 처리 기록의 스냅샷을 읽습니다.
    /// cancellationToken은 조회 중단 요청입니다.
    /// 반환값은 호출 시점의 기록을 JobId 순서로 담은 읽기 전용 목록입니다.
    /// </summary>
    Task<IReadOnlyList<ProcessingRecord>> GetAllAsync(CancellationToken cancellationToken);
}

// 입력 검증과 큐 등록을 조정하는 Application Service입니다.
public sealed class ReportSubmissionService
{
    private readonly IReportJobQueue _queue;

    /// <summary>
    /// 검증된 작업을 보낼 큐를 주입받아 서비스를 초기화합니다.
    /// queue는 작업을 실제로 보관하는 Port이며 null일 수 없습니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    public ReportSubmissionService(IReportJobQueue queue)
    {
        // ??는 왼쪽이 null이면 오른쪽을 선택하는 null-coalescing 연산자입니다. 필수 의존성 누락을 생성 시점에 막습니다.
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    /// <summary>
    /// 문자열 입력을 검증하고 성공한 작업만 큐에 등록합니다.
    /// jobId는 작업 키, customerId는 고객 키, format은 목표 형식, cancellationToken은 큐 공간 대기 중단 신호입니다.
    /// 반환값은 등록된 ReportJob 또는 호출자가 수정할 수 있는 입력 오류입니다.
    /// </summary>
    // async/await는 스레드를 붙잡지 않고 큐에 빈자리가 생길 때까지 기다리기 위해 사용합니다.
    public async Task<Result<ReportJob>> SubmitAsync(
        string? jobId,
        string? customerId,
        ReportFormat format,
        CancellationToken cancellationToken)
    {
        Result<ReportJob> created = ReportJob.Create(jobId, customerId, format);
        if (!created.IsSuccess)
        {
            return created;
        }

        // bounded queue가 가득 찼다면 이 await에서 생산자가 대기하여 소비자를 압도하지 않게 합니다.
        await _queue.EnqueueAsync(created.Value, cancellationToken);
        return created;
    }
}

// 여러 소비자를 만들고 각 작업을 올바른 Strategy에 보내는 Worker Pool Application Service입니다.
public sealed class ReportWorkerPool
{
    private readonly IReportJobQueue _queue;
    private readonly IReadOnlyDictionary<ReportFormat, IReportRenderer> _renderers;
    private readonly IProcessingRecordRepository _repository;

    /// <summary>
    /// Worker가 사용할 큐, 형식별 Renderer, 결과 Repository를 연결합니다.
    /// queue는 작업 소스, renderers는 형식별 Strategy 모음, repository는 처리 결과 저장 Port입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다. 빈 목록이나 중복 형식은 배포 구성 오류로 예외 처리합니다.
    /// </summary>
    public ReportWorkerPool(
        IReportJobQueue queue,
        IEnumerable<IReportRenderer> renderers,
        IProcessingRecordRepository repository)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        ArgumentNullException.ThrowIfNull(renderers);
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

        // LINQ ToDictionary는 각 Format을 키로 만듭니다. renderer => renderer.Format은 입력에서 키를 고르는 lambda 식입니다.
        _renderers = renderers.ToDictionary(renderer => renderer.Format);
        if (_renderers.Count == 0)
        {
            throw new ArgumentException("Renderer를 하나 이상 등록해야 합니다.", nameof(renderers));
        }
    }

    /// <summary>
    /// 지정한 수의 Worker를 시작하고 모든 Worker가 큐를 끝까지 비울 때까지 기다립니다.
    /// workerCount는 동시에 소비할 작업자 수, cancellationToken은 즉시 중단이 필요할 때의 신호입니다.
    /// 반환값은 모든 Worker가 종료될 때 완료되는 Task이며, 한 Worker의 예상 밖 실패는 형제를 중단하고 원 예외를 전달합니다.
    /// </summary>
    public async Task RunAsync(int workerCount, CancellationToken cancellationToken)
    {
        if (workerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workerCount), "Worker 수는 1 이상이어야 합니다.");
        }

        // using 선언은 RunAsync 종료 때 Dispose를 자동 호출합니다. linked source는 호출자 취소와 항목 실패를 합칩니다.
        using CancellationTokenSource workerCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Select는 1부터 workerCount까지 감독되는 소비 Task를 만들고 ToArray로 즉시 시작 목록을 고정합니다.
        Task[] workers = Enumerable
            .Range(1, workerCount)
            .Select(index => RunWorkerAsync(
                $"worker-{index}",
                workerCancellation,
                cancellationToken))
            .ToArray();

        // WhenAll은 일부 Worker만 끝났다고 전체가 끝난 것으로 오해하지 않게 모든 종료를 기다립니다.
        await Task.WhenAll(workers);
    }

    /// <summary>
    /// Worker 한 명이 큐를 읽고, 항목 처리의 예상 밖 실패만 감독하여 형제 Worker를 중단합니다.
    /// workerName은 작업자 이름, workerCancellation은 형제 공유 중단 소스, callerCancellationToken은 호출자 취소 구분 기준입니다.
    /// 반환값은 큐 종료 뒤 완료되는 Task이며, Renderer/Repository 또는 생산 완료 오류는 원래 예외로 전달합니다.
    /// </summary>
    private async Task RunWorkerAsync(
        string workerName,
        CancellationTokenSource workerCancellation,
        CancellationToken callerCancellationToken)
    {
        // await foreach는 항목이 도착할 때마다 비동기 스트림에서 하나씩 꺼냅니다. 큐 완료 오류는 이 바깥에서 전파합니다.
        await foreach (ReportJob job in _queue.ReadAllAsync(workerCancellation.Token))
        {
            try
            {
                ProcessingRecord record;

                // out은 dictionary가 찾은 값을 renderer 변수로 돌려주는 문법이며, 실패 시 null일 수 있어 ?로 표시합니다.
                if (!_renderers.TryGetValue(job.Format, out IReportRenderer? renderer))
                {
                    record = ProcessingRecord.Failed(job.JobId, workerName, "worker.strategy_missing");
                }
                else
                {
                    Result<GeneratedReport> rendered = await renderer.RenderAsync(job, workerCancellation.Token);

                    // 삼항 연산자 ?:는 조건에 따라 두 값 중 하나를 고릅니다. 두 분기가 같은 ProcessingRecord 형식일 때 간결합니다.
                    record = rendered.IsSuccess
                        ? ProcessingRecord.Succeeded(job.JobId, workerName, rendered.Value.OutputPath)
                        : ProcessingRecord.Failed(job.JobId, workerName, rendered.Problem.Code);
                }

                // 예상 가능한 한 작업의 실패도 기록으로 저장한 뒤 다음 작업을 계속 처리합니다.
                await _repository.SaveAsync(record, workerCancellation.Token);
            }
            // when은 exception filter입니다. 호출자가 요청한 취소는 생산 오류로 바꾸지 않고 그대로 전달합니다.
            catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested)
            {
                // 인수 없는 throw;는 방금 잡은 예외와 원래 stack trace를 그대로 보존해 다시 던집니다.
                throw;
            }
            catch (Exception exception)
            {
                // Renderer/Repository의 치명적 실패만 큐를 오류 완료하고 형제 Worker를 취소합니다.
                _queue.TryComplete(exception);
                workerCancellation.Cancel();
                // 새 예외로 감싸지 않아 최초 Renderer/Repository 장애 위치를 호출자가 볼 수 있게 합니다.
                throw;
            }
        }
    }
}
