using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Api.Mapping;
using PlanningPoker.Api.Realtime;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Domain.Rooms;

namespace PlanningPoker.Api.Hubs;

/// <summary>
/// Realtime surface for a room, one SignalR group per room. Most mutations broadcast a full
/// <see cref="ContractMapper.ToStateDto"/> snapshot; <see cref="PickCard"/> is the exception — it's
/// the hottest path (every pick/unpick fans out to every connected player), so it broadcasts only
/// the <see cref="PlayerPickStatusChanged"/> diff.
/// </summary>
public sealed class GameHub : Hub
{
    // A page refresh briefly drops the only connection to a room before the reload's JoinRoom call
    // lands a moment later -- deleting the room the instant it empties would race a delete against
    // that rejoin. This grace window comfortably covers a WASM cold-boot-and-reconnect cycle.
    private static readonly TimeSpan EmptyRoomGracePeriod = TimeSpan.FromSeconds(15);

    private readonly IRoomRepository _rooms;
    private readonly IPlayerConnectionTracker _connections;

    public GameHub(IRoomRepository rooms, IPlayerConnectionTracker connections)
    {
        _rooms = rooms;
        _connections = connections;
    }

    public async Task<JoinRoomResult> JoinRoom(string roomId, string playerName, bool isSpectator, string? avatarUrl)
    {
        var room = GetRoomOrThrow(roomId);
        var player = room.AddPlayer(playerName, isSpectator, avatarUrl);

        _connections.Track(Context.ConnectionId, room.Id, player.Id);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id.Value);

        await Clients.OthersInGroup(room.Id.Value).SendAsync("RoomStateChanged", room.ToStateDto());

        return new JoinRoomResult(player.Id, room.ToStateDto());
    }

    public Task LeaveRoom() => HandleDisconnectAsync();

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await HandleDisconnectAsync();
        await base.OnDisconnectedAsync(exception);
    }

    public async Task RenameRoom(string newName)
    {
        var (room, _) = GetTrackedRoomAndPlayer();
        room.Rename(newName);
        await BroadcastStateAsync(room);
    }

    public async Task SetPlayerName(string newName)
    {
        var (room, playerId) = GetTrackedRoomAndPlayer();
        room.SetPlayerName(playerId, newName);
        await BroadcastStateAsync(room);
    }

    public async Task SetSpectator(bool isSpectator)
    {
        var (room, playerId) = GetTrackedRoomAndPlayer();
        room.SetSpectator(playerId, isSpectator);
        await BroadcastStateAsync(room);
    }

    public async Task PickCard(int? cardIndex)
    {
        var (room, playerId) = GetTrackedRoomAndPlayer();
        room.PickCard(playerId, cardIndex);

        await Clients.Group(room.Id.Value).SendAsync(
            "PlayerPickStatusChanged", new PlayerPickStatusChanged(playerId, cardIndex is not null));
    }

    public async Task Reveal()
    {
        var (room, playerId) = GetTrackedRoomAndPlayer();

        // Throws UnauthorizedAccessException / InvalidOperationException (translated to
        // HubException by GameRuleExceptionHubFilter) unless the caller is host and every
        // non-spectator has picked — enforced here, not just reflected as a disabled button
        // client-side.
        room.Reveal(playerId);

        var revealed = new RoundRevealed(room.GetState().Select(p => p.ToContract()).ToList());

        await Clients.Group(room.Id.Value).SendAsync("RoundRevealed", revealed);
    }

    public async Task Reset()
    {
        var (room, playerId) = GetTrackedRoomAndPlayer();
        room.Reset(playerId);
        await Clients.Group(room.Id.Value).SendAsync("RoundReset");
    }

    public async Task RemovePlayer(Guid targetPlayerId)
    {
        var (room, hostPlayerId) = GetTrackedRoomAndPlayer();

        // Throws UnauthorizedAccessException (translated to HubException by
        // GameRuleExceptionHubFilter) unless the caller is host — enforced here, not just reflected
        // as a hidden button client-side.
        room.RemovePlayerAsHost(hostPlayerId, targetPlayerId);

        if (_connections.TryGetConnectionId(room.Id, targetPlayerId, out var targetConnectionId))
        {
            _connections.Remove(targetConnectionId);
            await Groups.RemoveFromGroupAsync(targetConnectionId, room.Id.Value);
            await Clients.Client(targetConnectionId).SendAsync("RemovedFromRoom");
        }

        await Clients.Group(room.Id.Value).SendAsync("RoomStateChanged", room.ToStateDto());
    }

    private async Task HandleDisconnectAsync()
    {
        if (!_connections.TryGet(Context.ConnectionId, out var info))
        {
            return;
        }

        _connections.Remove(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, info.RoomId.Value);

        if (!_rooms.TryGet(info.RoomId, out var room) || room is null)
        {
            return;
        }

        var isNowEmpty = room.RemovePlayer(info.PlayerId);
        if (isNowEmpty)
        {
            _ = RemoveIfStillEmptyAfterDelayAsync(info.RoomId);
        }
        else
        {
            await Clients.Group(info.RoomId.Value).SendAsync("RoomStateChanged", room.ToStateDto());
        }
    }

    private async Task RemoveIfStillEmptyAfterDelayAsync(RoomId roomId)
    {
        await Task.Delay(EmptyRoomGracePeriod);

        if (_rooms.TryGet(roomId, out var room) && room is not null && room.IsEmpty)
        {
            _rooms.Remove(roomId);
        }
    }

    private async Task BroadcastStateAsync(Room room) =>
        await Clients.Group(room.Id.Value).SendAsync("RoomStateChanged", room.ToStateDto());

    private Room GetRoomOrThrow(string roomId)
    {
        if (!RoomId.TryParse(roomId, out var parsed) || !_rooms.TryGet(parsed, out var room) || room is null)
        {
            throw new HubException("Room not found.");
        }

        return room;
    }

    private (Room Room, Guid PlayerId) GetTrackedRoomAndPlayer()
    {
        if (!_connections.TryGet(Context.ConnectionId, out var info))
        {
            throw new HubException("Not connected to a room.");
        }

        if (!_rooms.TryGet(info.RoomId, out var room) || room is null)
        {
            throw new HubException("Room not found.");
        }

        return (room, info.PlayerId);
    }
}
