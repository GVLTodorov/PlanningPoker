namespace PlanningPoker.Domain.Errors;

public sealed class SpectatorCannotPlayException : DomainException
{
    public SpectatorCannotPlayException()
        : base("Spectators cannot pick a card.")
    {
    }
}
