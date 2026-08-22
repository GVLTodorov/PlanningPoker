// Browser-driven demo recorder: drives 5 real Chromium sessions through the actual Home/Join/Board
// UI (one host + 4 guests) -- everyone joins, votes, the host reveals, resets, and everyone votes
// again, quickly. Only the host's session is recorded to video. Every wait targets a specific
// element (never NetworkIdle -- SignalR's persistent connection means that would never settle), so
// a stuck flow surfaces as a Playwright TimeoutException and a non-zero exit code: this doubles as
// a live smoke test of join/pick/reveal/reset, not just a video-generation script.
//
//   dotnet run --project PlanningPoker.Tests.Play -c Release -- http://localhost:5232 artifacts/demo-video

using Microsoft.Playwright;
using PlanningPoker.Tests.Play;

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5232";
var outputDir = args.Length > 1 ? args[1] : "artifacts/demo-video";

Directory.CreateDirectory(outputDir);

var rng = new Random();
var usedAvatars = new HashSet<string>();
string[] playerNames = ["Maya", "Noah", "Priya", "Sam", "Alex"];
var viewport = new ViewportSize { Width = 1280, Height = 720 };

Console.WriteLine($"Recording demo against {baseUrl}, output dir {outputDir}");

// Blazor WASM's first page load (downloading + booting the framework) can comfortably outrun
// Playwright's default 30s action timeout on a cold, CPU-constrained CI runner, even though the
// app already answered /healthz. One throwaway request here warms Kestrel + the OS file cache
// before any timed Playwright wait starts, so it isn't the host's own recorded page that eats it.
using (var warmupClient = new HttpClient())
{
    try
    {
        await warmupClient.GetAsync(baseUrl);
    }
    catch
    {
        // best-effort -- if this fails, the real navigation below will surface the real problem
    }
}

const int DefaultTimeoutMs = 60_000;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

// Only the host's context records video -- the recording is "how voting looks to the host", and
// keeping the other 4 sessions unrecorded keeps the artifact small and focused.
var hostContext = await browser.NewContextAsync(new BrowserNewContextOptions
{
    ViewportSize = viewport,
    RecordVideoDir = outputDir,
    RecordVideoSize = new RecordVideoSize { Width = viewport.Width, Height = viewport.Height },
});
hostContext.SetDefaultTimeout(DefaultTimeoutMs);

var guestContexts = new List<IBrowserContext>();
for (var i = 0; i < playerNames.Length - 1; i++)
{
    var guestContext = await browser.NewContextAsync(new BrowserNewContextOptions { ViewportSize = viewport });
    guestContext.SetDefaultTimeout(DefaultTimeoutMs);
    guestContexts.Add(guestContext);
}

var host = new DemoPlayer(await hostContext.NewPageAsync());
var guests = new List<DemoPlayer>();
foreach (var context in guestContexts)
{
    guests.Add(new DemoPlayer(await context.NewPageAsync()));
}

var players = new List<DemoPlayer> { host };
players.AddRange(guests);

Console.WriteLine($"{playerNames[0]} creates the room (becomes host)...");
var roomId = await host.CreateRoomAndJoinAsHostAsync(baseUrl, playerNames[0], usedAvatars);
Console.WriteLine($"Room created: {roomId}");

// Guests join one at a time with a visible stagger, so the host's recording shows each player
// card appearing individually rather than all four popping in at once.
for (var i = 0; i < guests.Count; i++)
{
    var name = playerNames[i + 1];
    Console.WriteLine($"{name} joins...");
    await guests[i].JoinRoomAsync(baseUrl, roomId, name, usedAvatars);
    await Task.Delay(500);
}

Console.WriteLine("Round 1: voting (staggered, so picks are visible one at a time)...");
foreach (var player in players)
{
    await player.PickRandomCardAsync(rng);
    await Task.Delay(rng.Next(400, 900));
}

Console.WriteLine("Host reveals...");
await host.RevealAsync();
await Task.Delay(2_000);

Console.WriteLine("Host resets...");
await host.ResetAsync();
await Task.Delay(2_500); // explicit pause on the freshly-reset board before round 2 starts

Console.WriteLine("Round 2: quick voting...");
foreach (var player in players)
{
    await player.PickRandomCardAsync(rng);
    await Task.Delay(150);
}

Console.WriteLine("Host reveals again...");
await host.RevealAsync();
await Task.Delay(2_000);

foreach (var context in guestContexts)
{
    await context.CloseAsync();
}

var hostPage = host.Page;
await hostContext.CloseAsync(); // flushes the video file
var videoPath = await hostPage.Video!.PathAsync();

await browser.CloseAsync();

Console.WriteLine($"VIDEO_PATH={videoPath}");
