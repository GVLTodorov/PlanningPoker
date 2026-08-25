using Bunit;
using PlanningPoker.Client.Components;
using PlanningPoker.Contracts;
using Xunit;

namespace PlanningPoker.Tests.Component.Components;

public class PlayerListTests : BunitContext
{
    private static PlayerResponse Player(string name) =>
        new(Guid.NewGuid(), name, false, false, null, false, null);

    [Fact]
    public void RendersOnePlayerCardPerPlayer()
    {
        var players = new[] { Player("Alice"), Player("Bob") };

        var cut = Render<PlayerList>(p => p.Add(x => x.Players, players));

        Assert.Equal(2, cut.FindAll(".player-card-name").Count);
    }

    [Fact]
    public void HostSeesRemoveButton_OnOtherPlayers()
    {
        var alice = Player("Alice");
        var bob = Player("Bob");

        var cut = Render<PlayerList>(p => p
            .Add(x => x.Players, [alice, bob])
            .Add(x => x.LocalPlayerId, alice.PlayerId)
            .Add(x => x.IsHost, true));

        Assert.Single(cut.FindAll(".player-card-remove"));
    }

    [Fact]
    public void HostDoesNotSeeRemoveButton_OnThemselves()
    {
        var alice = Player("Alice");

        var cut = Render<PlayerList>(p => p
            .Add(x => x.Players, [alice])
            .Add(x => x.LocalPlayerId, alice.PlayerId)
            .Add(x => x.IsHost, true));

        Assert.Empty(cut.FindAll(".player-card-remove"));
    }

    [Fact]
    public void NonHostNeverSeesRemoveButtons()
    {
        var alice = Player("Alice");
        var bob = Player("Bob");

        var cut = Render<PlayerList>(p => p
            .Add(x => x.Players, [alice, bob])
            .Add(x => x.LocalPlayerId, bob.PlayerId)
            .Add(x => x.IsHost, false));

        Assert.Empty(cut.FindAll(".player-card-remove"));
    }

    [Fact]
    public void RemovingAPlayer_InvokesCallbackWithThatPlayersId()
    {
        // PlayerCard's remove button routes through a JS confirm() dialog before invoking OnRemove.
        JSInterop.SetupModule("./js/interop.js").Setup<bool>("confirmAction", _ => true).SetResult(true);
        var alice = Player("Alice");
        var bob = Player("Bob");
        Guid removedId = Guid.Empty;

        var cut = Render<PlayerList>(p => p
            .Add(x => x.Players, [alice, bob])
            .Add(x => x.LocalPlayerId, alice.PlayerId)
            .Add(x => x.IsHost, true)
            .Add(x => x.OnRemovePlayer, id => removedId = id));

        cut.Find(".player-card-remove").Click();

        Assert.Equal(bob.PlayerId, removedId);
    }
}
