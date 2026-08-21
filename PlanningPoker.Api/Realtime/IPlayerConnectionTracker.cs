using PlanningPoker.Domain.Rooms;

namespace PlanningPoker.Api.Realtime;

/// <summary>
/// Maps a SignalR connection to the room/player it joined, so hub methods after JoinRoom don't need
/// the caller to keep re-supplying the room id. No reconnect grace period: a dropped connection is
/// simply forgotten, matching the reference implementation's session-scoped behavior.
/// </summary>
public interface IPlayerConnectionTracker
{
    void Track(string connectionId, RoomId roomId, Guid playerId);

    bool TryGet(string connectionId, out (RoomId RoomId, Guid PlayerId) info);

    void Remove(string connectionId);
}
