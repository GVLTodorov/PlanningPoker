using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Api.Giphy;
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
    private readonly IRoomRepository _rooms;
    private readonly IPlayerConnectionTracker _connections;
    private readonly IGiphyClient _giphy;

    public GameHub(IRoomRepository rooms, IPlayerConnectionTracker connections, IGiphyClient giphy)
    {
        _rooms = rooms;
        _connections = connections;
        _giphy = giphy;
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

    public async Task SetDeck(Contracts.DeckType deckType)
    {
        var (room, _) = GetTrackedRoomAndPlayer();
        room.SetDeck(deckType.ToDomain());
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
        var (room, _) = GetTrackedRoomAndPlayer();

        // Throws RevealRequiresAllPlayersToPickException (translated to HubException by
        // DomainExceptionHubFilter) unless every non-spectator has picked — enforced here, not
        // just reflected as a disabled button client-side.
        room.Reveal();

        var gifUrls = await _giphy.GetRandomImageUrlsAsync(1);
        var revealed = new RoundRevealed(
            room.GetState().Select(p => p.ToContract()).ToList(),
            gifUrls.Count > 0 ? gifUrls[0] : null);

        await Clients.Group(room.Id.Value).SendAsync("RoundRevealed", revealed);
    }

    public async Task Reset()
    {
        var (room, _) = GetTrackedRoomAndPlayer();
        room.Reset();
        await Clients.Group(room.Id.Value).SendAsync("RoundReset");
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
            _rooms.Remove(info.RoomId);
        }
        else
        {
            await Clients.Group(info.RoomId.Value).SendAsync("RoomStateChanged", room.ToStateDto());
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
