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

    public Room? Create(string name, DeckType deckType)
    {
        if (!RoomId.TryParse(name, out var id))
        {
            return null;
        }

        var room = new Room(id, name, deckType);
        return _rooms.TryAdd(id, room) ? room : null;
    }

    public bool TryGet(RoomId roomId, out Room? room) => _rooms.TryGetValue(roomId, out room);

    public void Remove(RoomId roomId) => _rooms.TryRemove(roomId, out _);
}

public interface IRoomRepository
{
    /// <returns>
    /// The created room, or <see langword="null"/> if <paramref name="name"/> has no usable
    /// characters to derive a <see cref="RoomId"/> from, or the resulting id is already taken.
    /// </returns>
    Room? Create(string name, DeckType deckType);

    bool TryGet(RoomId roomId, out Room? room);

    void Remove(RoomId roomId);
}
