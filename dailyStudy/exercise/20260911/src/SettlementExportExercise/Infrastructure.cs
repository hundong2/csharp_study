using System.Collections.Concurrent;
using System.Globalization;

// file-scoped namespace는 이 파일의 Adapter 구현을 같은 이름 공간에 넣습니다.
namespace SettlementExportExercise;

// CSV Formatter는 외부 형식 규칙을 캡슐화한 Strategy/Adapter입니다.
public sealed class CsvSettlementFormatter : ISettlementFormatter
{
    public ExportFormat Format => ExportFormat.Csv;

    public string FileExtension => "csv";

    public string Header => "settlement_id,merchant_name,amount,business_date";

    /// <summary>
    /// 정산 항목을 문화권에 흔들리지 않는 CSV 한 줄로 바꿉니다.
    /// entry는 검증된 정산 항목입니다.
    /// 반환값은 쉼표와 큰따옴표를 RFC 4180 방식으로 escaping한 CSV 줄입니다.
    /// </summary>
    public string FormatRow(SettlementEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // InvariantCulture는 PC 언어 설정이 달라도 소수점이 항상 `.`이고 날짜 형식도 같게 유지합니다.
        string amount = entry.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        string date = entry.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return $"{Escape(entry.Id)},{Escape(entry.MerchantName)},{amount},{date}";
    }

    /// <summary>
    /// CSV 필드에 쉼표·큰따옴표·줄바꿈이 있을 때 전체를 인용하고 내부 큰따옴표를 두 번 씁니다.
    /// value는 파일에 넣을 원래 필드입니다.
    /// 반환값은 CSV 파서가 원래 값으로 복원할 수 있는 안전한 필드입니다.
    /// </summary>
    private static string Escape(string value)
    {
        bool needsQuotes =
            value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\r', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal);

        if (!needsQuotes)
        {
            return value;
        }

        string doubledQuotes = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{doubledQuotes}\"";
    }
}

// 파이프 구분 Formatter는 Application 흐름을 바꾸지 않고 출력 규칙만 교체되는 Strategy 예시입니다.
public sealed class PipeSettlementFormatter : ISettlementFormatter
{
    public ExportFormat Format => ExportFormat.PipeDelimited;

    public string FileExtension => "txt";

    public string Header => "settlement_id|merchant_name|amount|business_date";

    /// <summary>
    /// 정산 항목을 파이프 구분 한 줄로 바꿉니다.
    /// entry는 검증된 정산 항목입니다.
    /// 반환값은 역슬래시와 파이프를 escaping하고 숫자·날짜 형식을 고정한 문자열입니다.
    /// </summary>
    public string FormatRow(SettlementEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string amount = entry.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        string date = entry.BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return $"{Escape(entry.Id)}|{Escape(entry.MerchantName)}|{amount}|{date}";
    }

    /// <summary>
    /// 파이프 파일에서 의미가 있는 역슬래시와 구분자를 데이터로 보존하도록 escaping합니다.
    /// value는 파일에 넣을 원래 필드입니다.
    /// 반환값은 역슬래시를 먼저 두 배로 만들고 파이프 앞에 역슬래시를 붙인 문자열입니다.
    /// </summary>
    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);
    }
}

// In-memory Repository Adapter는 외부 DB 없이도 Repository 계약과 LINQ 필터를 실행 가능하게 합니다.
public sealed class InMemorySettlementRepository : ISettlementRepository
{
    private readonly SettlementEntry[] _entries;

    /// <summary>
    /// 시작 시 제공된 정산 항목을 별도 배열 스냅샷으로 보관합니다.
    /// entries는 데모나 테스트에서 조회할 초기 정산 모음입니다.
    /// 생성자는 객체를 초기화하므로 반환값은 없으며 null 항목·중복 ID는 구성 오류로 거부합니다.
    /// </summary>
    public InMemorySettlementRepository(IEnumerable<SettlementEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries.ToArray();
        if (_entries.Any(entry => entry is null))
        {
            throw new ArgumentException("정산 목록에는 null을 넣을 수 없습니다.", nameof(entries));
        }

        bool hasDuplicateId = _entries
            .GroupBy(entry => entry.Id, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);

        if (hasDuplicateId)
        {
            throw new ArgumentException("정산 ID는 중복될 수 없습니다.", nameof(entries));
        }
    }

    /// <summary>
    /// 같은 영업일이면서 Ready 상태인 정산만 새 배열로 조회합니다.
    /// businessDate는 대상 날짜, cancellationToken은 조회 전 중단 신호입니다.
    /// 반환값은 필터 결과의 읽기 전용 스냅샷을 담은 Task입니다.
    /// </summary>
    public Task<IReadOnlyList<SettlementEntry>> ListReadyAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Where는 조건에 맞는 값만 선택하고 ToArray는 지연 실행 결과를 현재 시점 스냅샷으로 고정합니다.
        SettlementEntry[] ready = _entries
            .Where(entry => entry.BusinessDate == businessDate && entry.Status == SettlementStatus.Ready)
            .ToArray();

        return Task.FromResult<IReadOnlyList<SettlementEntry>>(ready);
    }
}

// 이 Adapter는 실제 파일 대신 staged 줄과 공개 파일을 메모리에 보관하여 commit-or-abort를 눈으로 검증하게 합니다.
public sealed class InMemorySettlementExportSessionFactory : ISettlementExportSessionFactory
{
    // target-typed `new(...)`는 왼쪽 ConcurrentDictionary 형식에서 생성할 형식을 추론하며 comparer는 대소문자를 구분합니다.
    private readonly ConcurrentDictionary<string, string[]> _publishedFiles =
        new(StringComparer.Ordinal);

    private int _openedCount;
    private int _committedCount;
    private int _abortedCount;
    private int _disposedCount;

    // Volatile.Read는 여러 Task가 갱신한 최신 카운터 값을 잠금 없이 안전하게 관찰합니다.
    public int OpenedCount => Volatile.Read(ref _openedCount);

    public int CommittedCount => Volatile.Read(ref _committedCount);

    public int AbortedCount => Volatile.Read(ref _abortedCount);

    public int DisposedCount => Volatile.Read(ref _disposedCount);

    public int PublishedCount => _publishedFiles.Count;

    /// <summary>
    /// 아직 외부에 보이지 않는 새 staged 세션을 엽니다.
    /// destinationName은 최종 공개 이름, cancellationToken은 열기 전 중단 신호입니다.
    /// 반환값은 호출자가 await using으로 소유해야 하는 세션을 담은 Task입니다.
    /// </summary>
    public Task<ISettlementExportSession> OpenAsync(
        string destinationName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationName);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _openedCount);

        ISettlementExportSession session = new InMemorySettlementExportSession(
            destinationName,
            Publish,
            RecordAbort,
            RecordDispose);

        return Task.FromResult(session);
    }

    /// <summary>
    /// 공개된 파일의 줄을 외부에서 바꾸지 못하도록 새 배열로 읽습니다.
    /// destinationName은 commit된 최종 이름입니다.
    /// 반환값은 공개 파일의 줄 스냅샷이며 이름이 없으면 KeyNotFoundException을 던집니다.
    /// </summary>
    public IReadOnlyList<string> GetPublishedLines(string destinationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationName);

        if (!_publishedFiles.TryGetValue(destinationName, out string[]? lines))
        {
            throw new KeyNotFoundException($"공개된 파일을 찾을 수 없습니다: {destinationName}");
        }

        return lines.ToArray();
    }

    /// <summary>
    /// staged 줄 전체를 동일한 이름에 한 번만 공개하고 commit 횟수를 기록합니다.
    /// destinationName은 최종 이름, lines는 공개할 불변 스냅샷입니다.
    /// 반환값은 없으며 기존 파일이 있으면 덮어쓰지 않고 IOException을 던집니다.
    /// </summary>
    private void Publish(string destinationName, IReadOnlyList<string> lines)
    {
        string[] snapshot = lines.ToArray();
        if (!_publishedFiles.TryAdd(destinationName, snapshot))
        {
            throw new IOException($"같은 이름의 공개 파일이 이미 있습니다: {destinationName}");
        }

        Interlocked.Increment(ref _committedCount);
    }

    /// <summary>
    /// commit되지 않은 세션이 Dispose되어 staged 내용이 폐기됐음을 기록합니다.
    /// 매개변수와 반환값은 없습니다.
    /// </summary>
    private void RecordAbort()
    {
        Interlocked.Increment(ref _abortedCount);
    }

    /// <summary>
    /// 성공·실패와 관계없이 세션의 DisposeAsync가 한 번 완료됐음을 기록합니다.
    /// 매개변수와 반환값은 없습니다.
    /// </summary>
    private void RecordDispose()
    {
        Interlocked.Increment(ref _disposedCount);
    }

    // 세션 상태를 제한하면 commit 뒤 쓰기나 두 번 commit 같은 잘못된 수명 주기를 즉시 찾을 수 있습니다.
    private enum SessionState
    {
        Active,
        Committed,
        Disposed,
    }

    // IAsyncDisposable은 네트워크 flush처럼 기다려야 할 수 있는 정리 작업을 비동기로 표현하는 표준 계약입니다.
    private sealed class InMemorySettlementExportSession : ISettlementExportSession
    {
        // `[]` collection expression은 아직 줄이 없는 새 List를 target type에 맞게 만듭니다.
        private readonly List<string> _stagedLines = [];
        private readonly Action<string, IReadOnlyList<string>> _publish;
        private readonly Action _recordAbort;
        private readonly Action _recordDispose;
        private SessionState _state = SessionState.Active;

        public string DestinationName { get; }

        /// <summary>
        /// 메모리 staged 세션과 수명 주기 callback을 초기화합니다.
        /// destinationName은 최종 이름, publish는 원자 공개 함수, recordAbort와 recordDispose는 관찰용 함수입니다.
        /// 생성자는 객체를 초기화하므로 반환값은 없습니다.
        /// </summary>
        public InMemorySettlementExportSession(
            string destinationName,
            Action<string, IReadOnlyList<string>> publish,
            Action recordAbort,
            Action recordDispose)
        {
            DestinationName = destinationName;
            _publish = publish;
            _recordAbort = recordAbort;
            _recordDispose = recordDispose;
        }

        /// <summary>
        /// 완성된 한 줄을 아직 공개되지 않은 메모리 staging 목록에 추가합니다.
        /// line은 줄바꿈 없는 텍스트, cancellationToken은 추가 전 중단 신호입니다.
        /// 반환값은 즉시 완료되는 ValueTask이며 실제 I/O Adapter라면 비동기 쓰기를 나타냅니다.
        /// </summary>
        public ValueTask WriteLineAsync(string line, CancellationToken cancellationToken)
        {
            EnsureActive();
            ArgumentNullException.ThrowIfNull(line);

            if (line.Contains('\r', StringComparison.Ordinal) || line.Contains('\n', StringComparison.Ordinal))
            {
                throw new ArgumentException("한 번의 쓰기에는 줄바꿈 없는 한 줄만 전달해야 합니다.", nameof(line));
            }

            cancellationToken.ThrowIfCancellationRequested();
            _stagedLines.Add(line);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// staged 줄을 새 최종 파일 이름으로 한 번만 공개합니다.
        /// cancellationToken은 공개 연산 직전 중단 신호입니다.
        /// 반환값은 즉시 완료되는 ValueTask이며 공개 실패 시 세션은 Active로 남아 Dispose에서 abort됩니다.
        /// </summary>
        public ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            EnsureActive();
            cancellationToken.ThrowIfCancellationRequested();

            if (_stagedLines.Count == 0)
            {
                throw new InvalidOperationException("빈 staged 파일은 commit할 수 없습니다.");
            }

            // Publish가 성공한 뒤에만 Committed로 바꿔, 예외가 나면 DisposeAsync가 실패한 staging을 폐기하게 합니다.
            _publish(DestinationName, _stagedLines);
            _state = SessionState.Committed;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 세션을 정확히 한 번 정리하고 commit 전이면 staged 줄을 폐기합니다.
        /// 매개변수는 없습니다. IAsyncDisposable 계약상 반환값은 정리 완료를 나타내는 ValueTask입니다.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            // Dispose는 여러 번 불려도 첫 호출만 효과가 있는 idempotent 정리 연산이어야 안전합니다.
            if (_state == SessionState.Disposed)
            {
                return ValueTask.CompletedTask;
            }

            if (_state == SessionState.Active)
            {
                _stagedLines.Clear();
                _recordAbort();
            }

            _state = SessionState.Disposed;
            _recordDispose();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 쓰기나 commit이 아직 Active 상태에서만 수행되는지 확인합니다.
        /// 매개변수와 반환값은 없으며 terminal 상태라면 InvalidOperationException을 던집니다.
        /// </summary>
        private void EnsureActive()
        {
            if (_state != SessionState.Active)
            {
                throw new InvalidOperationException($"{_state} 세션은 더 이상 쓰거나 commit할 수 없습니다.");
            }
        }
    }
}
