namespace PlanningPoker.Contracts;

public sealed record RoomStateDto(
    string RoomId,
    string Name,
    DeckType DeckType,
    RoundStatus Status,
    IReadOnlyList<PlayerDto> Players);
