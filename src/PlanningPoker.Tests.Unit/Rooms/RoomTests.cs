using PlanningPoker.Domain.Decks;
using PlanningPoker.Domain.Rooms;
using Xunit;

namespace PlanningPoker.Tests.Unit.Rooms;

public class RoomTests
{
    private static Room NewRoom(DeckType deckType = DeckType.Fibonacci)
    {
        RoomId.TryParse("Sprint Planning", out var id);
        return new Room(id, "Sprint Planning", deckType);
    }

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
    public void AddPlayer_WithExistingPlayerId_ReusesTheSamePlayer_PreservingHostStatus()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        room.AddPlayer("Bob", isSpectator: false, avatarUrl: null);

        // Simulates Alice (the host) refreshing while her old connection hasn't been swept yet --
        // the room is not empty (Bob is still there), so without identity reuse this would come back
        // isHost: false, exactly the bug this rejoin path exists to prevent.
        var rejoined = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null, existingPlayerId: alice.Id);

        Assert.Equal(alice.Id, rejoined.Id);
        Assert.True(rejoined.IsHost);
        Assert.Equal(2, room.GetState().Count);
    }

    [Fact]
    public void AddPlayer_WithExistingPlayerId_PreservesPickedCard()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        room.PickCard(alice.Id, 2);

        var rejoined = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null, existingPlayerId: alice.Id);

        Assert.True(rejoined.HasPicked);
    }

    [Fact]
    public void AddPlayer_WithExistingPlayerId_UpdatesNameAvatarAndSpectatorFlag()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: "old.png");

        var rejoined = room.AddPlayer(
            "Alice B.", isSpectator: true, avatarUrl: "new.png", existingPlayerId: alice.Id);

        Assert.Equal("Alice B.", rejoined.Name);
        Assert.True(rejoined.IsSpectator);
        Assert.Equal("new.png", rejoined.AvatarUrl);
    }

    [Fact]
    public void AddPlayer_WithExistingPlayerId_ThatIsNoLongerInTheRoom_JoinsAsNewPlayer()
    {
        var room = NewRoom();
        var goneId = Guid.NewGuid();

        // Grace period already elapsed and the old player was swept -- this id means nothing anymore,
        // so it must behave exactly like a fresh join (including normal first-joiner-is-host rules).
        var player = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null, existingPlayerId: goneId);

        Assert.Equal(goneId, player.Id);
        Assert.True(player.IsHost);
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

        Assert.Throws<InvalidOperationException>(() => room.PickCard(alice.Id, 0));
    }

    [Fact]
    public void PickCard_Throws_WhenCardIndexIsOutOfRange()
    {
        var room = NewRoom(DeckType.TrustVote);
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);

        Assert.Throws<ArgumentOutOfRangeException>(() => room.PickCard(alice.Id, 999));
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

        Assert.Throws<KeyNotFoundException>(() => room.PickCard(Guid.NewGuid(), 0));
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

        Assert.Throws<InvalidOperationException>(() => room.Reveal(alice.Id));
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

        Assert.Throws<UnauthorizedAccessException>(() => room.Reveal(bob.Id));
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

        room.Reset(alice.Id);

        Assert.Equal(RoundStatus.Voting, room.Status);
        Assert.False(room.GetState().Single().HasPicked);
    }

    [Fact]
    public void Reset_Throws_WhenCallerIsNotHost()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        var bob = room.AddPlayer("Bob", isSpectator: false, avatarUrl: null);
        room.PickCard(alice.Id, 0);
        room.PickCard(bob.Id, 0);
        room.Reveal(alice.Id);

        Assert.Throws<UnauthorizedAccessException>(() => room.Reset(bob.Id));
        Assert.Equal(RoundStatus.Revealed, room.Status);
    }

    [Fact]
    public void RemovePlayerAsHost_RemovesTargetPlayer()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        var bob = room.AddPlayer("Bob", isSpectator: false, avatarUrl: null);

        room.RemovePlayerAsHost(alice.Id, bob.Id);

        Assert.Single(room.GetState());
        Assert.Throws<KeyNotFoundException>(() => room.GetPlayer(bob.Id));
    }

    [Fact]
    public void RemovePlayerAsHost_Throws_WhenCallerIsNotHost()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);
        var bob = room.AddPlayer("Bob", isSpectator: false, avatarUrl: null);
        var carol = room.AddPlayer("Carol", isSpectator: false, avatarUrl: null);

        Assert.Throws<UnauthorizedAccessException>(() => room.RemovePlayerAsHost(bob.Id, carol.Id));
        Assert.Equal(3, room.GetState().Count);
    }

    [Fact]
    public void RemovePlayerAsHost_Throws_WhenTargetNotInRoom()
    {
        var room = NewRoom();
        var alice = room.AddPlayer("Alice", isSpectator: false, avatarUrl: null);

        Assert.Throws<KeyNotFoundException>(() => room.RemovePlayerAsHost(alice.Id, Guid.NewGuid()));
    }

    [Fact]
    public void Rename_UpdatesName()
    {
        var room = NewRoom();

        room.Rename("Retro");

        Assert.Equal("Retro", room.Name);
    }
}
