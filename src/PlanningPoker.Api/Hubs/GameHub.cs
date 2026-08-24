using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Api.Mapping;
using PlanningPoker.Api.Realtime;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Domain.Rooms;

namespace PlanningPoker.Api.Hubs;

/// <summary>
/// Realtime surface for a room, one SignalR group per room. Most mutations broadcast a full
/// <see cref="ContractMapper.ToStateResponse"/> snapshot; <see cref="PickCard"/> is the exception — it's
/// the hottest path (every pick/unpick fans out to every connected player), so it broadcasts only
/// the <see cref="PlayerPickStatusChanged"/> diff.
/// </summary>
public sealed class GameHub : Hub
{
    private readonly IRoomRepository _rooms;
    private readonly IPlayerConnectionTracker _connections;
    private readonly GameHubTimingOptions _timing;
    private readonly IHubContext<GameHub> _hubContext;

    public GameHub(
        IRoomRepository rooms,
        IPlayerConnectionTracker connections,
        GameHubTimingOptions timing,
        IHubContext<GameHub> hubContext)
    {
        _rooms = rooms;
        _connections = connections;
        _timing = timing;
        _hubContext = hubContext;
    }

    public async Task<JoinRoomResponse> JoinRoom(
        string roomId, string playerName, bool isSpectator, string? avatarUrl, Guid? existingPlayerId = null)
    {
        var room = GetRoomOrThrow(roomId);

        // Track the connection under the target player id *before* touching room state, so a
        // concurrent PlayerReconnectGracePeriod sweep can never observe "reclaimed in Room, but not
        // yet tracked as connected" and remove the player out from under this very rejoin.
        var playerId = existingPlayerId ?? Guid.NewGuid();
        _connections.Track(Context.ConnectionId, room.Id, playerId);

        var player = room.AddPlayer(playerName, isSpectator, avatarUrl, playerId);

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id.Value);

        await Clients.OthersInGroup(room.Id.Value).SendAsync("RoomStateChanged", room.ToStateResponse());

        return new JoinRoomResponse(player.Id, room.ToStateResponse());
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

        await Clients.Group(room.Id.Value).SendAsync("RoomStateChanged", room.ToStateResponse());
    }

    private async Task HandleDisconnectAsync()
    {
        if (!_connections.TryGet(Context.ConnectionId, out var info))
        {
            return;
        }

        _connections.Remove(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, info.RoomId.Value);

        // Don't remove the player yet -- give a reconnect (JoinRoom with this same player id) a
        // chance to land first and reclaim their spot, instead of racing a removal against it.
        _ = RemovePlayerIfStillDisconnectedAfterDelayAsync(info.RoomId, info.PlayerId);
    }

    // Runs well after the JoinRoom/disconnect invocation that scheduled it has returned, so it must
    // not touch this instance's Clients/Groups/Context -- those are only valid for the lifetime of
    // the triggering hub method call. _hubContext (a real singleton, not tied to any one invocation)
    // is the supported way to broadcast from background work like this.
    private async Task RemovePlayerIfStillDisconnectedAfterDelayAsync(RoomId roomId, Guid playerId)
    {
        await Task.Delay(_timing.PlayerReconnectGracePeriod);

        if (_connections.TryGetConnectionId(roomId, playerId, out _))
        {
            // A new connection claimed this player id in the meantime -- they reconnected.
            return;
        }

        if (!_rooms.TryGet(roomId, out var room) || room is null)
        {
            return;
        }

        var isNowEmpty = room.RemovePlayer(playerId);
        if (isNowEmpty)
        {
            _ = RemoveIfStillEmptyAfterDelayAsync(roomId);
        }
        else
        {
            await _hubContext.Clients.Group(roomId.Value).SendAsync("RoomStateChanged", room.ToStateResponse());
        }
    }

    private async Task RemoveIfStillEmptyAfterDelayAsync(RoomId roomId)
    {
        await Task.Delay(_timing.EmptyRoomGracePeriod);

        if (_rooms.TryGet(roomId, out var room) && room is not null && room.IsEmpty)
        {
            _rooms.Remove(roomId);
        }
    }

    private async Task BroadcastStateAsync(Room room) =>
        await Clients.Group(room.Id.Value).SendAsync("RoomStateChanged", room.ToStateResponse());

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
