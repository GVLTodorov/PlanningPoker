namespace PlanningPoker.Domain.Rooms;

/// <summary>
/// URL-safe room identifier, derived from the room's display name (e.g. "Sprint Planning" -&gt;
/// "sprint-planning") so shared room links read as <c>/sprint-planning</c> instead of an opaque code.
/// <see cref="TryParse"/> is used both to derive a new room's id from its name at creation time and
/// to normalize an id coming back in from a URL segment — slugifying an already-slugified value is a
/// no-op, so both paths produce the same key.
/// </summary>
public readonly record struct RoomId
{
    private const int MaxLength = 60;

    public string Value { get; }

    private RoomId(string value) => Value = value;

    public static bool TryParse(string? input, out RoomId roomId)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            roomId = default;
            return false;
        }

        Span<char> buffer = stackalloc char[Math.Min(input.Length, MaxLength)];
        var length = 0;
        var lastWasHyphen = false;

        foreach (var ch in input)
        {
            if (length >= buffer.Length)
            {
                break;
            }

            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                buffer[length++] = ch;
                lastWasHyphen = false;
            }
            else if (ch is >= 'A' and <= 'Z')
            {
                buffer[length++] = char.ToLowerInvariant(ch);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && length > 0)
            {
                buffer[length++] = '-';
                lastWasHyphen = true;
            }
        }

        if (lastWasHyphen)
        {
            length--;
        }

        if (length == 0)
        {
            roomId = default;
            return false;
        }

        roomId = new RoomId(new string(buffer[..length]));
        return true;
    }

    public override string ToString() => Value;
}
