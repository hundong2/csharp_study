using ConditionalCatalogApi.Application;
using ConditionalCatalogApi.Application.Ports;
using ConditionalCatalogApi.Infrastructure;
using ConditionalCatalogApi.Presentation;
using ConditionalCatalogApi.SelfTesting;

// top-level statements는 별도 Main 메서드 껍데기 없이 프로그램 시작 코드를 쓰는 C# 문법입니다.
// compiler가 args와 종료 코드를 가진 Main을 만들어 주므로 이 파일을 Composition Root에 집중시킵니다.
// var는 오른쪽 식에서 compile-time 타입을 추론합니다. WebApplicationBuilder라는 긴 타입을 반복하지 않되 동적 타입으로 바꾸지는 않습니다.
var builder = WebApplication.CreateBuilder(args);

// AddSingleton은 앱 전체에서 같은 thread-safe 인스턴스를 재사용하는 DI 등록입니다.
// 구체 구현 선택과 수명 결정은 Composition Root 한곳에 모아 Application이 new로 Adapter를 만들지 않게 합니다.
builder.Services.AddSingleton<ICatalogRepository>(_ =>
    new InMemoryCatalogRepository(SeedData.Create()));
builder.Services.AddSingleton<IEntityTagCodec, VersionEntityTagCodec>();
builder.Services.AddSingleton<CatalogApplicationService>();

var app = builder.Build();

// Contains는 args를 순회해 자체 검증 스위치가 있는지 찾습니다. 서버를 띄우지 않는 빠른 검증 경로를 분리합니다.
if (args.Contains("--self-test", StringComparer.Ordinal))
{
    // top-level await는 비동기 검증 동안 thread를 막지 않고 기다린 뒤 그 결과를 process 종료 코드로 돌려줍니다.
    return await SelfTestRunner.RunAsync();
}

// extension method는 기존 WebApplication에 우리 route 연결 동작을 자연스럽게 붙이는 문법입니다.
app.MapCatalogEndpoints();

Console.WriteLine("학습용 상품 API가 시작되었습니다.");
Console.WriteLine("GET/PATCH /catalog/11111111-1111-1111-1111-111111111111");

// RunAsync는 종료 신호가 올 때까지 요청을 처리합니다. 정상 종료 뒤 process 종료 코드 0을 반환합니다.
await app.RunAsync();
return 0;
