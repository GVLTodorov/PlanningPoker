namespace PlanningPoker.Domain.Errors;

public sealed class InvalidCardValueException : DomainException
{
    public InvalidCardValueException(int cardIndex)
        : base($"Card index {cardIndex} is not valid for the current deck.")
    {
    }
}
