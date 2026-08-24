namespace PlanningPoker.Contracts.Messages;

public sealed record RoundRevealed(IReadOnlyList<PlayerResponse> Players);
