using Bunit;
using PlanningPoker.Client.Components;
using PlanningPoker.Contracts;
using Xunit;

namespace PlanningPoker.Tests.Component;

public class RevealResetButtonTests : BunitContext
{
    [Fact]
    public void ShowsRevealButton_Disabled_WhenHostVotingAndNotEveryoneHasPicked()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, false)
            .Add(x => x.IsHost, true));

        var button = cut.Find("button");

        Assert.Equal("Reveal", button.TextContent.Trim());
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void ShowsRevealButton_Enabled_WhenHostVotingAndEveryoneHasPicked()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, true)
            .Add(x => x.IsHost, true));

        var button = cut.Find("button");

        Assert.Equal("Reveal", button.TextContent.Trim());
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public void ShowsWaitingMessage_NotButton_WhenNotHostAndVoting()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, false)
            .Add(x => x.IsHost, false));

        Assert.Empty(cut.FindAll("button"));
        Assert.Contains("Waiting for the host", cut.Markup);
    }

    [Fact]
    public void ShowsResetButton_WhenHostAndRevealed()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Revealed)
            .Add(x => x.CanReveal, false)
            .Add(x => x.IsHost, true));

        var button = cut.Find("button");

        Assert.Equal("Reset", button.TextContent.Trim());
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public void ShowsWaitingMessage_NotButton_WhenNotHostAndRevealed()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Revealed)
            .Add(x => x.CanReveal, false)
            .Add(x => x.IsHost, false));

        Assert.Empty(cut.FindAll("button"));
        Assert.Contains("Waiting for the host", cut.Markup);
    }

    [Fact]
    public void ClickingReveal_InvokesOnReveal()
    {
        var revealed = false;
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, true)
            .Add(x => x.IsHost, true)
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
            .Add(x => x.IsHost, true)
            .Add(x => x.OnReset, () => wasReset = true));

        cut.Find("button").Click();

        Assert.True(wasReset);
    }

    [Fact]
    public void ReRendering_WithIdenticalParameters_LeavesMarkupUnchanged()
    {
        // Exercises ShouldRender()'s false path (every OR-clause false: not first render, and none
        // of Status/CanReveal/IsHost changed) -- re-rendering with the exact same values must not
        // flip the button back to "Reveal" or otherwise disturb the markup.
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Revealed)
            .Add(x => x.CanReveal, false)
            .Add(x => x.IsHost, true));
        var markupAfterFirstRender = cut.Markup;

        cut.Render(p => p
            .Add(x => x.Status, RoundStatus.Revealed)
            .Add(x => x.CanReveal, false)
            .Add(x => x.IsHost, true));

        Assert.Equal(markupAfterFirstRender, cut.Markup);
    }

    [Fact]
    public void ReRendering_WithOnlyStatusChanged_SwitchesFromRevealToReset()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, true)
            .Add(x => x.IsHost, true));

        cut.Render(p => p
            .Add(x => x.Status, RoundStatus.Revealed)
            .Add(x => x.CanReveal, true)
            .Add(x => x.IsHost, true));

        Assert.Equal("Reset", cut.Find("button").TextContent.Trim());
    }

    [Fact]
    public void ReRendering_WithOnlyCanRevealChanged_TogglesTheDisabledAttribute()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, false)
            .Add(x => x.IsHost, true));

        cut.Render(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, true)
            .Add(x => x.IsHost, true));

        Assert.False(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void ReRendering_WithOnlyIsHostChanged_SwapsButtonForWaitingMessage()
    {
        var cut = Render<RevealResetButton>(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, true)
            .Add(x => x.IsHost, true));

        cut.Render(p => p
            .Add(x => x.Status, RoundStatus.Voting)
            .Add(x => x.CanReveal, true)
            .Add(x => x.IsHost, false));

        Assert.Empty(cut.FindAll("button"));
        Assert.Contains("Waiting for the host", cut.Markup);
    }
}
