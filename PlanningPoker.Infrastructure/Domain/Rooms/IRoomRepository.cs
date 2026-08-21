using PlanningPoker.Domain.Decks;

namespace PlanningPoker.Domain.Rooms;

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
