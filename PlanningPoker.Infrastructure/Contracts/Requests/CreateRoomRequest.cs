namespace PlanningPoker.Contracts.Requests;

public sealed record CreateRoomRequest(string Name, DeckType DeckType);
