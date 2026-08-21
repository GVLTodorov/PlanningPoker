using PlanningPoker.Domain.Decks;
using PlanningPoker.Domain.Errors;
using PlanningPoker.Domain.Rooms;
using Xunit;

namespace PlanningPoker.Tests.Unit.Rooms;

public class RoomTests
{
    private static Room NewRoom(DeckType deckType = DeckType.Fibonacci) =>
        new(RoomId.New(), "Sprint Planning", deckType);

    [Fact]
    public void AddPlayer_AddsPlayerToState()
    {
        var room = NewRoom();

        var player = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);

        var state = room.GetState();
        Assert.Single(state);
        Assert.Equal(player.Id, state[0].PlayerId);
        Assert.Equal("Alice", state[0].Name);
    }

    [Fact]
    public void RemovePlayer_ReturnsTrue_WhenRoomBecomesEmpty()
    {
        var room = NewRoom();
        var player = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);

        var isNowEmpty = room.RemovePlayer(player.Id);

        Assert.True(isNowEmpty);
        Assert.True(room.IsEmpty);
    }

    [Fact]
    public void RemovePlayer_ReturnsFalse_WhenOtherPlayersRemain()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        room.AddPlayer("Bob", isSpectator: false, avatarUrl: null);

        var isNowEmpty = room.RemovePlayer(alice.Id);

        Assert.False(isNowEmpty);
    }

    [Fact]
    public void AddPlayer_FirstPlayerIsHost()
    {
        var room = NewRoom();

        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);

        Assert.True(alice.IsHost);
    }

    [Fact]
    public void AddPlayer_SubsequentPlayersAreNotHost()
    {
        var room = NewRoom();
        room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);

        var bob = room.AddPlayer("Bob", isSpectator: false, avatarUrl: null);

        Assert.False(bob.IsHost);
    }

    [Fact]
    public void SetSpectator_ClearsHand_WhenBecomingSpectator()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        room.PickCard(alice.Id, 0);

        room.SetSpectator(alice.Id, true);

        Assert.False(room.GetState().Single().HasPicked);
    }

    [Fact]
    public void PickCard_Throws_WhenPlayerIsSpectator()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: true, avatarUrl: null);

        Assert.Throws<SpectatorCannotPlayException>(() => room.PickCard(alice.Id, 0));
    }

    [Fact]
    public void PickCard_Throws_WhenCardIndexIsOutOfRange()
    {
        var room = NewRoom(DeckType.TrustVote);
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);

        Assert.Throws<InvalidCardValueException>(() => room.PickCard(alice.Id, 999));
    }

    [Fact]
    public void PickCard_AllowsClearingPick_WithNullIndex()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        room.PickCard(alice.Id, 0);

        room.PickCard(alice.Id, null);

        Assert.False(room.GetState().Single().HasPicked);
    }

    [Fact]
    public void PickCard_Throws_WhenPlayerNotInRoom()
    {
        var room = NewRoom();

        Assert.Throws<PlayerNotInRoomException>(() => room.PickCard(Guid.NewGuid(), 0));
    }

    [Fact]
    public void HasAllNonSpectatorsPicked_IsFalse_WhenRoomHasOnlySpectators()
    {
        var room = NewRoom();
        room.AddPlayer("Alice", isSpectator: true, avatarUrl: null);
        room.AddPlayer("Bob", isSpectator: true, avatarUrl: null);

        Assert.False(room.HasAllNonSpectatorsPicked());
    }

    [Fact]
    public void HasAllNonSpectatorsPicked_IgnoresSpectators()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        room.AddPlayer("Bob", isSpectator: true, avatarUrl: null);
        room.PickCard(alice.Id, 0);

        Assert.True(room.HasAllNonSpectatorsPicked());
    }

    [Fact]
    public void Reveal_Throws_WhenNotEveryNonSpectatorHasPicked()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        room.AddPlayer("Bob", isSpectator: false, avatarUrl: null);
        room.PickCard(alice.Id, 0);

        Assert.Throws<RevealRequiresAllPlayersToPickException>(() => room.Reveal(alice.Id));
        Assert.Equal(RoundStatus.Voting, room.Status);
    }

    [Fact]
    public void Reveal_Throws_WhenCallerIsNotHost()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        var bob = room.AddPlayer("Bob", isSpectator: false, avatarUrl: null);
        room.PickCard(alice.Id, 0);
        room.PickCard(bob.Id, 0);

        Assert.Throws<OnlyHostCanRevealException>(() => room.Reveal(bob.Id));
        Assert.Equal(RoundStatus.Voting, room.Status);
    }

    [Fact]
    public void Reveal_Succeeds_AndExposesCardValues_WhenEveryoneHasPicked()
    {
        var room = NewRoom(DeckType.Fibonacci);
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        var bob = room.AddPlayer("Bob", isSpectator: false, avatarUrl: null);
        room.PickCard(alice.Id, 2);
        room.PickCard(bob.Id, 4);

        room.Reveal(alice.Id);

        Assert.Equal(RoundStatus.Revealed, room.Status);
        var state = room.GetState().ToDictionary(p => p.PlayerId);
        Assert.Equal(2, state[alice.Id].Card!.Value);
        Assert.Equal(5, state[bob.Id].Card!.Value);
    }

    [Fact]
    public void GetState_HidesCardValues_BeforeReveal()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        room.PickCard(alice.Id, 0);

        var state = room.GetState().Single();

        Assert.True(state.HasPicked);
        Assert.Null(state.Card);
    }

    [Fact]
    public void Reset_ClearsHandsAndReturnsToVoting()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        room.PickCard(alice.Id, 0);
        room.Reveal(alice.Id);

        room.Reset();

        Assert.Equal(RoundStatus.Voting, room.Status);
        Assert.False(room.GetState().Single().HasPicked);
    }

    [Fact]
    public void Rename_UpdatesName()
    {
        var room = NewRoom();

        room.Rename("Retro");

        Assert.Equal("Retro", room.Name);
    }
}
