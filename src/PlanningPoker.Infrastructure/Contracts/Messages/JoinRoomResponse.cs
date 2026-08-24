namespace PlanningPoker.Contracts.Messages;

/// <summary>Returned directly to the caller of <c>GameHub.JoinRoom</c> (a full snapshot, not a broadcast).</summary>
public sealed record JoinRoomResponse(Guid PlayerId, RoomStateResponse State);
