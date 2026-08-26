using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Contracts.Serialization;

namespace PlanningPoker.Client.Services;

/// <summary>Thin wrapper over a <see cref="HubConnection"/> to <c>Api/Hubs/GameHub.cs</c>.</summary>
public sealed class GameHubClient : IGameHubClient
{
    private readonly HubConnection _connection;

    public event Action<RoomStateResponse>? RoomStateChanged;
    public event Action<PlayerPickStatusChanged>? PlayerPickChanged;
    public event Action<RoundRevealed>? RoundRevealed;
    public event Action? RoundReset;
    public event Action? RemovedFromRoom;
    public event Action? Reconnected;

    public GameHubClient(NavigationManager navigation)
        : this(new HubConnectionBuilder()
            .WithUrl(navigation.ToAbsoluteUri("/hubs/game"))
            .WithAutomaticReconnect()
            .AddJsonProtocol(options => options.PayloadSerializerOptions = PlanningPokerJsonContext.CreateOptions())
            .Build())
    {
    }

    /// <summary>Accepts an already-built <see cref="HubConnection"/> directly. Used by
    /// PlanningPoker.Tests.Integration/GameHubClientTests.cs to point this wrapper's real event
    /// plumbing at an in-memory TestServer (the same <c>HttpMessageHandlerFactory</c> +
    /// forced-transport trick GameHubTests.cs already uses for raw <see cref="HubConnectionBuilder"/>
    /// usage) instead of a live browser-hosted URL -- HttpConnectionOptions itself isn't referenceable
    /// from this Blazor WebAssembly project's compile target, so callers configure the connection
    /// themselves and hand it in already-built.</summary>
    public GameHubClient(HubConnection connection)
    {
        _connection = connection;

        _connection.On<RoomStateResponse>("RoomStateChanged", state => RoomStateChanged?.Invoke(state));
        _connection.On<PlayerPickStatusChanged>("PlayerPickStatusChanged", change => PlayerPickChanged?.Invoke(change));
        _connection.On<RoundRevealed>("RoundRevealed", revealed => RoundRevealed?.Invoke(revealed));
        _connection.On("RoundReset", () => RoundReset?.Invoke());
        _connection.On("RemovedFromRoom", () => RemovedFromRoom?.Invoke());

        // WithAutomaticReconnect() restores transport connectivity transparently, but the new
        // connection has a fresh ConnectionId -- the server's per-connection room/player tracking
        // (GameHub's IPlayerTracker) has no entry for it until JoinRoom runs again. Without this,
        // every hub method after a reconnect throws "Not connected to a room." the moment the user
        // next picks a card. The caller re-invokes JoinRoomAsync with the existing player id here.
        _connection.Reconnected += _ =>
        {
            Reconnected?.Invoke();
            return Task.CompletedTask;
        };
    }

    public HubConnectionState State => _connection.State;

    public Task StartAsync(CancellationToken cancellationToken = default) => _connection.StartAsync(cancellationToken);

    public Task<JoinRoomResponse> JoinRoomAsync(
        string roomId, string playerName, bool isSpectator, string? avatarUrl, Guid? existingPlayerId = null) =>
        _connection.InvokeAsync<JoinRoomResponse>(
            "JoinRoom", roomId, playerName, isSpectator, avatarUrl, existingPlayerId);

    public Task LeaveRoomAsync() => _connection.InvokeAsync("LeaveRoom");

    public Task RenameRoomAsync(string newName) => _connection.InvokeAsync("RenameRoom", newName);

    public Task SetPlayerNameAsync(string newName) => _connection.InvokeAsync("SetPlayerName", newName);

    public Task SetSpectatorAsync(bool isSpectator) => _connection.InvokeAsync("SetSpectator", isSpectator);

    public Task PickCardAsync(int? cardIndex) => _connection.InvokeAsync("PickCard", cardIndex);

    public Task RevealAsync() => _connection.InvokeAsync("Reveal");

    public Task ResetAsync() => _connection.InvokeAsync("Reset");

    public Task RemovePlayerAsync(Guid targetPlayerId) => _connection.InvokeAsync("RemovePlayer", targetPlayerId);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
