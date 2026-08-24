namespace PlanningPoker.Contracts;

public sealed record DeckResponse(DeckType DeckType, string DisplayName, IReadOnlyList<CardOptionResponse> Cards);
