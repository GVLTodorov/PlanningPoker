using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Api.Endpoints;
using PlanningPoker.Api.Giphy;
using PlanningPoker.Api.Hubs;
using PlanningPoker.Api.Realtime;
using PlanningPoker.Contracts.Serialization;
using PlanningPoker.Domain.Rooms;

var builder = WebApplication.CreateBuilder(args);

// Gitignored, machine-local overrides (e.g. a real Giphy API key for debugging)
// layered on top of the tracked appsettings.*.json files — never committed.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var giphyOptions = new GiphyOptions
{
    BaseUrl = builder.Configuration["GIPHY_API_BASE_URL"],
    Query = builder.Configuration["GIPHY_API_QUERY"],
};
builder.Services.AddSingleton(giphyOptions);
builder.Services.AddMemoryCache();

if (giphyOptions.IsConfigured)
{
    builder.Services.AddHttpClient<IGiphyClient, GiphyClient>();
}
else
{
    builder.Services.AddSingleton<IGiphyClient, NullGiphyClient>();
}

builder.Services.AddSingleton<IRoomRepository, InMemoryRoomRepository>();
builder.Services.AddSingleton<IPlayerConnectionTracker, PlayerConnectionTracker>();

builder.Services
    .AddSignalR(options => options.AddFilter<DomainExceptionHubFilter>())
    .AddJsonProtocol(options => options.PayloadSerializerOptions = PlanningPokerJsonContext.CreateOptions());

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, PlanningPokerJsonContext.Default);
    options.SerializerOptions.TypeInfoResolverChain.Add(new DefaultJsonTypeInfoResolver());
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

var app = builder.Build();

app.UseResponseCompression();

// Most _framework/* filenames are content-hashed by the Blazor build, so they can be cached
// indefinitely -- except the two loader scripts (blazor.webassembly.js, dotnet.js), which keep
// their fixed names (see PlanningPoker.Client.csproj's OverrideHtmlAssetPlaceholders comment) and
// so must NOT be cached forever, or a returning visitor could run a stale loader against a new
// deploy's fingerprinted assembly set. Everything outside _framework/ (index.html, css, ...) keeps
// the framework's own short/no-cache default. Set from OnStarting (not inline here) because the
// static file middleware downstream sets its own Cache-Control just before writing the response --
// setting it earlier in the pipeline would just get overwritten.
string[] unfingerprintedFrameworkFiles = ["/_framework/blazor.webassembly.js", "/_framework/dotnet.js"];
app.Use((context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/_framework") &&
        !unfingerprintedFrameworkFiles.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return Task.CompletedTask;
        });
    }

    return next();
});

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapRoomEndpoints();
app.MapHub<GameHub>("/hubs/game");
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program
{
}
