using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using ReadingListApi.Api;
using ReadingListApi.Application;
using ReadingListApi.Infrastructure;
using ReadingListApi.SelfTest;

// 최상위 문장은 프로그램의 시작점입니다. --self-test이면 서버 대신 서비스 규칙을 빠르게 검사합니다.
if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
{
    return ServiceSelfTest.Run();
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Composition Root는 실제 구현을 인터페이스에 연결하는 한곳입니다. DI가 같은 저장소를 모든 요청에 나눠줍니다.
builder.Services.AddSingleton<IBookRepository, InMemoryBookRepository>();
builder.Services.AddSingleton<IBookFilterStrategy, StatusBookFilterStrategy>();
builder.Services.AddSingleton<BookService>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(ConfigureJson);

WebApplication app = builder.Build();

// 예외 처리기를 앞에 놓으면 뒤쪽 처리기에서 예상 밖의 오류가 생겨도 ProblemDetails로 응답할 수 있습니다.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<RequestIdMiddleware>();
app.MapBookEndpoints();
app.Run();
return 0;

/// <summary>
/// enum을 toRead 같은 문자열로 직렬화합니다. options는 HTTP JSON 설정이며 반환값은 없습니다.
/// </summary>
static void ConfigureJson(JsonOptions options)
{
    // Converter는 내부 enum 이름을 API에서 읽기 쉬운 camelCase 문자열로 변환합니다.
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
}
