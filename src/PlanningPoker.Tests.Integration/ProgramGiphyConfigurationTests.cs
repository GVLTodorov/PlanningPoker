using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Api.Giphy;
using Xunit;

namespace PlanningPoker.Tests.Integration;

/// <summary>
/// Program.cs registers a real GiphyClient or a no-op NullGiphyClient depending on whether
/// GIPHY_API_BASE_URL/GIPHY_API_QUERY resolve to a non-blank value -- both branches need their own
/// factory with an explicit config override rather than relying on whatever happens to be (or not be)
/// set in the machine running the tests.
/// </summary>
public class ProgramGiphyConfigurationTests
{
    [Fact]
    public async Task GiphyClient_IsRegistered_WhenGiphyOptionsAreConfigured()
    {
        // Program.cs's `if (giphyOptions.IsConfigured)` branch runs synchronously inside
        // `WebApplication.CreateBuilder(args)`/before `Build()`, but `WithWebHostBuilder`'s
        // `ConfigureAppConfiguration` only composes sources at `Build()` time -- too late to affect
        // that branch. Real process environment variables, however, are picked up by
        // `AddEnvironmentVariables()` as part of that same `CreateBuilder(args)` call, so they're the
        // only override that actually lands before the branch decision is made. (This test passed
        // locally on a machine with a real appsettings.Local.json purely by accident -- that file's
        // real credentials made IsConfigured true regardless of whether this override worked. CI has
        // no such file, so it failed there until this fix.)
        Environment.SetEnvironmentVariable("GIPHY_API_BASE_URL", "https://example.test/search");
        Environment.SetEnvironmentVariable("GIPHY_API_QUERY", "q=test");
        try
        {
            await using var factory = new WebApplicationFactory<Program>();

            using var scope = factory.Services.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IGiphyClient>();

            Assert.IsType<GiphyClient>(client);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIPHY_API_BASE_URL", null);
            Environment.SetEnvironmentVariable("GIPHY_API_QUERY", null);
        }
    }

    [Fact]
    public async Task NullGiphyClient_IsRegistered_WhenGiphyOptionsAreBlank()
    {
        // Program.cs's own `AddJsonFile("appsettings.Local.json", ...)` is the LAST configuration
        // source it adds, specifically so a developer's local secrets always win -- which means a
        // machine with a real local Giphy key (like this one) would otherwise make this test flaky by
        // always resolving as "configured" no matter what ConfigureAppConfiguration/UseSetting override
        // is layered on top. Pointing the content root at an empty temp directory makes that relative
        // appsettings.Local.json lookup miss (it's `optional: true`), so the branch is deterministic
        // regardless of what's on the machine running the tests.
        var emptyContentRoot = Directory.CreateTempSubdirectory("planningpoker-tests-").FullName;

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(emptyContentRoot);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GIPHY_API_BASE_URL"] = "",
                ["GIPHY_API_QUERY"] = "",
            }));
        });

        using var scope = factory.Services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IGiphyClient>();

        Assert.IsType<NullGiphyClient>(client);
    }
}
