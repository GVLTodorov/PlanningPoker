namespace PlanningPoker.Domain.Errors;

public sealed class RoomNotFoundException : DomainException
{
    public RoomNotFoundException(string roomId)
        : base($"Room '{roomId}' does not exist.")
    {
    }
}
