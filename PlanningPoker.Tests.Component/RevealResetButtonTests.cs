using Bunit;
using PlanningPoker.Client.Components;
using PlanningPoker.Contracts;
using Xunit;

namespace PlanningPoker.Tests.Component;

public class RevealResetButtonTests : BunitContext
{
    [Fact]
    public void ShowsRevealButton_Disabled_WhenVotingAndNotEveryoneHasPicked()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, false));

        var button = cut.Find("button");

        Assert.Equal("Reveal", button.TextContent.Trim());
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void ShowsRevealButton_Enabled_WhenVotingAndEveryoneHasPicked()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, true));

        var button = cut.Find("button");

        Assert.Equal("Reveal", button.TextContent.Trim());
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public void ShowsResetButton_WhenRevealed()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Revealed)
            .Add(x => x.CanReveal, false));

        var button = cut.Find("button");

        Assert.Equal("Reset", button.TextContent.Trim());
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public void ClickingReveal_InvokesOnReveal()
    {
        var revealed = false;
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, true)
            .Add(x => x.OnReveal, () => revealed = true));

        cut.Find("button").Click();

        Assert.True(revealed);
    }

    [Fact]
    public void ClickingReset_InvokesOnReset()
    {
        var wasReset = false;
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Revealed)
            .Add(x => x.OnReset, () => wasReset = true));

        cut.Find("button").Click();

        Assert.True(wasReset);
    }
}
