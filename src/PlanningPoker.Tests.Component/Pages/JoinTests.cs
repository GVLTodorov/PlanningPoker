using System.Net;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Client.Pages;
using PlanningPoker.Client.Services;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Serialization;
using PlanningPoker.Tests.Component.TestSupport;
using Xunit;

namespace PlanningPoker.Tests.Component.Pages;

public class JoinTests : BunitContext
{
    private const string RoomId = "sprint-planning";

    private static readonly JsonSerializerOptions JsonOptions = PlanningPokerJsonContext.CreateOptions();

    public JoinTests()
    {
        var jsModule = JSInterop.SetupModule("./js/interop.js");
        jsModule.SetupVoid("saveSessionItem", _ => true).SetVoidResult();
        jsModule.SetupVoid("focusElement", _ => true).SetVoidResult();
    }

    private void SetUpRoomApi(RoomSummaryResponse? room, IReadOnlyList<string>? avatars = null)
    {
        avatars ??= ["https://example.test/1.gif"];
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return path switch
            {
                $"/api/rooms/{RoomId}" => room is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : Json(room),
                "/api/avatars/random" => Json(avatars),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        });
        Services.AddSingleton(new RoomApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
        Services.AddSingleton(new PlayerSessionState(JSInterop.JSRuntime));
    }

    private static HttpResponseMessage Json<T>(T body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json") };

    [Fact]
    public void ShowsRoomNotFound_WhenTheRoomDoesNotExist()
    {
        SetUpRoomApi(room: null);

        var cut = Render<Join>(p => p.Add(x => x.RoomId, RoomId));

        Assert.Contains("Room not found", cut.Markup);
    }

    [Fact]
    public void ShowsTheJoinForm_WhenTheRoomExists()
    {
        SetUpRoomApi(new RoomSummaryResponse(RoomId, "Sprint Planning", DeckType.Fibonacci));

        var cut = Render<Join>(p => p.Add(x => x.RoomId, RoomId));

        Assert.Contains("Join \"Sprint Planning\"", cut.Markup);
    }

    [Fact]
    public void JoinRoom_ShowsError_WhenNameIsBlank()
    {
        SetUpRoomApi(new RoomSummaryResponse(RoomId, "Sprint Planning", DeckType.Fibonacci));
        var cut = Render<Join>(p => p.Add(x => x.RoomId, RoomId));

        cut.Find("form").Submit();

        Assert.Contains("enter your name", cut.Markup);
    }

    [Fact]
    public void JoinRoom_SavesSessionAndNavigates_OnSuccess()
    {
        SetUpRoomApi(new RoomSummaryResponse(RoomId, "Sprint Planning", DeckType.Fibonacci));
        var cut = Render<Join>(p => p.Add(x => x.RoomId, RoomId));
        cut.Find("input.text-input").Input("Bob");

        cut.Find("form").Submit();

        Assert.EndsWith($"/{RoomId}", Services.GetRequiredService<NavigationManager>().Uri);
    }
}
