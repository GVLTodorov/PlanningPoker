using Microsoft.AspNetCore.Components;

namespace PlanningPoker.Client.Services;

/// <summary>1 production implementer -- colocated with it per the repo's interface convention.</summary>
public interface IGameHubClientFactory
{
    IGameHubClient Create();
}

public sealed class GameHubClientFactory : IGameHubClientFactory
{
    private readonly NavigationManager _navigation;

    public GameHubClientFactory(NavigationManager navigation)
    {
        _navigation = navigation;
    }

    public IGameHubClient Create() => new GameHubClient(_navigation);
}
