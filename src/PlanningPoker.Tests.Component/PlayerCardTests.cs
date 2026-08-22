using Bunit;
using PlanningPoker.Client.Components;
using PlanningPoker.Contracts;
using Xunit;

namespace PlanningPoker.Tests.Component;

public class PlayerCardTests : BunitContext
{
    [Fact]
    public void RendersBackgroundLayer_SeparateFromNameText_WhenAvatarPresent()
    {
        var player = new PlayerDto(Guid.NewGuid(), "Alice", false, false, "https://example.test/a.gif", false, null);

        var cut = Render<PlayerCard>(p => p.Add(x => x.Player, player));

        // The background image must live in its own layer, not be applied as opacity on the
        // whole card -- otherwise the name text would fade along with the decorative image.
        var background = cut.Find(".player-card-background");
        Assert.Contains("url('https://example.test/a.gif')", background.GetAttribute("style"));

        var name = cut.Find(".player-card-name");
        Assert.Equal("Alice", name.TextContent);
        Assert.Empty(name.QuerySelectorAll(".player-card-background"));
    }

    [Fact]
    public void OmitsBackgroundLayer_WhenNoAvatar()
    {
        var player = new PlayerDto(Guid.NewGuid(), "Bob", false, false, null, false, null);

        var cut = Render<PlayerCard>(p => p.Add(x => x.Player, player));

        Assert.Empty(cut.FindAll(".player-card-background"));
    }

    [Fact]
    public void HidesCardValue_BeforeReveal_ButShowsPickedIndicator()
    {
        var player = new PlayerDto(Guid.NewGuid(), "Alice", false, false, null, true, null);

        var cut = Render<PlayerCard>(p => p.Add(x => x.Player, player));

        Assert.Empty(cut.FindAll(".player-card-value"));
        Assert.Single(cut.FindAll(".player-card-picked"));
    }

    [Fact]
    public void ShowsCardValue_AfterReveal()
    {
        var player = new PlayerDto(Guid.NewGuid(), "Alice", false, false, null, true, new CardOptionDto(2, 2, "2"));

        var cut = Render<PlayerCard>(p => p.Add(x => x.Player, player));

        Assert.Equal("2", cut.Find(".player-card-value").TextContent);
    }

    [Fact]
    public void ShowsSpectatorBadge_ForSpectators()
    {
        var player = new PlayerDto(Guid.NewGuid(), "Carol", true, false, null, false, null);

        var cut = Render<PlayerCard>(p => p.Add(x => x.Player, player));

        Assert.Equal("Spectator", cut.Find(".player-card-badge").TextContent);
    }

    [Fact]
    public void OmitsRemoveButton_WhenCanRemoveIsFalse()
    {
        var player = new PlayerDto(Guid.NewGuid(), "Alice", false, false, null, false, null);

        var cut = Render<PlayerCard>(p => p
            .Add(x => x.Player, player)
            .Add(x => x.CanRemove, false));

        Assert.Empty(cut.FindAll(".player-card-remove"));
    }

    [Fact]
    public void ClickingRemove_InvokesOnRemove_WhenConfirmed()
    {
        JSInterop.SetupModule("./js/interop.js").Setup<bool>("confirmAction", _ => true).SetResult(true);
        var player = new PlayerDto(Guid.NewGuid(), "Alice", false, false, null, false, null);
        var removed = false;

        var cut = Render<PlayerCard>(p => p
            .Add(x => x.Player, player)
            .Add(x => x.CanRemove, true)
            .Add(x => x.OnRemove, () => removed = true));

        cut.Find(".player-card-remove").Click();

        Assert.True(removed);
    }

    [Fact]
    public void ClickingRemove_DoesNotInvokeOnRemove_WhenNotConfirmed()
    {
        JSInterop.SetupModule("./js/interop.js").Setup<bool>("confirmAction", _ => true).SetResult(false);
        var player = new PlayerDto(Guid.NewGuid(), "Alice", false, false, null, false, null);
        var removed = false;

        var cut = Render<PlayerCard>(p => p
            .Add(x => x.Player, player)
            .Add(x => x.CanRemove, true)
            .Add(x => x.OnRemove, () => removed = true));

        cut.Find(".player-card-remove").Click();

        Assert.False(removed);
    }
}
