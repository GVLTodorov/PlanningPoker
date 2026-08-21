namespace PlanningPoker.Domain.Rooms;

public sealed class Player
{
    public Guid Id { get; }
    public string Name { get; internal set; }
    public bool IsSpectator { get; internal set; }
    public string? AvatarUrl { get; internal set; }
    public int? PickedCardIndex { get; internal set; }

    internal Player(Guid id, string name, bool isSpectator, string? avatarUrl)
    {
        Id = id;
        Name = name;
        IsSpectator = isSpectator;
        AvatarUrl = avatarUrl;
    }

    public bool HasPicked => PickedCardIndex is not null;
}
