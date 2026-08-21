using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Contracts.Requests;
using PlanningPoker.Contracts.Serialization;
using Xunit;

namespace PlanningPoker.Tests.Integration;

/// <summary>
/// Drives the whole realtime flow (join -> partial pick -> reveal rejected -> remaining pick ->
/// reveal accepted -> reset -> disconnect) end-to-end through the real HTTP + SignalR surface, with
/// no UI involved.
/// </summary>
public class GameHubTests : IClassFixture<PlanningPokerWebApplicationFactory>
{
    // Mirrors the server's REST JSON options (Program.cs): default System.Net.Http.Json web
    // defaults don't know how to read our string-formatted enums without this.
    private static readonly JsonSerializerOptions JsonOptions = PlanningPokerJsonContext.CreateOptions();

    private readonly PlanningPokerWebApplicationFactory _factory;

    public GameHubTests(PlanningPokerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullRoundLifecycle_JoinPickRevealResetDisconnect_BehavesAsExpected()
    {
        using var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Sprint Planning", DeckType.Fibonacci), JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryDto>(JsonOptions);
        Assert.NotNull(room);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();

        var aliceStateChanges = new List<RoomStateDto>();
        aliceConnection.On<RoomStateDto>("RoomStateChanged", state => aliceStateChanges.Add(state));

        var alicePickChanges = new List<PlayerPickStatusChanged>();
        aliceConnection.On<PlayerPickStatusChanged>("PlayerPickStatusChanged", change => alicePickChanges.Add(change));

        RoundRevealed? aliceRevealed = null;
        aliceConnection.On<RoundRevealed>("RoundRevealed", revealed => aliceRevealed = revealed);

        var aliceResetCount = 0;
        aliceConnection.On("RoundReset", () => aliceResetCount++);

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();

        var aliceJoin = await aliceConnection.InvokeAsync<JoinRoomResult>(
            "JoinRoom", room!.RoomId, "Alice", false, null);
        var bobJoin = await bobConnection.InvokeAsync<JoinRoomResult>(
            "JoinRoom", room.RoomId, "Bob", false, null);

        Assert.Equal(2, bobJoin.State.Players.Count);

        await aliceConnection.InvokeAsync("PickCard", 2);

        // Reveal must be rejected server-side while Bob hasn't picked yet -- this is the explicit
        // deviation from the reference implementation (which allows revealing at any time).
        var rejection = await Assert.ThrowsAsync<HubException>(
            () => aliceConnection.InvokeAsync("Reveal"));
        Assert.Contains("pick a card", rejection.Message, StringComparison.OrdinalIgnoreCase);

        await bobConnection.InvokeAsync("PickCard", 4);
        await aliceConnection.InvokeAsync("Reveal");

        await WaitUntilAsync(() => aliceRevealed is not null);
        Assert.NotNull(aliceRevealed);
        Assert.Equal(2, aliceRevealed!.Players.Count);
        Assert.All(aliceRevealed.Players, p => Assert.NotNull(p.Card));

        await aliceConnection.InvokeAsync("Reset");
        await WaitUntilAsync(() => aliceResetCount > 0);
        Assert.Equal(1, aliceResetCount);

        await bobConnection.StopAsync();
        await bobConnection.DisposeAsync();

        await WaitUntilAsync(() => aliceStateChanges.Any(s => s.Players.Count == 1));
        Assert.Single(aliceStateChanges.Last(s => s.Players.Count <= 1).Players);
    }

    [Fact]
    public async Task Reveal_Throws_WhenNoPlayersHavePicked()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Empty Round", DeckType.Powers), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryDto>(JsonOptions);

        await using var connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync<JoinRoomResult>("JoinRoom", room!.RoomId, "Solo", false, null);

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("Reveal"));
    }

    [Fact]
    public async Task PickCard_Throws_WhenPlayerIsSpectator()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Spectator Room", DeckType.Powers), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryDto>(JsonOptions);

        await using var connection = CreateHubConnection();
        await connection.StartAsync();
        await connection.InvokeAsync<JoinRoomResult>("JoinRoom", room!.RoomId, "Watcher", true, null);

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("PickCard", 0));
        Assert.Contains("spectator", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private HubConnection CreateHubConnection() =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                // WebSocket transport uses ClientWebSocket directly, bypassing the injected
                // handler, so it can't reach TestServer's in-memory endpoint -- long polling can.
                options.Transports = HttpTransportType.LongPolling;
            })
            // Must mirror the server's hub JSON options (Program.cs) so string-formatted enums
            // round-trip correctly and plain (non-DTO) argument types like PickCard's int? still
            // resolve instead of throwing NotSupportedException.
            .AddJsonProtocol(options => options.PayloadSerializerOptions = PlanningPokerJsonContext.CreateOptions())
            .Build();

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
