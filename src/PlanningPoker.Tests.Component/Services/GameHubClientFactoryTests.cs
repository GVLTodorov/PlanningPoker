using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.Client.Services;
using Xunit;

namespace PlanningPoker.Tests.Component.Services;

public class GameHubClientFactoryTests : BunitContext
{
    [Fact]
    public void Create_ReturnsAGameHubClient_BuiltFromTheInjectedNavigationManager()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        var factory = new GameHubClientFactory(navigation);

        var client = factory.Create();

        Assert.IsType<GameHubClient>(client);
    }
}
