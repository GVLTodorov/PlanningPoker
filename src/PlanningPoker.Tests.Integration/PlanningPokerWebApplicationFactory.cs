using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Api.Realtime;

namespace PlanningPoker.Tests.Integration;

/// <summary>Enables detailed hub errors so test failures show the real server-side exception message,
/// and shrinks GameHub's reconnect grace windows so tests that exercise the "still gone after the
/// grace period" path don't have to actually wait out the production-sized 15 seconds.</summary>
public class PlanningPokerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.Configure<HubOptions>(options => options.EnableDetailedErrors = true);
            services.AddSingleton(new GameHubTimingOptions
            {
                EmptyRoomGracePeriod = TimeSpan.FromSeconds(1),
                PlayerReconnectGracePeriod = TimeSpan.FromSeconds(1),
            });
        });
    }
}
