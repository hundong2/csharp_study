using EquipmentRentalApi.Application.Ports;
using EquipmentRentalApi.Infrastructure;
using EquipmentRentalApi.Tests.TestDoubles;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EquipmentRentalApi.Tests.Integration;

/// <summary>
/// 실제 Kestrel port 없이 Production pipeline을 띄우고, 외부 의존성만 결정적인 Test Double로 바꿉니다.
/// </summary>
internal sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Production DI 등록 뒤 Repository와 TimeProvider를 테스트 전용 인스턴스로 교체합니다.
    /// </summary>
    /// <param name="builder">WebApplicationFactory가 제공한 테스트 web host builder입니다.</param>
    /// <returns>override hook이므로 반환값은 없습니다.</returns>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        // => lambda는 service collection을 받아 Production DI 등록을 테스트용으로 바꾸는 함수를 전달합니다.
        builder.ConfigureServices(
            services =>
            {
                // RemoveAll은 Production 등록을 남긴 채 하나를 더 추가하는 실수를 막습니다.
                services.RemoveAll<IEquipmentRentalRepository>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<IEquipmentRentalRepository, InMemoryEquipmentRentalRepository>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(TestData.FixedNow));
            });
    }
}
