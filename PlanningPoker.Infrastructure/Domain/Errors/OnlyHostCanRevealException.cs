namespace PlanningPoker.Domain.Errors;

/// <summary>
/// Only the room's host (the first player to join it) may trigger a reveal, enforced server-side so
/// a non-host client can't force one via a raw hub call.
/// </summary>
public sealed class OnlyHostCanRevealException : DomainException
{
    public OnlyHostCanRevealException()
        : base("Only the room host can reveal cards.")
    {
    }
}
