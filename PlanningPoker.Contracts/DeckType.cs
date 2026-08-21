namespace PlanningPoker.Contracts;

/// <summary>
/// Wire-level mirror of <c>PlanningPoker.Domain.Decks.DeckType</c>. Kept as a separate type so the
/// Blazor client never needs to reference the Domain assembly.
/// </summary>
public enum DeckType
{
    Fibonacci,
    ModifiedFibonacci,
    Powers,
    TrustVote,
    TShirts,
}
