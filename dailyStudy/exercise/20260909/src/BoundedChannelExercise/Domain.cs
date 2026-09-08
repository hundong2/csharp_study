namespace BoundedChannelExercise;

// enum은 선택 가능한 값의 집합을 이름으로 제한합니다. 문자열보다 오타를 줄이고 switch에서 빠진 경우를 찾기 쉽습니다.
public enum ReportFormat
{
    Csv,
    Pdf,
}

// 처리 상태를 bool 하나가 아니라 이름으로 표현하면 성공과 실패의 의미가 호출부에서 분명해집니다.
public enum ProcessingStatus
{
    Succeeded,
    Failed,
}

// positional record의 괄호는 public 생성자와 get/init 속성을 함께 만듭니다. Code는 안정된 분류 키, Message는 안전한 설명입니다.
public sealed record Problem(string Code, string Message);

// Result<T>는 예상 가능한 성공과 실패를 예외 대신 값으로 표현합니다.
// T는 class로 제한해 null을 "성공값 없음"과 혼동하지 않도록 합니다.
public sealed class Result<T>
    where T : class
{
    // ?는 nullable reference annotation입니다. 값이 없을 수 있음을 컴파일러의 정적 분석에 알리며 런타임 형식은 바꾸지 않습니다.
    private readonly T? _value;
    private readonly Problem? _problem;

    /// <summary>
    /// 성공값 또는 실패 정보를 한 객체에 보관합니다.
    /// value는 성공할 때의 값, problem은 실패할 때의 설명이며, 둘 중 정확히 하나만 전달됩니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private Result(T? value, Problem? problem)
    {
        _value = value;
        _problem = problem;
    }

    // =>는 짧은 계산 속성을 한 줄로 표현합니다. `is null`은 값이 null인지 검사하는 constant pattern입니다.
    public bool IsSuccess => _problem is null;

    public T Value
    {
        get
        {
            if (!IsSuccess)
            {
                throw new InvalidOperationException("실패 Result에서는 성공값을 읽을 수 없습니다.");
            }

            // !는 null-forgiving 연산자입니다. 런타임 검사나 값 변경 없이 nullable 경고만 억제하며, 팩터리 불변식을 근거로 사용합니다.
            return _value!;
        }
    }

    public Problem Problem
    {
        get
        {
            if (IsSuccess)
            {
                throw new InvalidOperationException("성공 Result에서는 실패 정보를 읽을 수 없습니다.");
            }

            // 실패 팩터리가 항상 Problem을 넣으므로 이 지점의 값은 null이 아닙니다.
            return _problem!;
        }
    }

    /// <summary>
    /// 성공값을 가진 Result를 만듭니다.
    /// value는 호출자에게 돌려줄 유효한 객체이며 null일 수 없습니다.
    /// 반환값은 성공 상태의 Result입니다.
    /// </summary>
    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, problem: null);
    }

    /// <summary>
    /// 예상 가능한 실패를 가진 Result를 만듭니다.
    /// problem은 화면 표시나 분기 처리에 사용할 안전한 오류 정보입니다.
    /// 반환값은 실패 상태의 Result입니다.
    /// </summary>
    public static Result<T> Failure(Problem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return new Result<T>(value: null, problem);
    }
}

// 작업은 큐에 들어간 뒤 바뀌면 안 되므로 record와 get 전용 속성으로 불변성을 지킵니다.
public sealed record ReportJob
{
    public string JobId { get; }

    public string CustomerId { get; }

    public ReportFormat Format { get; }

    /// <summary>
    /// 검증을 통과한 보고서 작업을 초기화합니다.
    /// jobId는 작업 추적 키, customerId는 보고서 소유 고객 키, format은 생성할 파일 형식입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private ReportJob(string jobId, string customerId, ReportFormat format)
    {
        JobId = jobId;
        CustomerId = customerId;
        Format = format;
    }

    /// <summary>
    /// 외부 입력을 검사한 뒤 큐에 넣을 수 있는 ReportJob을 만듭니다.
    /// jobId는 비어 있지 않은 추적 키, customerId는 비어 있지 않은 고객 키, format은 지원 대상 형식입니다.
    /// 반환값은 유효한 작업을 가진 성공 Result 또는 고칠 수 있는 입력 오류를 가진 실패 Result입니다.
    /// </summary>
    public static Result<ReportJob> Create(string? jobId, string? customerId, ReportFormat format)
    {
        // string?의 ?는 null이 들어올 수 있음을 뜻합니다. 큐 경계 전에 null과 공백을 함께 거부합니다.
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return Result<ReportJob>.Failure(new Problem("job.id_required", "작업 ID는 비워 둘 수 없습니다."));
        }

        if (jobId.Trim().Length > 40)
        {
            return Result<ReportJob>.Failure(new Problem("job.id_too_long", "작업 ID는 40자 이하여야 합니다."));
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Result<ReportJob>.Failure(new Problem("job.customer_required", "고객 ID는 비워 둘 수 없습니다."));
        }

        // Enum.IsDefined는 강제 형 변환으로 들어온 정의되지 않은 숫자 값까지 막습니다.
        if (!Enum.IsDefined(format))
        {
            return Result<ReportJob>.Failure(new Problem("job.format_invalid", "지원하지 않는 보고서 형식입니다."));
        }

        return Result<ReportJob>.Success(new ReportJob(jobId.Trim(), customerId.Trim(), format));
    }
}

// 생성 결과도 처리 뒤 바뀌지 않아야 하므로 값 중심 record로 표현합니다. 인수는 작업 키, 형식, 출력 논리 경로 순서입니다.
public sealed record GeneratedReport(string JobId, ReportFormat Format, string OutputPath);

// Repository에 남길 처리 기록은 성공과 실패 중 한 상태만 갖도록 팩터리 메서드로 만듭니다.
public sealed record ProcessingRecord
{
    public string JobId { get; }

    public string WorkerName { get; }

    public ProcessingStatus Status { get; }

    public string? OutputPath { get; }

    public string? ErrorCode { get; }

    /// <summary>
    /// 처리 결과 한 건을 불변 객체로 초기화합니다.
    /// jobId는 원래 작업 키, workerName은 처리한 작업자, status는 성공 여부, outputPath와 errorCode는 상태별 세부 정보입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
    /// </summary>
    private ProcessingRecord(
        string jobId,
        string workerName,
        ProcessingStatus status,
        string? outputPath,
        string? errorCode)
    {
        JobId = jobId;
        WorkerName = workerName;
        Status = status;
        OutputPath = outputPath;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// 생성된 파일 경로를 포함한 성공 기록을 만듭니다.
    /// jobId는 작업 키, workerName은 처리 작업자, outputPath는 생성된 파일의 논리 경로입니다.
    /// 반환값은 Succeeded 상태이며 오류 코드가 없는 처리 기록입니다.
    /// </summary>
    public static ProcessingRecord Succeeded(string jobId, string workerName, string outputPath)
    {
        return new ProcessingRecord(jobId, workerName, ProcessingStatus.Succeeded, outputPath, errorCode: null);
    }

    /// <summary>
    /// 재처리나 분석에 사용할 오류 코드를 포함한 실패 기록을 만듭니다.
    /// jobId는 작업 키, workerName은 처리 작업자, errorCode는 개인정보를 담지 않은 안정적인 분류 코드입니다.
    /// 반환값은 Failed 상태이며 출력 경로가 없는 처리 기록입니다.
    /// </summary>
    public static ProcessingRecord Failed(string jobId, string workerName, string errorCode)
    {
        return new ProcessingRecord(jobId, workerName, ProcessingStatus.Failed, outputPath: null, errorCode);
    }
}
