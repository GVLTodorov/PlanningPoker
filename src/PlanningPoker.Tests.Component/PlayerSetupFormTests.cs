using Bunit;
using PlanningPoker.Client.Components;
using Xunit;

namespace PlanningPoker.Tests.Component;

public class PlayerSetupFormTests : BunitContext
{
    public PlayerSetupFormTests()
    {
        JSInterop.SetupModule("./js/interop.js").SetupVoid("focusElement", _ => true);
    }

    [Fact]
    public void SpectatorCheckbox_IsUncheckedByDefault()
    {
        var cut = Render<PlayerSetupForm>(p => p.Add(x => x.AvatarUrls, []));

        var checkbox = cut.Find("input[type=checkbox]");
        Assert.False(checkbox.HasAttribute("checked"));
    }

    [Fact]
    public void SpectatorCheckbox_ReflectsIsSpectatorParameter()
    {
        var cut = Render<PlayerSetupForm>(p => p
            .Add(x => x.AvatarUrls, [])
            .Add(x => x.IsSpectator, true));

        var checkbox = cut.Find("input[type=checkbox]");
        Assert.True(checkbox.HasAttribute("checked"));
    }

    [Fact]
    public void TogglingSpectatorCheckbox_RaisesIsSpectatorChanged()
    {
        bool? raised = null;
        var cut = Render<PlayerSetupForm>(p => p
            .Add(x => x.AvatarUrls, [])
            .Add(x => x.IsSpectatorChanged, v => raised = v));

        cut.Find("input[type=checkbox]").Change(true);

        Assert.True(raised);
    }
}
