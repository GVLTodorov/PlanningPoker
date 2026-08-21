namespace PlanningPoker.Domain.Errors;

public sealed class PlayerNotInRoomException : DomainException
{
    public PlayerNotInRoomException(string playerId)
        : base($"Player '{playerId}' is not in this room.")
    {
    }
}
