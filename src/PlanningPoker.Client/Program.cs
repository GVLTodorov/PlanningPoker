using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PlanningPoker.Client;
using PlanningPoker.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<RoomApiClient>();
builder.Services.AddScoped<PlayerSessionState>();
builder.Services.AddScoped<IGameHubClientFactory, GameHubClientFactory>();

// Coverage note: WebAssemblyHostBuilder.CreateDefault() itself throws PlatformNotSupportedException
// outside a real browser WASM host (confirmed: it eagerly calls into
// System.Runtime.InteropServices.JavaScript to read the page's base URI), so nothing in this file --
// not just RunAsync()'s blocking message loop -- can execute inside a dotnet test process. Each
// registered service (RoomApiClient, PlayerSessionState, GameHubClientFactory) is still fully unit
// tested on its own elsewhere; only this file's own DI-wiring lines are the accepted exception. This
// file is only ever actually exercised by the manual Playwright demo workflows (see REQUIREMENTS.MD's
// coverage-exceptions note).
await builder.Build().RunAsync();
