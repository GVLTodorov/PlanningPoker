// Small SignalR load-driver: spins up N rooms x M players, runs several pick/reveal/reset cycles
// per room concurrently, and reports p50/p95/max latency for the PickCard round trip -- the
// hottest path in the app (every pick fans out to every connected player). Not gated in CI
// (REQUIREMENTS.MD Section 10.3 explicitly allows that); run manually against a live instance:
//
//   dotnet run --project PlanningPoker.Tests.LoadTest -c Release -- http://localhost:6232 20 5 10

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Requests;
using PlanningPoker.Contracts.Serialization;

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:6232";
var roomCount = args.Length > 1 ? int.Parse(args[1]) : 20;
var playersPerRoom = args.Length > 2 ? int.Parse(args[2]) : 5;
var cyclesPerRoom = args.Length > 3 ? int.Parse(args[3]) : 10;

Console.WriteLine($"Load test: {roomCount} rooms x {playersPerRoom} players x {cyclesPerRoom} pick/reveal/reset cycles against {baseUrl}");

var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
var jsonOptions = PlanningPokerJsonContext.CreateOptions();
var pickLatenciesMs = new ConcurrentBag<double>();

var overallStopwatch = Stopwatch.StartNew();

var roomTasks = Enumerable.Range(0, roomCount).Select(async roomIndex =>
{
    var createResponse = await httpClient.PostAsJsonAsync(
        "/api/rooms", new CreateRoomRequest($"Load {roomIndex}", DeckType.Fibonacci), jsonOptions);
    var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(jsonOptions)
        ?? throw new InvalidOperationException("Room creation failed during load test setup.");

    var connections = new List<HubConnection>();
    for (var p = 0; p < playersPerRoom; p++)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"{baseUrl}/hubs/game")
            .AddJsonProtocol(o => o.PayloadSerializerOptions = PlanningPokerJsonContext.CreateOptions())
            .Build();

        await connection.StartAsync();
        await connection.InvokeAsync<object>("JoinRoom", room.RoomId, $"Bot{roomIndex}-{p}", false, null, null);
        connections.Add(connection);
    }

    for (var cycle = 0; cycle < cyclesPerRoom; cycle++)
    {
        foreach (var connection in connections)
        {
            var stopwatch = Stopwatch.StartNew();
            await connection.InvokeAsync("PickCard", cycle % 11);
            pickLatenciesMs.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        await connections[0].InvokeAsync("Reveal");
        await connections[0].InvokeAsync("Reset");
    }

    foreach (var connection in connections)
    {
        await connection.DisposeAsync();
    }
});

await Task.WhenAll(roomTasks);
overallStopwatch.Stop();

var sorted = pickLatenciesMs.OrderBy(x => x).ToList();

double Percentile(double p)
{
    if (sorted.Count == 0)
    {
        return 0;
    }

    var index = (int)Math.Clamp(Math.Round(p * (sorted.Count - 1)), 0, sorted.Count - 1);
    return sorted[index];
}

Console.WriteLine();
Console.WriteLine($"Total wall time: {overallStopwatch.Elapsed.TotalSeconds:F2}s");
Console.WriteLine($"PickCard round-trip samples: {sorted.Count}");
Console.WriteLine($"p50: {Percentile(0.50):F2} ms");
Console.WriteLine($"p95: {Percentile(0.95):F2} ms");
Console.WriteLine($"p99: {Percentile(0.99):F2} ms");
Console.WriteLine($"max: {(sorted.Count > 0 ? sorted[^1] : 0):F2} ms");
