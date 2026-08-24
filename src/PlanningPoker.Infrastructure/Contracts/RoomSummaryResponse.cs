namespace PlanningPoker.Contracts;

public sealed record RoomSummaryResponse(string RoomId, string Name, DeckType DeckType);
