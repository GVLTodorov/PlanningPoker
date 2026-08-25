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

    [Fact]
    public void FocusElement_IsInvokedOnlyOnce_AcrossMultipleRenders()
    {
        // OnAfterRenderAsync early-returns on every render after the first -- a second render pass
        // (triggered here by a parameter change) must not call focusElement again.
        var cut = Render<PlayerSetupForm>(p => p.Add(x => x.AvatarUrls, []).Add(x => x.Name, "Alice"));

        cut.Render(p => p.Add(x => x.AvatarUrls, []).Add(x => x.Name, "Alicia"));

        JSInterop.VerifyInvoke("focusElement", calledTimes: 1);
    }

    [Fact]
    public void RefreshButton_ShowsSpinningState_WhileRefreshingAvatars()
    {
        var cut = Render<PlayerSetupForm>(p => p
            .Add(x => x.AvatarUrls, [])
            .Add(x => x.IsRefreshingAvatars, true));

        var button = cut.Find(".icon-button");
        Assert.Contains("icon-button-spinning", button.ClassList);
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public void RefreshButton_IsNotSpinning_ByDefault()
    {
        var cut = Render<PlayerSetupForm>(p => p.Add(x => x.AvatarUrls, []));

        var button = cut.Find(".icon-button");
        Assert.DoesNotContain("icon-button-spinning", button.ClassList);
        Assert.False(button.HasAttribute("disabled"));
    }
}
