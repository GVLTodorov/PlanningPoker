namespace PlanningPoker.Contracts.Messages;

/// <summary>
/// The hottest broadcast path (every card pick/unpick fans out to every connected player), so it
/// carries only the diff a client needs — not the full <see cref="RoomStateDto"/>.
/// </summary>
public sealed record PlayerPickStatusChanged(Guid PlayerId, bool HasPicked);
