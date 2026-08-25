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
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);
        Assert.NotNull(room);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();

        var aliceStateChanges = new List<RoomStateResponse>();
        aliceConnection.On<RoomStateResponse>("RoomStateChanged", state => aliceStateChanges.Add(state));

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

        // PlayerReconnectGracePeriod (ApiConstants) is 15s; wait comfortably past it.
        await WaitUntilAsync(() => aliceStateChanges.Any(s => s.Players.Count == 1), timeoutMs: 20_000);
        Assert.Single(aliceStateChanges.Last(s => s.Players.Count <= 1).Players);
    }

    [Fact]
    public async Task JoinRoom_WithExistingPlayerId_ReclaimsHostStatus_AfterReconnect()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Reconnect Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

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
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

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
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();

        var bobWasRemoved = false;
        bobConnection.On("RemovedFromRoom", () => bobWasRemoved = true);

        var aliceStateChanges = new List<RoomStateResponse>();
        aliceConnection.On<RoomStateResponse>("RoomStateChanged", state => aliceStateChanges.Add(state));

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
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

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
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var connection = CreateHubConnection();
        await connection.StartAsync();
        await JoinAsync(connection, room!.RoomId, "Watcher", isSpectator: true);

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("PickCard", 0));
        Assert.Contains("spectator", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JoinRoom_Throws_WhenRoomIdIsBogus()
    {
        await using var connection = CreateHubConnection();
        await connection.StartAsync();

        var ex = await Assert.ThrowsAsync<HubException>(
            () => JoinAsync(connection, "no-such-room", "Alice"));
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnyHubMethod_Throws_WhenCalledBeforeJoiningARoom()
    {
        await using var connection = CreateHubConnection();
        await connection.StartAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("PickCard", 0));
        Assert.Contains("not connected", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LeaveRoom_UntracksThePlayer_SoFurtherCallsOnThatConnectionAreRejected()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Leave Room Test", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var connection = CreateHubConnection();
        await connection.StartAsync();
        await JoinAsync(connection, room!.RoomId, "Alice");

        await connection.InvokeAsync("LeaveRoom");

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("PickCard", 0));
        Assert.Contains("not connected", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Disconnect_WithoutHavingJoinedARoom_IsANoOp()
    {
        // HandleDisconnectAsync's very first guard (_connections.TryGet returning false) exists
        // specifically for this case -- a connection that opened and closed without ever joining a
        // room. Nothing to assert beyond "this doesn't throw/hang", since there's no tracked state to
        // observe either way.
        await using var connection = CreateHubConnection();
        await connection.StartAsync();

        await connection.StopAsync();
    }

    [Fact]
    public async Task RenameRoom_ChangesTheName_AndBroadcastsToOtherPlayers()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Rename Room Test", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();
        var bobStateChanges = new List<RoomStateResponse>();
        bobConnection.On<RoomStateResponse>("RoomStateChanged", state => bobStateChanges.Add(state));

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();
        await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        await JoinAsync(bobConnection, room.RoomId, "Bob");

        await aliceConnection.InvokeAsync("RenameRoom", "Renamed");

        await WaitUntilAsync(() => bobStateChanges.Any(s => s.Name == "Renamed"));
    }

    [Fact]
    public async Task SetPlayerName_ChangesTheCallersName_AndBroadcasts()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Set Name Test", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();
        var bobStateChanges = new List<RoomStateResponse>();
        bobConnection.On<RoomStateResponse>("RoomStateChanged", state => bobStateChanges.Add(state));

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();
        var aliceJoin = await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        await JoinAsync(bobConnection, room.RoomId, "Bob");

        await aliceConnection.InvokeAsync("SetPlayerName", "Alicia");

        await WaitUntilAsync(() => bobStateChanges.Any(
            s => s.Players.Any(p => p.PlayerId == aliceJoin.PlayerId && p.Name == "Alicia")));
    }

    [Fact]
    public async Task SetSpectator_AtHubLevel_UpdatesTheFlag_AndBroadcasts()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Set Spectator Test", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();
        var bobStateChanges = new List<RoomStateResponse>();
        bobConnection.On<RoomStateResponse>("RoomStateChanged", state => bobStateChanges.Add(state));

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();
        var aliceJoin = await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        await JoinAsync(bobConnection, room.RoomId, "Bob");

        await aliceConnection.InvokeAsync("SetSpectator", true);

        await WaitUntilAsync(() => bobStateChanges.Any(
            s => s.Players.Single(p => p.PlayerId == aliceJoin.PlayerId).IsSpectator));
    }

    [Fact]
    public async Task Reveal_ByNonHost_Throws()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Reveal Rejection Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();
        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();
        await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        await JoinAsync(bobConnection, room.RoomId, "Bob");

        var ex = await Assert.ThrowsAsync<HubException>(() => bobConnection.InvokeAsync("Reveal"));
        Assert.Contains("host", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reset_ByNonHost_Throws()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Reset Rejection Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        await using var bobConnection = CreateHubConnection();
        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();
        await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        await JoinAsync(bobConnection, room.RoomId, "Bob");
        await aliceConnection.InvokeAsync("PickCard", 0);
        await bobConnection.InvokeAsync("PickCard", 0);
        await aliceConnection.InvokeAsync("Reveal");

        var ex = await Assert.ThrowsAsync<HubException>(() => bobConnection.InvokeAsync("Reset"));
        Assert.Contains("host", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemovePlayer_TargetAlreadyDisconnected_StillUpdatesRoomState()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Kick Already Gone Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        var bobConnection = CreateHubConnection();
        var aliceStateChanges = new List<RoomStateResponse>();
        aliceConnection.On<RoomStateResponse>("RoomStateChanged", state => aliceStateChanges.Add(state));

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();
        var aliceJoin = await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        var bobJoin = await JoinAsync(bobConnection, room.RoomId, "Bob");

        // Bob's connection tracking is removed synchronously on disconnect (only the *domain-level*
        // player removal is delayed by the reconnect grace period) -- so Alice's RemovePlayer call
        // right after this hits the `TryGetConnectionId` false branch deterministically, without
        // needing to wait out any grace period.
        await bobConnection.DisposeAsync();

        await aliceConnection.InvokeAsync("RemovePlayer", bobJoin.PlayerId);

        await WaitUntilAsync(() => aliceStateChanges.Any(s => s.Players.Count == 1));
        Assert.Equal(aliceJoin.PlayerId, aliceStateChanges.Last().Players.Single().PlayerId);
    }

    // SignalR's hub dispatcher requires the exact declared arity -- it does NOT fill in C# optional
    // parameters for a client that sends fewer arguments -- so every JoinRoom call needs all five,
    // even when existingPlayerId is just null. Centralized here so that's a one-line fact, not five
    // near-identical InvokeAsync calls each carrying their own trailing nulls.
    private static Task<JoinRoomResponse> JoinAsync(
        HubConnection connection, string roomId, string playerName,
        bool isSpectator = false, Guid? existingPlayerId = null) =>
        connection.InvokeAsync<JoinRoomResponse>("JoinRoom", roomId, playerName, isSpectator, null, existingPlayerId);

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
            // round-trip correctly and plain (non-model) argument types like PickCard's int? still
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
