using System.Text.RegularExpressions;
using PlanningPoker.Domain.Rooms;
using Xunit;

namespace PlanningPoker.Tests.Unit.Rooms;

public partial class RoomNameGeneratorTests
{
    [GeneratedRegex(@"^[a-z]+-[a-z0-9]{6}$")]
    private static partial Regex NamePattern();

    [Fact]
    public void Generate_MatchesWordDashShortIdShape()
    {
        var name = RoomNameGenerator.Generate();

        Assert.Matches(NamePattern(), name);
    }

    [Fact]
    public void Generate_ProducesVariedOutput()
    {
        var names = Enumerable.Range(0, 20).Select(_ => RoomNameGenerator.Generate()).ToHashSet();

        Assert.True(names.Count > 1);
    }
}
