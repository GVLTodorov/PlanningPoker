namespace PlanningPoker.Domain.Errors;

/// <summary>
/// Deliberate deviation from the reference implementation: the reference allows revealing at any
/// time. Here, revealing is only permitted once every non-spectator player has picked a card, and
/// this rule is enforced server-side, not merely reflected as a disabled button in the UI.
/// </summary>
public sealed class RevealRequiresAllPlayersToPickException : DomainException
{
    public RevealRequiresAllPlayersToPickException()
        : base("Every non-spectator player must pick a card before revealing.")
    {
    }
}
