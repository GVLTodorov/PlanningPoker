using Bunit;
using PlanningPoker.Client.Components;
using PlanningPoker.Contracts;
using Xunit;

namespace PlanningPoker.Tests.Component;

public class VoteSummaryTests : BunitContext
{
    private static PlayerDto Voter(double value, string label, bool isSpectator = false) =>
        new(Guid.NewGuid(), "Player", isSpectator, false, null, true, new CardOptionDto(0, value, label));

    [Fact]
    public void RendersNothing_WhenNoVoters()
    {
        var cut = Render<VoteSummary>(p => p.Add(x => x.Players, []));

        Assert.Empty(cut.FindAll(".vote-summary"));
    }

    [Fact]
    public void GroupsVotesByValue_DescendingWithCounts()
    {
        var players = new List<PlayerDto>
        {
            Voter(3, "3"),
            Voter(3, "3"),
            Voter(8, "8"),
        };

        var cut = Render<VoteSummary>(p => p.Add(x => x.Players, players));

        var groups = cut.FindAll(".vote-summary-group");
        Assert.Equal(2, groups.Count);
        Assert.Equal("8", groups[0].QuerySelector(".vote-summary-card")!.TextContent);
        Assert.Equal("1 Vote", groups[0].QuerySelector(".vote-summary-count")!.TextContent);
        Assert.Equal("3", groups[1].QuerySelector(".vote-summary-card")!.TextContent);
        Assert.Equal("2 Votes", groups[1].QuerySelector(".vote-summary-count")!.TextContent);
    }

    [Fact]
    public void ExcludesSpectatorsAndUnpickedPlayers_FromCalculations()
    {
        var players = new List<PlayerDto>
        {
            Voter(2, "2"),
            Voter(4, "4", isSpectator: true),
            new(Guid.NewGuid(), "NoPick", false, false, null, false, null),
        };

        var cut = Render<VoteSummary>(p => p.Add(x => x.Players, players));

        Assert.Single(cut.FindAll(".vote-summary-group"));
        Assert.Equal("2", cut.Find(".vote-summary-stat-value").TextContent);
    }

    [Fact]
    public void ComputesAverage()
    {
        var players = new List<PlayerDto> { Voter(2, "2"), Voter(4, "4") };

        var cut = Render<VoteSummary>(p => p.Add(x => x.Players, players));

        Assert.Equal("3", cut.Find(".vote-summary-stat-value").TextContent);
    }

    [Fact]
    public void ShowsFullAgreement_AsHighClass_WhenUnanimous()
    {
        var players = new List<PlayerDto> { Voter(5, "5"), Voter(5, "5") };

        var cut = Render<VoteSummary>(p => p.Add(x => x.Players, players));

        var agreement = cut.FindAll(".vote-summary-stat-value")[1];
        Assert.Equal("100%", agreement.TextContent);
        Assert.Contains("vote-summary-agreement-high", agreement.ClassList);
    }

    [Fact]
    public void ShowsSplitAgreement_AsLowClass_WhenBelowHalf()
    {
        var players = new List<PlayerDto> { Voter(1, "1"), Voter(2, "2"), Voter(3, "3") };

        var cut = Render<VoteSummary>(p => p.Add(x => x.Players, players));

        var agreement = cut.FindAll(".vote-summary-stat-value")[1];
        Assert.Equal("33%", agreement.TextContent);
        Assert.Contains("vote-summary-agreement-low", agreement.ClassList);
    }
}
