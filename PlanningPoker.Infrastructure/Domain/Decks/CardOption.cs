namespace PlanningPoker.Domain.Decks;

/// <summary>A single selectable card within a deck, addressed by its index for a value-free wire format.</summary>
public sealed record CardOption(int Index, double Value, string Label);
