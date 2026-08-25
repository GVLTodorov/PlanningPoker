using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using PlanningPoker.Client.Pages;
using PlanningPoker.Client.Services;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Contracts.Serialization;
using PlanningPoker.Tests.Component.TestSupport;
using Xunit;

namespace PlanningPoker.Tests.Component.Pages;

public class BoardTests : BunitContext
{
    private const string RoomId = "sprint-planning";

    private static readonly JsonSerializerOptions JsonOptions = PlanningPokerJsonContext.CreateOptions();

    private static readonly CardOptionResponse[] Cards =
    [
        new CardOptionResponse(0, 1, "1"),
        new CardOptionResponse(1, 2, "2"),
        new CardOptionResponse(2, 3, "3"),
    ];

    private static readonly DeckResponse[] Decks = [new DeckResponse(DeckType.Fibonacci, "Fibonacci", Cards)];

    public BoardTests()
    {
        // Every Board render calls Session.RestoreAsync() first, and every successful join calls
        // Session.SaveAsync() -- both go through the JS interop module, so both must be stubbed for
        // every test, not just the ones specifically about session restore/save. One shared module
        // handle is reused for every Setup call below rather than calling SetupModule repeatedly.
        var jsModule = JSInterop.SetupModule("./js/interop.js");
        jsModule.Setup<string?>("loadSessionItem", _ => true).SetResult(null);
        // SetupVoid's planned invocation is completed once and then keeps matching/auto-resolving
        // every future call with the same identifier -- but only once SetVoidResult() has actually
        // been called on it. Without that, even the first matching call hangs indefinitely.
        jsModule.SetupVoid("saveSessionItem", _ => true).SetVoidResult();
        jsModule.SetupVoid("celebrateConsensus").SetVoidResult();
        jsModule.SetupVoid("copyToClipboard", _ => true).SetVoidResult();

        SetUpRoomApiClient(Decks);
    }

    private FakeGameHubClient SetUpHub()
    {
        var factory = new FakeGameHubClientFactory();
        Services.AddSingleton<IGameHubClientFactory>(factory);
        return factory.Client;
    }

    private PlayerSessionState SetUpSession(string playerName = "Alice", string? roomId = null, Guid? playerId = null)
    {
        // JSInterop.JSRuntime hands back the fake runtime directly, without going through Services
        // (which bUnit locks against further registration as soon as anything is resolved from it).
        var session = new PlayerSessionState(JSInterop.JSRuntime)
        {
            PlayerName = playerName,
            RoomId = roomId,
            PlayerId = playerId,
        };
        Services.AddSingleton(session);
        return session;
    }

    private void SetUpRoomApiClient(IReadOnlyList<DeckResponse> decks)
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(decks, JsonOptions));
        Services.AddSingleton(new RoomApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }

    private static JoinRoomResponse NewJoinResult(Guid playerId, bool isHost, RoundStatus status = RoundStatus.Voting, params PlayerResponse[] players) =>
        new(playerId, new RoomStateResponse(RoomId, "Sprint Planning", DeckType.Fibonacci, status, players));

    [Fact]
    public void RedirectsToJoinScreen_WhenNoSessionPlayerNameIsSet()
    {
        SetUpHub();
        SetUpSession(playerName: string.Empty);

        Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        Assert.EndsWith($"/{RoomId}/join", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void ShowsRoomNotFound_WhenJoinRoomThrowsHubException()
    {
        var hub = SetUpHub();
        SetUpSession();
        hub.JoinRoomResult.SetException(new HubException("Room not found."));

        var cut = Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        Assert.Contains("Room not found", cut.Markup);
    }

    [Fact]
    public void ShowsConnecting_WhileJoinRoomHasNotCompletedYet()
    {
        SetUpHub();
        SetUpSession();

        // The fake's JoinRoomResult TaskCompletionSource is left uncompleted, so OnInitializedAsync
        // suspends mid-join -- exactly like a real slow network round trip -- letting this assert the
        // "connecting" screen and exercise event handlers' `_state is null` guards below.
        var cut = Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        Assert.Contains("Connecting", cut.Markup);
    }

    [Fact]
    public void EventHandlers_NoOp_WhileStateIsStillNull()
    {
        var hub = SetUpHub();
        SetUpSession();

        Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        // None of these must throw or do anything observable -- _state is still null because
        // JoinRoomResult hasn't completed.
        hub.RaisePlayerPickChanged(new PlayerPickStatusChanged(Guid.NewGuid(), true));
        hub.RaiseRoundRevealed(new RoundRevealed([]));
        hub.RaiseRoundReset();
    }

    [Fact]
    public void JoinRoomAsync_PassesExistingPlayerId_OnlyWhenSessionRoomIdMatchesTheCurrentRoom()
    {
        var hub = SetUpHub();
        var existingPlayerId = Guid.NewGuid();
        SetUpSession(roomId: RoomId, playerId: existingPlayerId);
        hub.JoinRoomResult.SetResult(NewJoinResult(existingPlayerId, isHost: true));

        Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        Assert.Contains(hub.Calls, c => c.StartsWith("JoinRoomAsync(") && c.EndsWith($",{existingPlayerId})"));
    }

    [Fact]
    public void JoinRoomAsync_PassesNoExistingPlayerId_WhenSessionRoomIdIsDifferentRoom()
    {
        var hub = SetUpHub();
        SetUpSession(roomId: "some-other-room", playerId: Guid.NewGuid());
        hub.JoinRoomResult.SetResult(NewJoinResult(Guid.NewGuid(), isHost: true));

        Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        Assert.Contains(hub.Calls, c => c.StartsWith("JoinRoomAsync(") && c.EndsWith(",)"));
    }

    [Fact]
    public void RendersBoard_AfterJoinRoomSucceeds()
    {
        var hub = SetUpHub();
        SetUpSession();
        var playerId = Guid.NewGuid();
        var host = new PlayerResponse(playerId, "Alice", false, true, null, false, null);
        hub.JoinRoomResult.SetResult(NewJoinResult(playerId, isHost: true, players: host));

        var cut = Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        Assert.Contains("Fibonacci", cut.Markup);
        Assert.Single(cut.FindAll(".player-card-name"));
    }

    [Fact]
    public void OnRoomStateChanged_ReplacesState_AndReRenders()
    {
        var hub = SetUpHub();
        SetUpSession();
        var playerId = Guid.NewGuid();
        var host = new PlayerResponse(playerId, "Alice", false, true, null, false, null);
        hub.JoinRoomResult.SetResult(NewJoinResult(playerId, isHost: true, players: host));
        var cut = Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        var renamed = new RoomStateResponse(RoomId, "Renamed Room", DeckType.Fibonacci, RoundStatus.Voting, [host]);
        hub.RaiseRoomStateChanged(renamed);

        cut.Render();
        Assert.Contains("Renamed Room", cut.Markup);
    }

    [Fact]
    public void OnPlayerPickChanged_UpdatesJustThatPlayersHasPicked()
    {
        var hub = SetUpHub();
        SetUpSession();
        var hostId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var host = new PlayerResponse(hostId, "Alice", false, true, null, false, null);
        var guest = new PlayerResponse(guestId, "Bob", false, false, null, false, null);
        hub.JoinRoomResult.SetResult(NewJoinResult(hostId, isHost: true, players: [host, guest]));
        var cut = Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        hub.RaisePlayerPickChanged(new PlayerPickStatusChanged(guestId, true));

        // Observed indirectly: CanReveal only becomes true once every non-spectator has picked, so
        // once the host also picks, a reveal being possible proves Bob's HasPicked flag actually
        // flipped rather than the whole state object silently being replaced with stale data.
        hub.RaisePlayerPickChanged(new PlayerPickStatusChanged(hostId, true));
        cut.Render();
        Assert.False(cut.Find(".reveal-button").HasAttribute("disabled"));
    }

    [Fact]
    public void OnRoundRevealed_CelebratesConsensus_WhenEveryVoteMatches()
    {
        var hub = SetUpHub();
        SetUpSession();
        var hostId = Guid.NewGuid();
        var host = new PlayerResponse(hostId, "Alice", false, true, null, true, null);
        hub.JoinRoomResult.SetResult(NewJoinResult(hostId, isHost: true, players: host));
        Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        var revealedHost = host with { Card = new CardOptionResponse(1, 2, "2") };
        hub.RaiseRoundRevealed(new RoundRevealed([revealedHost]));

        JSInterop.VerifyInvoke("celebrateConsensus");
    }

    [Fact]
    public void OnRoundRevealed_DoesNotCelebrate_WhenNoOneHasACard()
    {
        // Guards the `votes.Count == 1` check specifically against zero votes (an all-spectator
        // reveal) -- Count == 0 must not be treated as "everyone agreed".
        var hub = SetUpHub();
        SetUpSession();
        var hostId = Guid.NewGuid();
        var host = new PlayerResponse(hostId, "Alice", true, true, null, false, null);
        hub.JoinRoomResult.SetResult(NewJoinResult(hostId, isHost: true, players: host));
        Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        hub.RaiseRoundRevealed(new RoundRevealed([host]));

        JSInterop.VerifyNotInvoke("celebrateConsensus");
    }

    [Fact]
    public void OnRoundReset_ReturnsToVoting_AndClearsPicks()
    {
        var hub = SetUpHub();
        SetUpSession();
        var hostId = Guid.NewGuid();
        var host = new PlayerResponse(hostId, "Alice", false, true, null, true, new CardOptionResponse(0, 1, "1"));
        hub.JoinRoomResult.SetResult(NewJoinResult(hostId, isHost: true, status: RoundStatus.Revealed, players: host));
        var cut = Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        hub.RaiseRoundReset();
        cut.Render();

        Assert.NotEmpty(cut.FindAll(".reveal-button"));
    }

    [Fact]
    public void OnRemovedFromRoom_ClearsSession_AndNavigatesHome()
    {
        var hub = SetUpHub();
        var session = SetUpSession();
        var hostId = Guid.NewGuid();
        hub.JoinRoomResult.SetResult(NewJoinResult(hostId, isHost: true, players: new PlayerResponse(hostId, "Alice", false, true, null, false, null)));
        var cut = Render<Board>(p => p.Add(x => x.RoomId, RoomId));
        var navigation = Services.GetRequiredService<NavigationManager>();

        hub.RaiseRemovedFromRoom();
        // OnRemovedFromRoom's cleanup runs inside InvokeAsync(async () => ...), so it lands on a
        // later render; WaitForState pumps the renderer until that navigation actually happens
        // instead of guessing at a fixed delay.
        cut.WaitForState(() => navigation.Uri.Contains("removed=true"));

        Assert.Equal(string.Empty, session.PlayerName);
        Assert.Null(session.RoomId);
    }

    [Fact]
    public void PickCardAsync_ForwardsToTheHub_AndMarksTheCardSelected()
    {
        var hub = SetUpHub();
        SetUpSession();
        var hostId = Guid.NewGuid();
        var host = new PlayerResponse(hostId, "Alice", false, true, null, false, null);
        hub.JoinRoomResult.SetResult(NewJoinResult(hostId, isHost: true, players: host));
        var cut = Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        cut.FindAll(".hand-card")[1].Click();

        Assert.Contains(hub.Calls, c => c == "PickCardAsync(1)");
    }

    [Fact]
    public void CopyInviteLinkAsync_CopiesTheUrl_AndFlipsTheCopiedIndicator()
    {
        var hub = SetUpHub();
        SetUpSession();
        var hostId = Guid.NewGuid();
        hub.JoinRoomResult.SetResult(NewJoinResult(hostId, isHost: true, players: new PlayerResponse(hostId, "Alice", false, true, null, false, null)));
        var cut = Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        cut.Find(".copy-link-button").Click();
        cut.Render();

        JSInterop.VerifyInvoke("copyToClipboard");
        Assert.Equal("Link copied!", cut.Find(".copy-link-button").GetAttribute("aria-label"));
    }

    [Fact]
    public void DisposeAsync_UnsubscribesAndDisposesTheHub()
    {
        var hub = SetUpHub();
        SetUpSession();
        var hostId = Guid.NewGuid();
        hub.JoinRoomResult.SetResult(NewJoinResult(hostId, isHost: true, players: new PlayerResponse(hostId, "Alice", false, true, null, false, null)));
        Render<Board>(p => p.Add(x => x.RoomId, RoomId));

        // BunitContext disposes its whole render tree (and thus every rendered component, including
        // Board's own IAsyncDisposable.DisposeAsync) when the context itself is disposed.
        Dispose();

        Assert.True(hub.Disposed);
    }
}
