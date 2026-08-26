using Microsoft.AspNetCore.SignalR.Client;
using PlanningPoker.Client.Services;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Messages;

namespace PlanningPoker.Tests.Component.TestSupport;

/// <summary>Hand-written IGameHubClient test double (this repo uses no mocking framework). Every
/// method records its call so a test can assert what Board.razor invoked; JoinRoomAsync's result is
/// driven by a TaskCompletionSource so a test can control exactly when the join "completes" -- e.g.
/// to observe Board's private event handlers running while `_state` is still null.</summary>
internal sealed class FakeGameHubClient : IGameHubClient
{
    public event Action<RoomStateResponse>? RoomStateChanged;
    public event Action<PlayerPickStatusChanged>? PlayerPickChanged;
    public event Action<RoundRevealed>? RoundRevealed;
    public event Action? RoundReset;
    public event Action? RemovedFromRoom;
    public event Action? Reconnected;

    public List<string> Calls { get; } = [];

    public TaskCompletionSource<JoinRoomResponse> JoinRoomResult { get; } = new();

    public bool Disposed { get; private set; }

    public HubConnectionState State { get; set; } = HubConnectionState.Disconnected;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Calls.Add(nameof(StartAsync));
        return Task.CompletedTask;
    }

    public Task<JoinRoomResponse> JoinRoomAsync(
        string roomId, string playerName, bool isSpectator, string? avatarUrl, Guid? existingPlayerId = null)
    {
        Calls.Add($"{nameof(JoinRoomAsync)}({roomId},{playerName},{isSpectator},{avatarUrl},{existingPlayerId})");
        return JoinRoomResult.Task;
    }

    public Task LeaveRoomAsync()
    {
        Calls.Add(nameof(LeaveRoomAsync));
        return Task.CompletedTask;
    }

    public Task RenameRoomAsync(string newName)
    {
        Calls.Add($"{nameof(RenameRoomAsync)}({newName})");
        return Task.CompletedTask;
    }

    public Task SetPlayerNameAsync(string newName)
    {
        Calls.Add($"{nameof(SetPlayerNameAsync)}({newName})");
        return Task.CompletedTask;
    }

    public Task SetSpectatorAsync(bool isSpectator)
    {
        Calls.Add($"{nameof(SetSpectatorAsync)}({isSpectator})");
        return Task.CompletedTask;
    }

    public Task PickCardAsync(int? cardIndex)
    {
        Calls.Add($"{nameof(PickCardAsync)}({cardIndex})");
        return Task.CompletedTask;
    }

    public Task RevealAsync()
    {
        Calls.Add(nameof(RevealAsync));
        return Task.CompletedTask;
    }

    public Task ResetAsync()
    {
        Calls.Add(nameof(ResetAsync));
        return Task.CompletedTask;
    }

    public Task RemovePlayerAsync(Guid targetPlayerId)
    {
        Calls.Add($"{nameof(RemovePlayerAsync)}({targetPlayerId})");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    public void RaiseRoomStateChanged(RoomStateResponse state) => RoomStateChanged?.Invoke(state);

    public void RaisePlayerPickChanged(PlayerPickStatusChanged change) => PlayerPickChanged?.Invoke(change);

    public void RaiseRoundRevealed(RoundRevealed revealed) => RoundRevealed?.Invoke(revealed);

    public void RaiseRoundReset() => RoundReset?.Invoke();

    public void RaiseRemovedFromRoom() => RemovedFromRoom?.Invoke();

    public void RaiseReconnected() => Reconnected?.Invoke();
}
