using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace PlanningPoker.Tests.Integration;

/// <summary>Enables detailed hub errors so test failures show the real server-side exception
/// message. GameHub's reconnect grace windows (<see cref="PlanningPoker.Api.Extensions.ApiConstants"/>)
/// are fixed constants, not DI-overridable, so tests that exercise the "still gone after the grace
/// period" path (see <c>GameHubDisconnectSweepTests</c>) genuinely wait out the production-sized 15
/// seconds.</summary>
public class PlanningPokerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.Configure<HubOptions>(options => options.EnableDetailedErrors = true);
        });
    }
}
