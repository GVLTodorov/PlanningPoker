using System.Collections.Concurrent;
using PlanningPoker.Domain.Rooms;

namespace PlanningPoker.Api.Realtime;

public sealed class PlayerConnectionTracker : IPlayerConnectionTracker
{
    private readonly ConcurrentDictionary<string, (RoomId RoomId, Guid PlayerId)> _connections = new();

    public void Track(string connectionId, RoomId roomId, Guid playerId) =>
        _connections[connectionId] = (roomId, playerId);

    public bool TryGet(string connectionId, out (RoomId RoomId, Guid PlayerId) info) =>
        _connections.TryGetValue(connectionId, out info);

    public bool TryGetConnectionId(RoomId roomId, Guid playerId, out string connectionId)
    {
        foreach (var (candidateConnectionId, info) in _connections)
        {
            if (info.RoomId.Equals(roomId) && info.PlayerId == playerId)
            {
                connectionId = candidateConnectionId;
                return true;
            }
        }

        connectionId = string.Empty;
        return false;
    }

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);
}
