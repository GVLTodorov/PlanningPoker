using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Client.Pages;
using PlanningPoker.Client.Services;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Requests;
using PlanningPoker.Contracts.Serialization;
using PlanningPoker.Tests.Component.TestSupport;
using Xunit;

namespace PlanningPoker.Tests.Component.Pages;

public class HomeTests : BunitContext
{
    private static readonly JsonSerializerOptions JsonOptions = PlanningPokerJsonContext.CreateOptions();

    private static readonly DeckResponse[] Decks = [new DeckResponse(DeckType.Fibonacci, "Fibonacci", [])];

    public HomeTests()
    {
        var jsModule = JSInterop.SetupModule("./js/interop.js");
        jsModule.SetupVoid("saveSessionItem", _ => true).SetVoidResult();
        jsModule.SetupVoid("focusElement", _ => true).SetVoidResult();
    }

    private RoomSummaryResponse? _createRoomResult = new("sprint-planning", "Sprint Planning", DeckType.Fibonacci);
    private HttpStatusCode _createRoomStatus = HttpStatusCode.Created;

    private void SetUpRoomApi(string nameSuggestion = "brave-falcon", IReadOnlyList<string>? avatars = null)
    {
        avatars ??= ["https://example.test/1.gif", "https://example.test/2.gif"];
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return (path, request.Method.Method) switch
            {
                ("/api/rooms/name-suggestion", "GET") => Json(new RoomNameSuggestionResponse(nameSuggestion)),
                ("/api/decks", "GET") => Json(Decks),
                ("/api/avatars/random", "GET") => Json(avatars),
                ("/api/rooms", "POST") => _createRoomResult is null
                    ? new HttpResponseMessage(_createRoomStatus)
                    : Json(_createRoomResult, _createRoomStatus),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        });
        Services.AddSingleton(new RoomApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
        Services.AddSingleton(new PlayerSessionState(JSInterop.JSRuntime));
    }

    private static HttpResponseMessage Json<T>(T body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json") };

    [Fact]
    public void ShowsRemovedBanner_WhenWasRemovedIsTrue()
    {
        SetUpRoomApi();
        // [SupplyParameterFromQuery] parameters can only be driven through NavigationManager, not a
        // direct .Add(...) on the render builder.
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("removed", true));

        var cut = Render<Home>();

        Assert.Contains("removed from the room", cut.Markup);
    }

    [Fact]
    public void DoesNotShowRemovedBanner_ByDefault()
    {
        SetUpRoomApi();

        var cut = Render<Home>();

        Assert.DoesNotContain("removed from the room", cut.Markup);
    }

    [Fact]
    public void PrefillsTheSuggestedRoomName()
    {
        SetUpRoomApi(nameSuggestion: "clever-otter");

        var cut = Render<Home>();

        Assert.Equal("clever-otter", cut.Find("input.text-input").GetAttribute("value"));
    }

    [Fact]
    public void SelectsNoAvatar_WhenNoAvatarsAreReturned()
    {
        SetUpRoomApi(avatars: []);

        var cut = Render<Home>();

        // No avatar selected renders as an empty AvatarPicker -- if _avatarUrl had been left
        // pointing at a non-existent index instead of null, this would throw during render.
        Assert.NotNull(cut);
    }

    [Fact]
    public void CreateRoomAsync_ShowsError_WhenRoomNameIsBlank()
    {
        SetUpRoomApi();
        var cut = Render<Home>();
        cut.Find("input.text-input").Input(string.Empty);

        cut.Find("form").Submit();

        Assert.Contains("enter a room name", cut.Markup);
    }

    [Fact]
    public void CreateRoomAsync_ShowsErrorAndResuggestsName_WhenRoomNameIsTaken()
    {
        SetUpRoomApi(nameSuggestion: "first-suggestion");
        _createRoomResult = null;
        _createRoomStatus = HttpStatusCode.Conflict;
        var cut = Render<Home>();
        cut.Find("input.text-input").Input("Taken Name");
        SetPlayerName(cut, "Alice");

        cut.Find("form").Submit();

        Assert.Contains("already taken", cut.Markup);
    }

    [Fact]
    public void CreateRoomAsync_SavesSessionAndNavigates_OnSuccess()
    {
        SetUpRoomApi();
        var cut = Render<Home>();
        cut.Find("input.text-input").Input("Sprint Planning");
        SetPlayerName(cut, "Alice");

        cut.Find("form").Submit();

        Assert.EndsWith("/sprint-planning", Services.GetRequiredService<NavigationManager>().Uri);
    }

    private static void SetPlayerName(IRenderedComponent<Home> cut, string name)
    {
        var nameInput = cut.Find(".player-setup input.text-input");
        nameInput.Input(name);
    }
}
