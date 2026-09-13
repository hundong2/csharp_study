using InventoryWatcherExercise.Hosting;
using InventoryWatcherExercise.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// 최상위 문은 Program 클래스를 직접 쓰지 않아도 실행 시작점을 만드는 C# 문법이다.
// --self-test는 외부 테스트 패키지 없이 학습 예제의 계약을 빠르게 검증하는 진입점이다.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    // `return await`는 비동기 테스트 결과를 기다린 뒤 그 정수를 이 프로세스의 종료 코드로 바로 돌려준다.
    return await SelfTests.RunAllAsync();
}

// ContentRootPath를 출력 폴더로 고정하면 저장소 루트에서 실행해도 복사된 appsettings.json을 찾는다.
// Host의 기본 구성 순서는 JSON 뒤에 환경 변수를 읽으므로 운영 환경의 override 동작도 유지된다.
// 객체 initializer는 생성 직후 여러 속성을 중괄호 안에서 이름으로 지정하는 문법이다.
var settings = new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
};

var builder = Host.CreateApplicationBuilder(settings);
builder.Services.AddInventoryWatcher(builder.Configuration);

// using은 실행 블록이 끝날 때 Host와 그 안의 DI 서비스를 Dispose해 자원을 빠짐없이 정리한다.
using var host = builder.Build();
var exitStatus = host.Services.GetRequiredService<WorkerExitStatus>();
await host.RunAsync();

// .NET 10에서 BackgroundService 예외 뒤 RunAsync가 정상 완료되는 경우에도 Worker가 기록한 실패 코드를 반환한다.
return exitStatus.ExitCode;
