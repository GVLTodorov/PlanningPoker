using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace PlanningPoker.Tests.Integration;

/// <summary>
/// Program.cs sets response cache headers from a middleware whose branch depends on the hosting
/// environment (dev: blanket no-cache; prod: long-cache for fingerprinted _framework/* assets, except
/// the two loader scripts with fixed names). The header is set from Response.OnStarting, which fires
/// for any outgoing response -- even a 404 -- so these requests don't need a real static asset to
/// exist; only the request path's shape matters for which branch runs. Assertions read the parsed
/// CacheControlHeaderValue's individual fields rather than comparing its ToString() -- the header
/// value's directive order in that reconstructed string isn't guaranteed to match the app's literal
/// "public, max-age=31536000, immutable" source string.
/// </summary>
public class ProgramStaticFileCachingTests
{
    [Fact]
    public async Task Development_SetsBlanketNoCache_RegardlessOfPath()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Development));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/_framework/anything.wasm");

        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.MustRevalidate);
        Assert.Equal("no-cache", string.Join(",", response.Headers.Pragma));
        Assert.Equal("0", GetRawHeaderValue(response, "Expires"));
    }

    [Fact]
    public async Task Production_LongCaches_FingerprintedFrameworkAssets()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/_framework/some-fingerprinted-file.abc123.wasm");

        Assert.True(IsLongCached(response));
    }

    [Theory]
    [InlineData("/_framework/blazor.webassembly.js")]
    [InlineData("/_framework/dotnet.js")]
    public async Task Production_DoesNotLongCache_TheUnfingerprintedLoaderScripts(string path)
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.False(IsLongCached(response));
    }

    [Fact]
    public async Task Production_DoesNotLongCache_PathsOutsideFramework()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Production));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.False(IsLongCached(response));
    }

    private static bool IsLongCached(HttpResponseMessage response) =>
        response.Headers.CacheControl is { Public: true, MaxAge: { } maxAge } &&
        maxAge >= TimeSpan.FromDays(300) &&
        response.Headers.CacheControl.Extensions.Any(e => e.Name == "immutable");

    // "0" isn't a valid HTTP-date, so the typed Content.Headers.Expires (DateTimeOffset?) parser
    // silently drops it -- reading the raw header value directly is the only way to see it was set.
    private static string? GetRawHeaderValue(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var headerValues) ||
        response.Content.Headers.TryGetValues(name, out headerValues)
            ? headerValues.FirstOrDefault()
            : null;
}
