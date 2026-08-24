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

        var aliceJoin = await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        var bobJoin = await JoinAsync(bobConnection, room.RoomId, "Bob");

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
    public async Task JoinRoom_WithExistingPlayerId_ReclaimsHostStatus_AfterReconnect()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Reconnect Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryDto>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();

        var aliceJoin = await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        await JoinAsync(bobConnection, room.RoomId, "Bob");
        Assert.True(aliceJoin.State.Players.Single(p => p.PlayerId == aliceJoin.PlayerId).IsHost);

        // Simulates a page refresh: the old connection drops (no explicit LeaveRoom -- a refresh
        // just tears down the socket) and a brand new connection rejoins with the same player id
        // while the room still has another player in it. Without the disconnect grace period plus
        // existingPlayerId reuse, Alice would come back as a fresh, non-host player here -- exactly
        // the bug this test guards against.
        await aliceConnection.StopAsync();

        await using var aliceReconnection = CreateHubConnection();
        await aliceReconnection.StartAsync();
        var rejoin = await JoinAsync(aliceReconnection, room.RoomId, "Alice", existingPlayerId: aliceJoin.PlayerId);

        Assert.Equal(aliceJoin.PlayerId, rejoin.PlayerId);
        Assert.Equal(2, rejoin.State.Players.Count);
        Assert.True(rejoin.State.Players.Single(p => p.PlayerId == aliceJoin.PlayerId).IsHost);
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
        await JoinAsync(connection, room!.RoomId, "Solo");

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("Reveal"));
    }

    [Fact]
    public async Task RemovePlayer_ByHost_DisconnectsTargetAndUpdatesRoomState()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Kick Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryDto>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();

        var bobWasRemoved = false;
        bobConnection.On("RemovedFromRoom", () => bobWasRemoved = true);

        var aliceStateChanges = new List<RoomStateDto>();
        aliceConnection.On<RoomStateDto>("RoomStateChanged", state => aliceStateChanges.Add(state));

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();

        var aliceJoin = await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        var bobJoin = await JoinAsync(bobConnection, room.RoomId, "Bob");

        await aliceConnection.InvokeAsync("RemovePlayer", bobJoin.PlayerId);

        await WaitUntilAsync(() => bobWasRemoved);
        await WaitUntilAsync(() => aliceStateChanges.Any(s => s.Players.Count == 1));
        Assert.Single(aliceStateChanges.Last().Players);
        Assert.Equal(aliceJoin.PlayerId, aliceStateChanges.Last().Players.Single().PlayerId);
    }

    [Fact]
    public async Task RemovePlayer_ByNonHost_Throws()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Kick Rejection Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryDto>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();

        var aliceJoin = await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        await JoinAsync(bobConnection, room.RoomId, "Bob");

        var ex = await Assert.ThrowsAsync<HubException>(
            () => bobConnection.InvokeAsync("RemovePlayer", aliceJoin.PlayerId));
        Assert.Contains("host", ex.Message, StringComparison.OrdinalIgnoreCase);
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
        await JoinAsync(connection, room!.RoomId, "Watcher", isSpectator: true);

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("PickCard", 0));
        Assert.Contains("spectator", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // SignalR's hub dispatcher requires the exact declared arity -- it does NOT fill in C# optional
    // parameters for a client that sends fewer arguments -- so every JoinRoom call needs all five,
    // even when existingPlayerId is just null. Centralized here so that's a one-line fact, not five
    // near-identical InvokeAsync calls each carrying their own trailing nulls.
    private static Task<JoinRoomResult> JoinAsync(
        HubConnection connection, string roomId, string playerName,
        bool isSpectator = false, Guid? existingPlayerId = null) =>
        connection.InvokeAsync<JoinRoomResult>("JoinRoom", roomId, playerName, isSpectator, null, existingPlayerId);

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
