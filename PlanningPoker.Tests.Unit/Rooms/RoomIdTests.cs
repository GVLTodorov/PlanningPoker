using PlanningPoker.Domain.Rooms;
using Xunit;

namespace PlanningPoker.Tests.Unit.Rooms;

public class RoomIdTests
{
    [Fact]
    public void New_ProducesUniqueIds()
    {
        var a = RoomId.New();
        var b = RoomId.New();

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void New_RoundTripsThroughTryParse()
    {
        var original = RoomId.New();

        var parsed = RoomId.TryParse(original.Value, out var roomId);

        Assert.True(parsed);
        Assert.Equal(original, roomId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("toolongforaroomid")]
    [InlineData("O0O0O0O0")]
    public void TryParse_RejectsInvalidInput(string? input)
    {
        var parsed = RoomId.TryParse(input, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void TryParse_IsCaseInsensitive()
    {
        var original = RoomId.New();

        var parsed = RoomId.TryParse(original.Value.ToLowerInvariant(), out var roomId);

        Assert.True(parsed);
        Assert.Equal(original, roomId);
    }
}
