using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Contracts.Requests;
using PlanningPoker.Contracts.Serialization;
using Xunit;

namespace PlanningPoker.Tests.Integration;

/// <summary>
/// GameHub's two background sweeps (RemovePlayerIfStillDisconnectedAfterDelayAsync,
/// RemoveIfStillEmptyAfterDelayAsync) only fire after ApiConstants' real 15-second grace periods
/// elapse -- kept in their own file since these tests are meaningfully slower than the rest of the
/// suite (each one waits out at least one real grace period).
/// </summary>
public class GameHubDisconnectSweepTests : IClassFixture<PlanningPokerWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = PlanningPokerJsonContext.CreateOptions();

    private readonly PlanningPokerWebApplicationFactory _factory;

    public GameHubDisconnectSweepTests(PlanningPokerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DisconnectedPlayer_IsRemoved_AfterTheReconnectGracePeriodExpires()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Sweep Removal Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        var bobConnection = CreateHubConnection();
        var aliceStateChanges = new List<RoomStateResponse>();
        aliceConnection.On<RoomStateResponse>("RoomStateChanged", state => aliceStateChanges.Add(state));

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();
        var aliceJoin = await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        await JoinAsync(bobConnection, room.RoomId, "Bob");

        await bobConnection.DisposeAsync();

        // PlayerReconnectGracePeriod is 15s; wait comfortably past it for the sweep's own broadcast
        // (the room is not empty afterward, so this exercises the "still has players" broadcast
        // branch of RemovePlayerIfStillDisconnectedAfterDelayAsync).
        await WaitUntilAsync(() => aliceStateChanges.Any(s => s.Players.Count == 1), timeoutMs: 20_000);
        Assert.Equal(aliceJoin.PlayerId, aliceStateChanges.Last().Players.Single().PlayerId);
    }

    [Fact]
    public async Task DisconnectedPlayer_IsNotRemoved_WhenTheyReconnectWithinTheGracePeriod()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Sweep Reconnect Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        await using var aliceConnection = CreateHubConnection();
        var bobConnection = CreateHubConnection();

        await aliceConnection.StartAsync();
        await bobConnection.StartAsync();
        await JoinAsync(aliceConnection, room!.RoomId, "Alice");
        var bobJoin = await JoinAsync(bobConnection, room.RoomId, "Bob");

        await bobConnection.DisposeAsync();

        // Reconnect well inside the 15s grace period -- the sweep, once it does run, must find
        // Bob's player id tracked again (by this new connection) and return without touching the room.
        await using var bobReconnection = CreateHubConnection();
        await bobReconnection.StartAsync();
        await JoinAsync(bobReconnection, room.RoomId, "Bob", existingPlayerId: bobJoin.PlayerId);

        // Wait past the original grace period, then confirm Bob is still present via a fresh query.
        await Task.Delay(16_000);
        var stateResponse = await client.GetAsync($"/api/rooms/{room.RoomId}");
        stateResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Room_IsRemoved_WhenItStaysEmptyThroughBothGracePeriods()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Sweep Empty Room", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        var soloConnection = CreateHubConnection();
        await soloConnection.StartAsync();
        await JoinAsync(soloConnection, room!.RoomId, "Solo");

        await soloConnection.DisposeAsync();

        // PlayerReconnectGracePeriod (15s) elapses, the room becomes empty, then
        // EmptyRoomGracePeriod (another 15s) elapses before the room itself is deleted -- poll past
        // both rather than a single fixed delay, to keep this robust against scheduling jitter.
        await WaitUntilAsync(
            async () =>
            {
                var response = await client.GetAsync($"/api/rooms/{room.RoomId}");
                return response.StatusCode == HttpStatusCode.NotFound;
            },
            timeoutMs: 35_000);
    }

    private static Task<JoinRoomResponse> JoinAsync(
        HubConnection connection, string roomId, string playerName,
        bool isSpectator = false, Guid? existingPlayerId = null) =>
        connection.InvokeAsync<JoinRoomResponse>("JoinRoom", roomId, playerName, isSpectator, null, existingPlayerId);

    private HubConnection CreateHubConnection() =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
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

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutMs = 5000)
    {
        var start = DateTime.UtcNow;
        while (!await condition())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition was not met within the timeout.");
            }

            await Task.Delay(100);
        }
    }
}
