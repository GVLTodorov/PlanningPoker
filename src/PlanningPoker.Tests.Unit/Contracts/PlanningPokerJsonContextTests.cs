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
}
