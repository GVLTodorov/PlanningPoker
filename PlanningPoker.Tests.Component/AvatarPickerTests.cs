using Bunit;
using PlanningPoker.Client.Components;
using Xunit;

namespace PlanningPoker.Tests.Component;

public class AvatarPickerTests : BunitContext
{
    [Fact]
    public void RendersOneOptionPerAvatarUrl()
    {
        List<string> urls = ["https://example.test/a.gif", "https://example.test/b.gif", "https://example.test/c.gif"];

        var cut = Render<AvatarPicker>(p => p.Add(x => x.AvatarUrls, urls));

        Assert.Equal(3, cut.FindAll(".avatar-option").Count);
    }

    [Fact]
    public void DegradesGracefully_WhenNoAvatarsAvailable()
    {
        var cut = Render<AvatarPicker>(p => p.Add(x => x.AvatarUrls, new List<string>()));

        Assert.Empty(cut.FindAll(".avatar-option"));
        Assert.Contains("No avatars available", cut.Markup);
    }

    [Fact]
    public void SelectedAvatar_GetsSelectedCssClass()
    {
        List<string> urls = ["https://example.test/a.gif", "https://example.test/b.gif"];

        var cut = Render<AvatarPicker>(p => p
            .Add(x => x.AvatarUrls, urls)
            .Add(x => x.SelectedUrl, urls[1]));

        var options = cut.FindAll(".avatar-option");

        Assert.DoesNotContain("avatar-option-selected", options[0].ClassList);
        Assert.Contains("avatar-option-selected", options[1].ClassList);
    }

    [Fact]
    public void ClickingAnAvatar_RaisesSelectedUrlChanged()
    {
        List<string> urls = ["https://example.test/a.gif", "https://example.test/b.gif"];
        string? selected = null;

        var cut = Render<AvatarPicker>(p => p
            .Add(x => x.AvatarUrls, urls)
            .Add(x => x.SelectedUrlChanged, url => selected = url));

        cut.FindAll(".avatar-option")[1].Click();

        Assert.Equal(urls[1], selected);
    }
}
