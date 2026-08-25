using Bunit;
using Microsoft.AspNetCore.Components;
using PlanningPoker.Client.Layout;
using Xunit;

namespace PlanningPoker.Tests.Component.Layout;

public class MainLayoutTests : BunitContext
{
    [Fact]
    public void RendersBodyInsideTheAppShell()
    {
        RenderFragment body = builder => builder.AddMarkupContent(0, "<p>page content</p>");

        var cut = Render<MainLayout>(p => p.Add(x => x.Body, body));

        var shell = cut.Find("main.app-shell");
        Assert.Contains("page content", shell.InnerHtml);
    }
}
