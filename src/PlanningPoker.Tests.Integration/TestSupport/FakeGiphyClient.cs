using PlanningPoker.Api.Giphy;

namespace PlanningPoker.Tests.Integration.TestSupport;

/// <summary>Hand-written IGiphyClient test double (this repo uses no mocking framework) that records
/// the `count` it was called with and returns a fixed list, so a test can assert on the count the
/// /api/avatars/random endpoint actually clamped and passed through.</summary>
internal sealed class FakeGiphyClient : IGiphyClient
{
    public int? LastRequestedCount { get; private set; }

    public Task<IReadOnlyList<string>> GetRandomImageUrlsAsync(int count, CancellationToken cancellationToken = default)
    {
        LastRequestedCount = count;
        IReadOnlyList<string> urls = Enumerable.Range(1, count).Select(i => $"https://example.test/{i}.gif").ToList();
        return Task.FromResult(urls);
    }
}
