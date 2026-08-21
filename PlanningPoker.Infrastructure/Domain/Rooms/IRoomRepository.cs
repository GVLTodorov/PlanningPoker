using PlanningPoker.Domain.Decks;

namespace PlanningPoker.Domain.Rooms;

public interface IRoomRepository
{
    Room Create(string name, DeckType deckType);

    bool TryGet(RoomId roomId, out Room? room);

    void Remove(RoomId roomId);
}
