namespace PlanningPoker.Contracts;

/// <summary><see cref="Card"/> is null until the round is revealed, mirroring the server's hidden-hand rule.</summary>
public sealed record PlayerDto(
    Guid PlayerId,
    string Name,
    bool IsSpectator,
    string? AvatarUrl,
    bool HasPicked,
    CardOptionDto? Card);
