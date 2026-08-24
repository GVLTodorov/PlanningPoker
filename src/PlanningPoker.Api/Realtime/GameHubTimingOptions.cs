namespace PlanningPoker.Api.Realtime;

/// <summary>
/// The grace windows <see cref="Hubs.GameHub"/> waits before treating a disconnect as final --
/// long enough in production to cover a page refresh's brief drop-then-reconnect, short enough in
/// tests (see <c>PlanningPokerWebApplicationFactory</c>) that exercising the "still disconnected
/// after the grace period" path doesn't mean an actually-slow test.
/// </summary>
public sealed class GameHubTimingOptions
{
    /// <summary>How long an empty room is kept around before deletion, in case its last player is
    /// mid-refresh rather than gone for good.</summary>
    public TimeSpan EmptyRoomGracePeriod { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>How long a disconnected player is kept in the room before being removed, in case
    /// they reconnect (same room, same player id) and reclaim their identity instead.</summary>
    public TimeSpan PlayerReconnectGracePeriod { get; init; } = TimeSpan.FromSeconds(15);
}
