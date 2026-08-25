using PlanningPoker.Client.Services;

namespace PlanningPoker.Tests.Component.TestSupport;

/// <summary>Hands out a single pre-built FakeGameHubClient, keeping it accessible from the test after
/// Render&lt;Board&gt;(...) so it can raise events / inspect calls post-render.</summary>
internal sealed class FakeGameHubClientFactory : IGameHubClientFactory
{
    public FakeGameHubClient Client { get; } = new();

    public IGameHubClient Create() => Client;
}
