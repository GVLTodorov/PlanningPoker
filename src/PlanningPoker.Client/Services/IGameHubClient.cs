using Microsoft.AspNetCore.SignalR.Client;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Messages;

namespace PlanningPoker.Client.Services;

/// <summary>Abstraction over <see cref="GameHubClient"/> so consumers (e.g. <c>Board.razor</c>) can be
/// driven by a test double instead of a real SignalR connection.</summary>
public interface IGameHubClient : IAsyncDisposable
{
    event Action<RoomStateResponse>? RoomStateChanged;
    event Action<PlayerPickStatusChanged>? PlayerPickChanged;
    event Action<RoundRevealed>? RoundRevealed;
    event Action? RoundReset;
    event Action? RemovedFromRoom;

    HubConnectionState State { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task<JoinRoomResponse> JoinRoomAsync(
        string roomId, string playerName, bool isSpectator, string? avatarUrl, Guid? existingPlayerId = null);

    Task LeaveRoomAsync();

    Task RenameRoomAsync(string newName);

    Task SetPlayerNameAsync(string newName);

    Task SetSpectatorAsync(bool isSpectator);

    Task PickCardAsync(int? cardIndex);

    Task RevealAsync();

    Task ResetAsync();

    Task RemovePlayerAsync(Guid targetPlayerId);
}
