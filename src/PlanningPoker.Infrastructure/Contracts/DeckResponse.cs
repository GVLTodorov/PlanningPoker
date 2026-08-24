namespace PlanningPoker.Contracts;

public sealed record DeckDto(DeckType DeckType, string DisplayName, IReadOnlyList<CardOptionDto> Cards);
