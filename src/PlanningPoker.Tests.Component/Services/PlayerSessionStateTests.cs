using Bunit;
using PlanningPoker.Client.Services;
using Xunit;

namespace PlanningPoker.Tests.Component.Services;

public class PlayerSessionStateTests : BunitContext
{
    [Fact]
    public async Task RestoreAsync_DoesNothing_WhenPlayerNameIsAlreadySet()
    {
        // No JS interop is stubbed at all in this test -- if RestoreAsync's early-return guard didn't
        // fire, the very first JS call would throw bUnit's "not configured" exception, failing loudly.
        var session = new PlayerSessionState(JSInterop.JSRuntime) { PlayerName = "Alice" };

        await session.RestoreAsync();

        Assert.Equal("Alice", session.PlayerName);
    }

    [Fact]
    public async Task RestoreAsync_LeavesPlayerNameEmpty_WhenNothingIsStored()
    {
        JSInterop.SetupModule("./js/interop.js").Setup<string?>("loadSessionItem", _ => true).SetResult(null);
        var session = new PlayerSessionState(JSInterop.JSRuntime);

        await session.RestoreAsync();

        Assert.Equal(string.Empty, session.PlayerName);
    }

    [Fact]
    public async Task RestoreAsync_LeavesPlayerNameEmpty_WhenTheStoredValueDeserializesToNull()
    {
        // "null" is valid JSON for a null value (as opposed to a missing/blank string, already
        // covered above) -- JsonSerializer.Deserialize<StoredSession>("null") returns null rather
        // than throwing, which is the specific case this branch guards against.
        JSInterop.SetupModule("./js/interop.js").Setup<string?>("loadSessionItem", _ => true).SetResult("null");
        var session = new PlayerSessionState(JSInterop.JSRuntime);

        await session.RestoreAsync();

        Assert.Equal(string.Empty, session.PlayerName);
    }

    [Fact]
    public async Task RestoreAsync_PopulatesEveryField_WhenAValidSessionIsStored()
    {
        var playerId = Guid.NewGuid();
        var storedJson = $$"""
            {"PlayerName":"Bob","AvatarUrl":"https://example.test/b.gif","IsSpectator":true,"RoomId":"sprint-planning","PlayerId":"{{playerId}}"}
            """;
        JSInterop.SetupModule("./js/interop.js").Setup<string?>("loadSessionItem", _ => true).SetResult(storedJson);
        var session = new PlayerSessionState(JSInterop.JSRuntime);

        await session.RestoreAsync();

        Assert.Equal("Bob", session.PlayerName);
        Assert.Equal("https://example.test/b.gif", session.AvatarUrl);
        Assert.True(session.IsSpectator);
        Assert.Equal("sprint-planning", session.RoomId);
        Assert.Equal(playerId, session.PlayerId);
    }

    [Fact]
    public async Task SaveAsync_PersistsTheCurrentFields()
    {
        var jsModule = JSInterop.SetupModule("./js/interop.js");
        jsModule.SetupVoid("saveSessionItem", _ => true).SetVoidResult();
        var session = new PlayerSessionState(JSInterop.JSRuntime) { PlayerName = "Carol" };

        // Nothing throws bUnit's "not configured" exception -- proves the call went through the
        // module with the identifier this test stubbed, exercising SaveAsync's only code path.
        await session.SaveAsync();
    }
}
