using PlanningPoker.Domain.Decks;
using PlanningPoker.Domain.Rooms;
using Xunit;

namespace PlanningPoker.Tests.Unit.Rooms;

public class InMemoryRoomRepositoryTests
{
    [Fact]
    public void Create_ThenTryGet_ReturnsSameRoom()
    {
        var repository = new InMemoryRoomRepository();

        var created = repository.Create("Sprint Planning", DeckType.Fibonacci);

        Assert.NotNull(created);
        Assert.Equal("sprint-planning", created.Id.Value);
        var found = repository.TryGet(created.Id, out var room);

        Assert.True(found);
        Assert.Same(created, room);
    }

    [Fact]
    public void Create_ReturnsNull_WhenRoomIdIsAlreadyTaken()
    {
        var repository = new InMemoryRoomRepository();
        repository.Create("Sprint Planning", DeckType.Fibonacci);

        var second = repository.Create("sprint planning", DeckType.Powers);

        Assert.Null(second);
    }

    [Fact]
    public void Create_ReturnsNull_WhenNameHasNoUsableCharacters()
    {
        var repository = new InMemoryRoomRepository();

        var created = repository.Create("!!!", DeckType.Fibonacci);

        Assert.Null(created);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownRoom()
    {
        var repository = new InMemoryRoomRepository();

        RoomId.TryParse("unknown-room", out var unknownId);
        var found = repository.TryGet(unknownId, out var room);

        Assert.False(found);
        Assert.Null(room);
    }

    [Fact]
    public void Remove_MakesRoomUnreachable()
    {
        var repository = new InMemoryRoomRepository();
        var created = repository.Create("Sprint Planning", DeckType.Fibonacci);
        Assert.NotNull(created);

        repository.Remove(created.Id);

        Assert.False(repository.TryGet(created.Id, out _));
    }
}
