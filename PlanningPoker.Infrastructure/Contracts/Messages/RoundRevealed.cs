namespace PlanningPoker.Contracts.Messages;

public sealed record RoundRevealed(IReadOnlyList<PlayerDto> Players, string? GifUrl);
