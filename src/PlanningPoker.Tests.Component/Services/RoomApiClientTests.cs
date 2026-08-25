using System.Net;
using PlanningPoker.Client.Services;
using PlanningPoker.Contracts;
using PlanningPoker.Tests.Component.TestSupport;
using Xunit;

namespace PlanningPoker.Tests.Component.Services;

public class RoomApiClientTests
{
    private static RoomApiClient NewClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") });

    [Fact]
    public async Task GetRoomNameSuggestionAsync_ReturnsTheSuggestedName()
    {
        var client = NewClient(new StubHttpMessageHandler(HttpStatusCode.OK, """{"name":"brave-falcon"}"""));

        var name = await client.GetRoomNameSuggestionAsync();

        Assert.Equal("brave-falcon", name);
    }

    [Fact]
    public async Task GetRoomNameSuggestionAsync_ReturnsEmptyString_WhenResponseBodyIsNull()
    {
        var client = NewClient(new StubHttpMessageHandler(HttpStatusCode.OK, "null"));

        var name = await client.GetRoomNameSuggestionAsync();

        Assert.Equal(string.Empty, name);
    }

    [Fact]
    public async Task GetDecksAsync_ReturnsTheDecks()
    {
        var client = NewClient(new StubHttpMessageHandler(HttpStatusCode.OK,
            """[{"deckType":"Fibonacci","displayName":"Fibonacci","cards":[]}]"""));

        var decks = await client.GetDecksAsync();

        Assert.Single(decks);
    }

    [Fact]
    public async Task GetDecksAsync_ReturnsEmptyList_WhenResponseBodyIsNull()
    {
        var client = NewClient(new StubHttpMessageHandler(HttpStatusCode.OK, "null"));

        var decks = await client.GetDecksAsync();

        Assert.Empty(decks);
    }

    [Fact]
    public async Task GetRandomAvatarsAsync_RequestsTheDefaultCount_WhenNoneIsGiven()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = NewClient(handler);

        await client.GetRandomAvatarsAsync();

        Assert.Equal("https://example.test/api/avatars/random?count=3", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetRandomAvatarsAsync_RequestsAnExplicitCount()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = NewClient(handler);

        await client.GetRandomAvatarsAsync(5);

        Assert.Equal("https://example.test/api/avatars/random?count=5", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetRandomAvatarsAsync_ReturnsEmptyList_WhenResponseBodyIsNull()
    {
        var client = NewClient(new StubHttpMessageHandler(HttpStatusCode.OK, "null"));

        var avatars = await client.GetRandomAvatarsAsync();

        Assert.Empty(avatars);
    }

    [Fact]
    public async Task CreateRoomAsync_ReturnsTheCreatedRoom_OnSuccess()
    {
        var client = NewClient(new StubHttpMessageHandler(HttpStatusCode.Created,
            """{"roomId":"sprint-planning","name":"Sprint Planning","deckType":"Fibonacci"}"""));

        var room = await client.CreateRoomAsync("Sprint Planning", DeckType.Fibonacci);

        Assert.NotNull(room);
        Assert.Equal("sprint-planning", room!.RoomId);
    }

    [Fact]
    public async Task CreateRoomAsync_ReturnsNull_OnFailureStatusCode()
    {
        var client = NewClient(new StubHttpMessageHandler(HttpStatusCode.Conflict));

        var room = await client.CreateRoomAsync("Sprint Planning", DeckType.Fibonacci);

        Assert.Null(room);
    }

    [Fact]
    public async Task GetRoomAsync_ReturnsTheRoom_OnSuccess()
    {
        var client = NewClient(new StubHttpMessageHandler(HttpStatusCode.OK,
            """{"roomId":"sprint-planning","name":"Sprint Planning","deckType":"Fibonacci"}"""));

        var room = await client.GetRoomAsync("sprint-planning");

        Assert.NotNull(room);
    }

    [Fact]
    public async Task GetRoomAsync_ReturnsNull_WhenNotFound()
    {
        var client = NewClient(new StubHttpMessageHandler(HttpStatusCode.NotFound));

        var room = await client.GetRoomAsync("does-not-exist");

        Assert.Null(room);
    }
}
