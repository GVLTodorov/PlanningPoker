using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Client.Services;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Contracts.Requests;
using PlanningPoker.Contracts.Serialization;
using Xunit;

namespace PlanningPoker.Tests.Integration;

/// <summary>
/// GameHubClient.cs itself has no internal branches (it's a straight pass-through wrapper), so full
/// line coverage just means exercising every method once through a real connection -- unlike
/// GameHubTests.cs (which drives the raw HubConnection/hub protocol directly), this drives the
/// wrapper's own public methods and events, using its `GameHubClient(HubConnection)` constructor to
/// point it at the same in-memory TestServer.
/// </summary>
public class GameHubClientTests : IClassFixture<PlanningPokerWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = PlanningPokerJsonContext.CreateOptions();

    private readonly PlanningPokerWebApplicationFactory _factory;

    public GameHubClientTests(PlanningPokerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullLifecycle_ThroughGameHubClientsOwnMethods_RaisesEveryEvent()
    {
        using var httpClient = _factory.CreateClient();
        var createResponse = await httpClient.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Client Lifecycle Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);
        Assert.NotNull(room);

        await using var alice = NewClient();
        await using var bob = NewClient();

        var aliceStateChanges = new List<RoomStateResponse>();
        alice.RoomStateChanged += s => aliceStateChanges.Add(s);

        var alicePickChanges = new List<PlayerPickStatusChanged>();
        alice.PlayerPickChanged += c => alicePickChanges.Add(c);

        RoundRevealed? aliceRevealed = null;
        alice.RoundRevealed += r => aliceRevealed = r;

        var aliceResetCount = 0;
        alice.RoundReset += () => aliceResetCount++;

        var bobWasRemoved = false;
        bob.RemovedFromRoom += () => bobWasRemoved = true;

        Assert.Equal(HubConnectionState.Disconnected, alice.State);
        await alice.StartAsync();
        await bob.StartAsync();
        Assert.Equal(HubConnectionState.Connected, alice.State);

        var aliceJoin = await alice.JoinRoomAsync(room!.RoomId, "Alice", isSpectator: false, avatarUrl: null);
        var bobJoin = await bob.JoinRoomAsync(room.RoomId, "Bob", isSpectator: false, avatarUrl: null);
        Assert.Equal(2, bobJoin.State.Players.Count);

        await bob.SetSpectatorAsync(true);
        await WaitUntilAsync(() => aliceStateChanges.Any(s => s.Players.Single(p => p.PlayerId == bobJoin.PlayerId).IsSpectator));

        await bob.SetSpectatorAsync(false);
        await bob.SetPlayerNameAsync("Bobby");
        await WaitUntilAsync(() => aliceStateChanges.Any(s => s.Players.Any(p => p.PlayerId == bobJoin.PlayerId && p.Name == "Bobby")));

        await alice.PickCardAsync(2);
        await WaitUntilAsync(() => alicePickChanges.Any(c => c.PlayerId == aliceJoin.PlayerId && c.HasPicked));

        await bob.PickCardAsync(4);
        await alice.RevealAsync();
        await WaitUntilAsync(() => aliceRevealed is not null);
        Assert.Equal(2, aliceRevealed!.Players.Count);

        await alice.ResetAsync();
        await WaitUntilAsync(() => aliceResetCount > 0);

        await alice.RenameRoomAsync("Renamed Room");
        await WaitUntilAsync(() => aliceStateChanges.Any(s => s.Name == "Renamed Room"));

        await alice.RemovePlayerAsync(bobJoin.PlayerId);
        await WaitUntilAsync(() => bobWasRemoved);

        await alice.LeaveRoomAsync();
    }

    private GameHubClient NewClient() => new(
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions = PlanningPokerJsonContext.CreateOptions())
            .Build());

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition was not met within the timeout.");
            }

            await Task.Delay(25);
        }
    }
}
