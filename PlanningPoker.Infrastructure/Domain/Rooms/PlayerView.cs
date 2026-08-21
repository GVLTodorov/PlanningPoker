using PlanningPoker.Domain.Decks;

namespace PlanningPoker.Domain.Rooms;

/// <summary>
/// Read-only projection of a player for broadcast to clients. <see cref="Card"/> is populated only
/// once the round has been revealed; before that only <see cref="HasPicked"/> is visible, mirroring
/// the reference implementation's hidden-hand-until-reveal behavior.
/// </summary>
public sealed record PlayerView(
    Guid PlayerId,
    string Name,
    bool IsSpectator,
    string? AvatarUrl,
    bool HasPicked,
    CardOption? Card);
