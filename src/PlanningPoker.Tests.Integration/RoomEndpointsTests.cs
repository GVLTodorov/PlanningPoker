using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Api.Giphy;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Requests;
using PlanningPoker.Contracts.Serialization;
using PlanningPoker.Tests.Integration.TestSupport;
using Xunit;

namespace PlanningPoker.Tests.Integration;

/// <summary>Covers the REST surface in RoomEndpoints.cs that GameHubTests.cs never touches -- that
/// file only ever calls the happy path of POST /api/rooms before moving on to SignalR.</summary>
public class RoomEndpointsTests : IClassFixture<PlanningPokerWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = PlanningPokerJsonContext.CreateOptions();

    private readonly PlanningPokerWebApplicationFactory _factory;

    public RoomEndpointsTests(PlanningPokerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetNameSuggestion_ReturnsAName()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/rooms/name-suggestion");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RoomNameSuggestionResponse>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(body!.Name));
    }

    [Fact]
    public async Task GetDecks_ReturnsEveryDeckType()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/decks");

        response.EnsureSuccessStatusCode();
        var decks = await response.Content.ReadFromJsonAsync<List<DeckResponse>>(JsonOptions);
        Assert.Equal(Enum.GetValues<DeckType>().Length, decks!.Count);
    }

    [Fact]
    public async Task GetHealthz_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetRoom_ReturnsTheRoom_WhenItExists()
    {
        using var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Get Room Test", DeckType.Fibonacci), JsonOptions);
        var room = await createResponse.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/rooms/{room!.RoomId}");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetRoom_ReturnsNotFound_WhenTheRoomDoesNotExist()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/rooms/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_ReturnsNotFound_WhenTheIdHasNoUsableCharacters()
    {
        // A distinct short-circuit from the "well-formed but unknown id" case above -- this one never
        // even reaches the repository lookup because RoomId.TryParse itself rejects the input.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/rooms/!!!");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostRoom_ReturnsBadRequest_WhenNameIsBlank()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("   ", DeckType.Fibonacci), JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRoom_ReturnsConflict_WhenTheNameIsAlreadyTaken()
    {
        using var client = _factory.CreateClient();
        var first = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Duplicate Name Room", DeckType.Fibonacci), JsonOptions);
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest("Duplicate Name Room", DeckType.Fibonacci), JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetRandomAvatars_UsesTheDefaultCount_WhenNoneIsGiven()
    {
        var fakeGiphy = new FakeGiphyClient();
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IGiphyClient>(fakeGiphy)));
        using var client = factory.CreateClient();

        await client.GetAsync("/api/avatars/random");

        Assert.Equal(3, fakeGiphy.LastRequestedCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(11, 10)]
    [InlineData(999, 10)]
    [InlineData(5, 5)]
    public async Task GetRandomAvatars_ClampsCountToBetween1And10(int requestedCount, int expectedClampedCount)
    {
        var fakeGiphy = new FakeGiphyClient();
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<IGiphyClient>(fakeGiphy)));
        using var client = factory.CreateClient();

        await client.GetAsync($"/api/avatars/random?count={requestedCount}");

        Assert.Equal(expectedClampedCount, fakeGiphy.LastRequestedCount);
    }
}
