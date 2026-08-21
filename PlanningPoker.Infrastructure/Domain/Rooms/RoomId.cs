namespace PlanningPoker.Domain.Rooms;

/// <summary>Short, URL-safe room identifier. Distinct from the room's (editable) display name.</summary>
public readonly record struct RoomId
{
    // Excludes visually ambiguous characters: 0/O, 1/I/L.
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int Length = 8;

    public string Value { get; }

    private RoomId(string value) => Value = value;

    public static RoomId New()
    {
        Span<char> buffer = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            buffer[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        }

        return new RoomId(new string(buffer));
    }

    public static bool TryParse(string? value, out RoomId roomId)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Length == Length)
        {
            var upper = value.ToUpperInvariant();
            if (upper.All(c => Alphabet.Contains(c)))
            {
                roomId = new RoomId(upper);
                return true;
            }
        }

        roomId = default;
        return false;
    }

    public override string ToString() => Value;
}
