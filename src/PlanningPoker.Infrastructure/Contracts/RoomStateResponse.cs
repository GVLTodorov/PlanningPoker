namespace PlanningPoker.Contracts;

public sealed record RoomStateResponse(
    string RoomId,
    string Name,
    DeckType DeckType,
    RoundStatus Status,
    IReadOnlyList<PlayerResponse> Players);
