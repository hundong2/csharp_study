using System.ComponentModel.DataAnnotations;
using InventoryWatcherExercise.Application;
using InventoryWatcherExercise.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InventoryWatcherExercise.Hosting;

/// <summary>
/// appsettings.json의 InventoryWatcher 구역과 연결되는 강한 형식의 설정입니다.
/// 문자열 키를 코드 곳곳에서 직접 읽지 않고, 아래 Composition Root의 엄격한 Binder가 오타도 즉시 거부합니다.
/// </summary>
public sealed class InventoryWatcherOptions
{
    /// <summary>설정 파일에서 이 옵션을 찾을 구역 이름입니다.</summary>
    public const string SectionName = "InventoryWatcher";

    /// <summary>첫 회차 전부터 일정하게 발생하는 타이머 Tick의 고정 주기(밀리초)입니다.</summary>
    // [Range]는 속성값의 허용 범위를 선언하는 특성(attribute)이고, ErrorMessage는 이름을 붙여 전달한 선택 인수다.
    // 60_000의 밑줄은 사람이 자릿수를 읽기 쉽게 할 뿐 실제 값은 60000과 같다.
    [Range(25, 60_000, ErrorMessage = "IntervalMilliseconds는 25~60000 사이여야 합니다.")]
    // init 접근자는 객체를 처음 만들 때만 값을 넣게 해 이후 설정의 우발적 변경을 막는다.
    public int IntervalMilliseconds { get; init; } = 150;

    /// <summary>데모가 자동 종료되기 전에 실행할 감시 회차 수입니다.</summary>
    [Range(1, 100, ErrorMessage = "MaxCycles는 1~100 사이여야 합니다.")]
    public int MaxCycles { get; init; } = 2;
}

/// <summary>
/// Worker가 다음 실행 시점을 기다리는 방법을 분리하는 시간 포트입니다.
/// 테스트에서는 실제 시간을 기다리지 않는 가짜 구현으로 바꿀 수 있습니다.
/// </summary>
public interface ITickSource
{
    /// <summary>
    /// 다음 주기가 오거나 타이머가 끝날 때까지 비동기로 기다립니다.
    /// </summary>
    /// <param name="cancellationToken">대기를 즉시 중단할 수 있는 호스트 종료 신호입니다.</param>
    /// <returns>다음 주기가 도착했으면 true, 타이머가 더는 동작하지 않으면 false를 반환합니다.</returns>
    // ValueTask는 결과가 이미 준비된 경우 Task 객체 할당을 줄이는 가벼운 비동기 반환 형식이다.
    ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken);
}

/// <summary>
/// .NET의 <see cref="PeriodicTimer"/>로 일정 간격의 실행 신호를 만드는 Singleton 시간 어댑터입니다.
/// PeriodicTimer는 느린 회차 동안 생긴 여러 Tick을 쌓지 않고 하나로 합치므로 다음 대기가 곧바로 끝날 수 있습니다.
/// 한 Worker가 하나의 타이머 상태를 소유하도록 Singleton으로 등록합니다.
/// </summary>
// IAsyncDisposable은 자원 해제 자체도 비동기 계약으로 표현하는 인터페이스다.
public sealed class PeriodicTimerTickSource : ITickSource, IAsyncDisposable
{
    private readonly PeriodicTimer _timer;

    /// <summary>
    /// 시작 시 검증된 설정의 간격으로 반복 타이머를 만듭니다. 생성자는 타이머를 준비하며 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="options">appsettings와 환경 변수에서 바인딩하고 검증한 감시 설정입니다.</param>
    public PeriodicTimerTickSource(IOptions<InventoryWatcherOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(options.Value.IntervalMilliseconds));
    }

    /// <summary>
    /// PeriodicTimer의 다음 신호를 기다리며 취소 신호를 그대로 전달합니다.
    /// </summary>
    /// <param name="cancellationToken">호스트 종료 시 대기를 깨우는 취소 신호입니다.</param>
    /// <returns>주기가 도착했는지를 나타내는 가벼운 ValueTask를 반환합니다.</returns>
    public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
    {
        return _timer.WaitForNextTickAsync(cancellationToken);
    }

    /// <summary>
    /// 호스트가 종료될 때 내부 타이머 자원을 해제합니다.
    /// 매개변수는 없습니다.
    /// </summary>
    /// <returns>이미 완료된 비동기 해제 작업을 반환하며 별도 업무 값은 반환하지 않습니다.</returns>
    public ValueTask DisposeAsync()
    {
        _timer.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// BackgroundService 내부의 치명적 실패를 프로세스 종료 코드로 전달하는 Singleton 상태입니다.
/// .NET 10 Host가 Worker 예외 뒤 RunAsync를 정상 완료해도 운영 스크립트가 실패를 놓치지 않게 합니다.
/// </summary>
public sealed class WorkerExitStatus
{
    private int _exitCode;

    /// <summary>현재 프로세스 종료 코드를 스레드에 안전하게 읽습니다. 0은 정상, 1은 Worker 실패입니다.</summary>
    // Volatile.Read는 다른 스레드가 쓴 최신 값을 이 스레드가 캐시된 과거 값 대신 보게 한다.
    // ref는 필드의 값 복사본이 아니라 실제 저장 위치를 메서드에 전달한다는 뜻이다.
    public int ExitCode => Volatile.Read(ref _exitCode);

    /// <summary>
    /// Worker에서 처리하지 못한 예외가 발생했음을 종료 코드 1로 기록합니다.
    /// 매개변수는 없으며, 값을 반환하지 않고 상태만 변경합니다.
    /// </summary>
    public void MarkFailure()
    {
        // Interlocked.Exchange는 여러 스레드가 동시에 호출해도 값을 찢어진 상태 없이 원자적으로 바꾼다.
        Interlocked.Exchange(ref _exitCode, 1);
    }
}

/// <summary>
/// Generic Host 안에서 주기적으로 재고 감시 유스케이스를 실행하는 백그라운드 Worker입니다.
/// Hosted Service는 Singleton이므로 Scoped 서비스를 직접 보관하지 않고 매 회차 새 Scope에서 가져옵니다.
/// </summary>
public sealed class InventoryWatcherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITickSource _tickSource;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly WorkerExitStatus _exitStatus;
    private readonly ILogger<InventoryWatcherWorker> _logger;
    private readonly int _maxCycles;

    /// <summary>
    /// Worker에 Singleton으로 안전한 의존성만 주입하고 최대 실행 횟수를 복사합니다. 생성자는 값을 반환하지 않습니다.
    /// </summary>
    /// <param name="scopeFactory">매 회차 독립된 DI Scope를 만드는 팩터리입니다.</param>
    /// <param name="tickSource">다음 실행 시점을 알려 주는 Singleton 시간 포트입니다.</param>
    /// <param name="applicationLifetime">정해진 회차 뒤 데모 호스트를 정상 종료시키는 수명 제어기입니다.</param>
    /// <param name="exitStatus">처리하지 못한 Worker 실패를 프로세스 종료 코드로 전달하는 Singleton 상태입니다.</param>
    /// <param name="options">시작 시 검증된 주기와 최대 회차 설정입니다.</param>
    /// <param name="logger">회차 결과와 종료 이유를 출력하는 호스트 로거입니다.</param>
    public InventoryWatcherWorker(
        IServiceScopeFactory scopeFactory,
        ITickSource tickSource,
        IHostApplicationLifetime applicationLifetime,
        WorkerExitStatus exitStatus,
        IOptions<InventoryWatcherOptions> options,
        ILogger<InventoryWatcherWorker> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _tickSource = tickSource ?? throw new ArgumentNullException(nameof(tickSource));
        _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
        _exitStatus = exitStatus ?? throw new ArgumentNullException(nameof(exitStatus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);
        _maxCycles = options.Value.MaxCycles;
    }

    /// <summary>
    /// 새 DI Scope를 만들고 Scoped Application Service를 한 번 실행한 뒤 Scope를 반드시 해제합니다.
    /// 공개 메서드라 실제 타이머 없이도 한 회차를 수동·결정적으로 테스트할 수 있습니다.
    /// </summary>
    /// <param name="cancellationToken">이번 한 회차의 조회와 전송을 중단하는 취소 신호입니다.</param>
    /// <returns>Scoped 유스케이스가 만든 한 회차 보고서를 비동기로 반환합니다.</returns>
    public async Task<WatchCycleReport> RunOnceAsync(CancellationToken cancellationToken)
    {
        // 이미 취소된 요청이면 Scoped 객체를 만들 필요가 없으므로 Scope 경계보다 먼저 확인한다.
        cancellationToken.ThrowIfCancellationRequested();

        // await using은 비동기 해제가 필요한 Scope를 메서드가 끝날 때 자동으로 정리하는 문법이다.
        // Repository 같은 Scoped 자원이 회차 밖으로 새지 않게 하는 것이 핵심이다.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var cycle = scope.ServiceProvider.GetRequiredService<IInventoryWatchCycle>();
        return await cycle.RunAsync(cancellationToken);
    }

    /// <summary>
    /// 타이머 신호마다 새 Scope에서 감시 회차를 실행합니다.
    /// 최대 회차나 Tick source 종료에는 Host를 정상 종료하고, Host 취소는 그대로 보존하며, 그 밖의 예외는 실패 코드로 기록해 재전파합니다.
    /// </summary>
    /// <param name="stoppingToken">Ctrl+C 또는 호스트 종료 요청을 모든 대기와 회차에 전달하는 취소 신호입니다.</param>
    /// <returns>Worker 반복 실행 전체의 완료를 나타내며 별도 업무 값은 반환하지 않는 Task입니다.</returns>
    // override는 기반 BackgroundService가 정의한 실행 지점을 이 Worker의 동작으로 교체한다는 뜻이다.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var completedCycles = 0;

            while (completedCycles < _maxCycles)
            {
                var hasNextTick = await _tickSource.WaitForNextTickAsync(stoppingToken);
                if (!hasNextTick)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        // 일부 시간 Adapter는 취소 시 예외 대신 false를 반환할 수 있으므로 종료 로그를 잘못 남기지 않는다.
                        return;
                    }

                    // false는 MaxCycles 완료와 다른 종료 원인이다. 둘을 구분해야 운영 로그가 거짓 성공을 말하지 않는다.
                    _logger.LogInformation(
                        "Tick source가 종료되어 {CompletedCycles}/{MaxCycles}회 뒤 데모 호스트를 종료합니다.",
                        completedCycles,
                        _maxCycles);
                    _applicationLifetime.StopApplication();
                    return;
                }

                // Tick 직후 종료가 요청되었다면 새 Scope를 만들지 않아 종료 경계를 예측 가능하게 유지한다.
                stoppingToken.ThrowIfCancellationRequested();
                var report = await RunOnceAsync(stoppingToken);
                completedCycles++;

                _logger.LogInformation(
                    "감시 회차 {CompletedCycles}/{MaxCycles} 완료 | 검사={InspectedCount} | 경고={AlertCount}",
                    completedCycles,
                    _maxCycles,
                    report.InspectedCount,
                    report.Alerts.Count);
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("설정된 {MaxCycles}회 감시를 마쳐 데모 호스트를 종료합니다.", _maxCycles);
                _applicationLifetime.StopApplication();
            }
        }
        // Host 토큰 자체가 취소 원인인 OperationCanceledException만 정상 종료로 제외한다.
        // 다른 토큰의 timeout 취소가 Host 종료와 우연히 겹치면 실제 장애이므로 실패 상태로 기록한다.
        catch (Exception exception)
            when (exception is not OperationCanceledException cancellationException
                || !stoppingToken.IsCancellationRequested
                || cancellationException.CancellationToken != stoppingToken)
        {
            _exitStatus.MarkFailure();
            _logger.LogCritical(exception, "재고 감시 Worker가 처리하지 못한 오류로 종료됩니다.");
            // 다시 던져 Generic Host의 BackgroundService 예외 정책과 정상적인 자원 정리가 그대로 동작하게 한다.
            throw;
        }
    }
}

/// <summary>
/// 프로그램 시작점에서 모든 구현과 DI 수명을 한곳에 조립하는 Composition Root 확장 메서드입니다.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 설정을 바인딩·시작 검증하고 재고 감시에 필요한 서비스와 수명을 DI 컨테이너에 등록합니다.
    /// </summary>
    /// <param name="services">서비스 등록을 모으는 Generic Host의 DI 컬렉션입니다.</param>
    /// <param name="configuration">appsettings와 환경 변수 등이 합쳐진 애플리케이션 설정입니다.</param>
    /// <returns>다른 등록을 이어 갈 수 있도록 같은 서비스 컬렉션을 반환합니다.</returns>
    // 확장 메서드의 첫 매개변수 앞 `this`는 services.AddInventoryWatcher(...)처럼 자연스럽게 호출하게 해 준다.
    public static IServiceCollection AddInventoryWatcher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetRequiredSection(InventoryWatcherOptions.SectionName);

        // ValidateOnStart는 잘못된 설정을 첫 주기까지 숨기지 않고 호스트 시작 단계에서 실패시킨다.
        // ErrorOnUnknownConfiguration은 기본 Binder가 무시하는 오타 키까지 계약 위반으로 바꾼다.
        services
            .AddOptions<InventoryWatcherOptions>()
            .Bind(section, binderOptions => binderOptions.ErrorOnUnknownConfiguration = true)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Worker와 타이머는 실행 전체에 하나만 필요하므로 Singleton이다.
        services.AddSingleton<ITickSource, PeriodicTimerTickSource>();
        services.AddSingleton<WorkerExitStatus>();

        // 한 회차를 이루는 Repository, Strategy, Sink, Application Service는 함께 생성·해제되도록 모두 Scoped다.
        // 지금은 상태가 없어도 같은 수명 경계를 쓰면 나중에 DbContext나 전송 세션이 추가되어도 Singleton에 새지 않는다.
        services.AddScoped<IInventoryRepository, DemoInventoryRepository>();
        services.AddScoped<IStockThresholdStrategy, DefaultStockThresholdStrategy>();
        services.AddScoped<ILowStockAlertSink, ConsoleLowStockAlertSink>();
        services.AddScoped<IInventoryWatchCycle, InventoryWatchCycle>();

        // AddHostedService는 Worker를 Singleton hosted service로 등록한다.
        services.AddHostedService<InventoryWatcherWorker>();
        return services;
    }
}
