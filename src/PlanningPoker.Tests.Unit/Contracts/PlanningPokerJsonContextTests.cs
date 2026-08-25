using System.Text.Json;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Contracts.Requests;
using PlanningPoker.Contracts.Serialization;
using Xunit;

namespace PlanningPoker.Tests.Unit.Contracts;

public class PlanningPokerJsonContextTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = PlanningPokerJsonContext.Default,
    };

    [Fact]
    public void RoomStateResponse_RoundTrips()
    {
        var original = new RoomStateResponse(
            "ABCD1234",
            "Sprint Planning",
            DeckType.Fibonacci,
            RoundStatus.Revealed,
            [
                new PlayerResponse(Guid.NewGuid(), "Alice", false, true, "https://example.test/a.gif", true, new CardOptionResponse(2, 2, "2")),
                new PlayerResponse(Guid.NewGuid(), "Bob", true, false, null, false, null),
            ]);

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<RoomStateResponse>(json, Options);

        // Record equality doesn't do sequence equality on IReadOnlyList properties, so round-trip
        // fidelity is asserted by re-serializing and comparing JSON rather than object equality.
        Assert.Equal(json, JsonSerializer.Serialize(roundTripped, Options));
    }

    [Fact]
    public void CreateRoomRequest_RoundTrips()
    {
        var original = new CreateRoomRequest("Retro", DeckType.TShirts);

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<CreateRoomRequest>(json, Options);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void JoinRoomResponse_RoundTrips()
    {
        var original = new JoinRoomResponse(
            Guid.NewGuid(),
            new RoomStateResponse("ABCD1234", "Sprint Planning", DeckType.Powers, RoundStatus.Voting, []));

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<JoinRoomResponse>(json, Options);

        Assert.Equal(json, JsonSerializer.Serialize(roundTripped, Options));
    }

    [Fact]
    public void PlayerPickStatusChanged_RoundTrips()
    {
        var original = new PlayerPickStatusChanged(Guid.NewGuid(), true);

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<PlayerPickStatusChanged>(json, Options);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void RoundRevealed_RoundTrips()
    {
        var original = new RoundRevealed(
            [new PlayerResponse(Guid.NewGuid(), "Alice", false, true, null, true, new CardOptionResponse(0, 0, "0"))]);

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<RoundRevealed>(json, Options);

        Assert.Equal(json, JsonSerializer.Serialize(roundTripped, Options));
    }

    [Fact]
    public void DeckType_SerializesAsString_NotNumber()
    {
        // Guards against an accidental switch away from string enum output, which would break
        // forward-compatible deck additions and make the wire payload unreadable when debugging.
        var request = new CreateRoomRequest("Retro", DeckType.ModifiedFibonacci);

        var json = JsonSerializer.Serialize(request, Options);

        Assert.Contains("\"ModifiedFibonacci\"", json);
    }

    [Fact]
    public void CardOptionResponse_RoundTrips_Standalone()
    {
        // Only ever exercised nested inside PlayerResponse/RoomStateResponse above -- this asserts
        // the source-generated (de)serializer works for the type on its own too.
        var original = new CardOptionResponse(3, 3, "3");

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<CardOptionResponse>(json, Options);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void DeckResponse_RoundTrips()
    {
        var original = new DeckResponse(DeckType.Fibonacci, "Fibonacci", [new CardOptionResponse(0, 0, "0"), new CardOptionResponse(1, 1, "1")]);

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<DeckResponse>(json, Options);

        Assert.Equal(json, JsonSerializer.Serialize(roundTripped, Options));
    }

    [Fact]
    public void DeckResponseList_RoundTrips()
    {
        IReadOnlyList<DeckResponse> original =
        [
            new DeckResponse(DeckType.Fibonacci, "Fibonacci", []),
            new DeckResponse(DeckType.TShirts, "T-Shirts", []),
        ];

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<IReadOnlyList<DeckResponse>>(json, Options);

        Assert.Equal(2, roundTripped!.Count);
    }

    [Fact]
    public void RoomNameSuggestionResponse_RoundTrips()
    {
        var original = new RoomNameSuggestionResponse("brave-falcon");

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<RoomNameSuggestionResponse>(json, Options);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void RoomSummaryResponse_RoundTrips()
    {
        var original = new RoomSummaryResponse("sprint-planning", "Sprint Planning", DeckType.Powers);

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<RoomSummaryResponse>(json, Options);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void StringList_RoundTrips()
    {
        var original = new List<string> { "https://example.test/a.gif", "https://example.test/b.gif" };

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<List<string>>(json, Options);

        Assert.Equal(original, roundTripped);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(5)]
    public void NullableInt_RoundTrips(int? original)
    {
        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<int?>(json, Options);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void NullableGuid_RoundTrips_WhenNull()
    {
        Guid? original = null;

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<Guid?>(json, Options);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void NullableGuid_RoundTrips_WhenPresent()
    {
        Guid? original = Guid.NewGuid();

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<Guid?>(json, Options);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void CreateOptions_ProducesOptionsThatRoundTripAKnownType()
    {
        // The existing tests above build JsonSerializerOptions by hand (TypeInfoResolver = Default);
        // this directly exercises CreateOptions() itself, including the reflection-fallback resolver
        // it appends, which is what the real hub/REST pipeline actually uses (see GameHubTests.cs).
        var options = PlanningPokerJsonContext.CreateOptions();
        var original = new CreateRoomRequest("Retro", DeckType.Powers);

        var json = JsonSerializer.Serialize(original, options);
        var roundTripped = JsonSerializer.Deserialize<CreateRoomRequest>(json, options);

        Assert.Equal(original, roundTripped);
    }
}
