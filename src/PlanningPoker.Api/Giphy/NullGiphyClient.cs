namespace PlanningPoker.Api.Giphy;

/// <summary>
/// Used when <c>GIPHY_API_BASE_URL</c>/<c>GIPHY_API_QUERY</c> are unset, so the app runs with no
/// visible Giphy features (empty avatar picker, no reveal gif) instead of failing.
/// </summary>
public sealed class NullGiphyClient : IGiphyClient
{
    public Task<IReadOnlyList<string>> GetRandomImageUrlsAsync(int count, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}
