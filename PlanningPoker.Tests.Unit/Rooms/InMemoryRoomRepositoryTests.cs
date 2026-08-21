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
        var found = repository.TryGet(created.Id, out var room);

        Assert.True(found);
        Assert.Same(created, room);
    }

    [Fact]
    public void TryGet_ReturnsFalse_ForUnknownRoom()
    {
        var repository = new InMemoryRoomRepository();

        var found = repository.TryGet(RoomId.New(), out var room);

        Assert.False(found);
        Assert.Null(room);
    }

    [Fact]
    public void Remove_MakesRoomUnreachable()
    {
        var repository = new InMemoryRoomRepository();
        var created = repository.Create("Sprint Planning", DeckType.Fibonacci);

        repository.Remove(created.Id);

        Assert.False(repository.TryGet(created.Id, out _));
    }
}
