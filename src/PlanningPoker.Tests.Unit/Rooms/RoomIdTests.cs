using PlanningPoker.Domain.Rooms;
using Xunit;

namespace PlanningPoker.Tests.Unit.Rooms;

public class RoomIdTests
{
    [Theory]
    [InlineData("Sprint Planning", "sprint-planning")]
    [InlineData("  Trailing   Spaces  ", "trailing-spaces")]
    [InlineData("clever-a3f9k2", "clever-a3f9k2")]
    [InlineData("Café Déjà Vu!!", "caf-d-j-vu")]
    [InlineData("UPPER_CASE", "upper-case")]
    public void TryParse_SlugifiesName(string input, string expectedSlug)
    {
        var parsed = RoomId.TryParse(input, out var roomId);

        Assert.True(parsed);
        Assert.Equal(expectedSlug, roomId.Value);
    }

    [Fact]
    public void TryParse_IsIdempotent_SoUrlSegmentsRoundTrip()
    {
        RoomId.TryParse("Sprint Planning", out var fromName);

        var parsed = RoomId.TryParse(fromName.Value, out var fromUrlSegment);

        Assert.True(parsed);
        Assert.Equal(fromName, fromUrlSegment);
    }

    [Fact]
    public void TryParse_IsCaseInsensitive()
    {
        RoomId.TryParse("Sprint Planning", out var lower);

        var parsed = RoomId.TryParse("SPRINT PLANNING", out var upper);

        Assert.True(parsed);
        Assert.Equal(lower, upper);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void TryParse_RejectsInputWithNoUsableCharacters(string? input)
    {
        var parsed = RoomId.TryParse(input, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_TruncatesVeryLongNames()
    {
        var longName = new string('a', 200);

        var parsed = RoomId.TryParse(longName, out var roomId);

        Assert.True(parsed);
        Assert.Equal(60, roomId.Value.Length);
    }

    [Fact]
    public void TryParse_SuppressesLeadingHyphen_WhenInputStartsWithPunctuation()
    {
        // The `!lastWasHyphen && length > 0` guard has two distinct reasons to be false: a hyphen
        // was just written (mid-string collapse, covered by the "Trailing Spaces" case above), or
        // no character has been written yet (a *leading* separator, exercised only here) -- without
        // the `length > 0` half of that guard this would slugify to "-sprint" instead of "sprint".
        var parsed = RoomId.TryParse("!!!Sprint", out var roomId);

        Assert.True(parsed);
        Assert.Equal("sprint", roomId.Value);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        RoomId.TryParse("Sprint Planning", out var roomId);

        Assert.Equal(roomId.Value, roomId.ToString());
    }
}
