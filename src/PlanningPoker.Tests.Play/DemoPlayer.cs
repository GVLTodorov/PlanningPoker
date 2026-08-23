using Microsoft.Playwright;

namespace PlanningPoker.Tests.Play;

/// <summary>
/// Drives one simulated player's browser session through the real Home/Join/Board UI (never the
/// hub or REST API directly) so a recording of it looks exactly like what a real user would see.
/// Kept independent of the specific demo scenario in Program.cs so it can be reused for other
/// browser-driven scenarios later.
/// </summary>
public sealed class DemoPlayer(IPage page)
{
    public IPage Page { get; } = page;

    /// <summary>
    /// Creates a fresh room via the Home screen and joins it -- since the room is empty, this
    /// player becomes host (first joiner wins, enforced server-side in Room.AddPlayer).
    /// </summary>
    public async Task<string> CreateRoomAndJoinAsHostAsync(string baseUrl, string playerName, HashSet<string> usedAvatars)
    {
        await Page.GotoAsync(baseUrl);
        await Page.GetByLabel("Room name").WaitForAsync();

        await Page.GetByLabel("Your name").FillAsync(playerName);
        await PickUnusedAvatarAsync(usedAvatars);

        await Task.Delay(2_500); // lingers on the filled-in form so the recording shows the menus
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create Room" }).ClickAsync();
        await Page.Locator(".player-list").WaitForAsync();

        return RoomIdFromUrl();
    }

    /// <summary>Joins an existing room via the Join screen. Every subsequent joiner is a guest.</summary>
    public async Task JoinRoomAsync(string baseUrl, string roomId, string playerName, HashSet<string> usedAvatars)
    {
        await Page.GotoAsync($"{baseUrl}/{roomId}/join");
        await Page.GetByLabel("Your name").WaitForAsync();

        await Page.GetByLabel("Your name").FillAsync(playerName);
        await PickUnusedAvatarAsync(usedAvatars);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Join Room" }).ClickAsync();
        await Page.Locator(".player-list").WaitForAsync();
    }

    public async Task PickRandomCardAsync(Random rng)
    {
        var cards = Page.GetByRole(AriaRole.Group, new() { Name = "Pick your estimate" }).GetByRole(AriaRole.Button);
        var count = await cards.CountAsync();
        await cards.Nth(rng.Next(count)).ClickAsync();
    }

    public Task RevealAsync() =>
        Page.GetByRole(AriaRole.Button, new() { Name = "Reveal" }).ClickAsync();

    public Task ResetAsync() =>
        Page.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();

    /// <summary>
    /// The avatar picker offers a handful of gifs drawn randomly from a shared server-side Giphy
    /// cache, so two players can easily be offered the same one. Reads every offered avatar's
    /// image URL and clicks the first one no other player in this run has already picked. If
    /// Giphy isn't configured, the picker renders no options at all -- the app degrades
    /// gracefully to "no avatar", so this does too, rather than failing the run.
    /// </summary>
    private async Task PickUnusedAvatarAsync(HashSet<string> usedAvatars)
    {
        var options = Page.GetByRole(AriaRole.Radiogroup, new() { Name = "Choose an avatar" }).GetByRole(AriaRole.Radio);

        try
        {
            await options.First.WaitForAsync(new() { Timeout = 5_000 });
        }
        catch (TimeoutException)
        {
            return;
        }

        var count = await options.CountAsync();
        var chosenIndex = 0;

        for (var i = 0; i < count; i++)
        {
            var src = await options.Nth(i).Locator("img").GetAttributeAsync("src");
            if (src is not null && usedAvatars.Add(src))
            {
                chosenIndex = i;
                break;
            }
        }

        await options.Nth(chosenIndex).ClickAsync();
    }

    private string RoomIdFromUrl()
    {
        var uri = new Uri(Page.Url);
        return uri.AbsolutePath.Trim('/');
    }
}
