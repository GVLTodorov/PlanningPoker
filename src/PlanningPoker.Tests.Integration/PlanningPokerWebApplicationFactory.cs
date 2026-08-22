using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace PlanningPoker.Tests.Integration;

/// <summary>Enables detailed hub errors so test failures show the real server-side exception message.</summary>
public class PlanningPokerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
            services.Configure<HubOptions>(options => options.EnableDetailedErrors = true));
    }
}
