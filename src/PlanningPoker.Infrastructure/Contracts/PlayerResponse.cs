namespace PlanningPoker.Contracts;

/// <summary><see cref="Card"/> is null until the round is revealed, mirroring the server's hidden-hand rule.</summary>
public sealed record PlayerResponse(
    Guid PlayerId,
    string Name,
    bool IsSpectator,
    bool IsHost,
    string? AvatarUrl,
    bool HasPicked,
    CardOptionResponse? Card);
