namespace PlanningPoker.Api.Extensions;

/// <summary>
/// The grace windows <see cref="Hubs.GameHub"/> waits before treating a disconnect as final -- long
/// enough to cover a page refresh's brief drop-then-reconnect.
/// </summary>
public static class ApiConstants
{
    /// <summary>How long an empty room is kept around before deletion, in case its last player is
    /// mid-refresh rather than gone for good.</summary>
    public static readonly TimeSpan EmptyRoomGracePeriod = TimeSpan.FromSeconds(15);

    /// <summary>How long a disconnected player is kept in the room before being removed, in case
    /// they reconnect (same room, same player id) and reclaim their identity instead.</summary>
    public static readonly TimeSpan PlayerReconnectGracePeriod = TimeSpan.FromSeconds(15);
}
