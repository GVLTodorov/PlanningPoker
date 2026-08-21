using System.Collections.Concurrent;
using PlanningPoker.Domain.Decks;

namespace PlanningPoker.Domain.Rooms;

/// <summary>
/// Pure in-memory room store, no database. A room is fully deleted (not just evicted) when its
/// last player leaves — see <see cref="Room.RemovePlayer"/> callers.
/// </summary>
public sealed class InMemoryRoomRepository : IRoomRepository
{
    private readonly ConcurrentDictionary<RoomId, Room> _rooms = new();

    public Room Create(string name, DeckType deckType)
    {
        var room = new Room(RoomId.New(), name, deckType);
        _rooms[room.Id] = room;
        return room;
    }

    public bool TryGet(RoomId roomId, out Room? room) => _rooms.TryGetValue(roomId, out room);

    public void Remove(RoomId roomId) => _rooms.TryRemove(roomId, out _);
}
