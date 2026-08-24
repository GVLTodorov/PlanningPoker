using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Contracts.Serialization;

namespace PlanningPoker.Client.Services;

/// <summary>Thin wrapper over a <see cref="HubConnection"/> to <c>Api/Hubs/GameHub.cs</c>.</summary>
public sealed class GameHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public event Action<RoomStateDto>? RoomStateChanged;
    public event Action<PlayerPickStatusChanged>? PlayerPickChanged;
    public event Action<RoundRevealed>? RoundRevealed;
    public event Action? RoundReset;
    public event Action? RemovedFromRoom;

    public GameHubClient(NavigationManager navigation)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(navigation.ToAbsoluteUri("/hubs/game"))
            .WithAutomaticReconnect()
            .AddJsonProtocol(options => options.PayloadSerializerOptions = PlanningPokerJsonContext.CreateOptions())
            .Build();

        _connection.On<RoomStateDto>("RoomStateChanged", state => RoomStateChanged?.Invoke(state));
        _connection.On<PlayerPickStatusChanged>("PlayerPickStatusChanged", change => PlayerPickChanged?.Invoke(change));
        _connection.On<RoundRevealed>("RoundRevealed", revealed => RoundRevealed?.Invoke(revealed));
        _connection.On("RoundReset", () => RoundReset?.Invoke());
        _connection.On("RemovedFromRoom", () => RemovedFromRoom?.Invoke());
    }

    public HubConnectionState State => _connection.State;

    public Task StartAsync(CancellationToken cancellationToken = default) => _connection.StartAsync(cancellationToken);

    public Task<JoinRoomResult> JoinRoomAsync(
        string roomId, string playerName, bool isSpectator, string? avatarUrl, Guid? existingPlayerId = null) =>
        _connection.InvokeAsync<JoinRoomResult>(
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
