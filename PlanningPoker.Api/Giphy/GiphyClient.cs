using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace PlanningPoker.Api.Giphy;

/// <summary>
/// Registered via <c>AddHttpClient&lt;IGiphyClient, GiphyClient&gt;()</c> (pooled/reused
/// <see cref="HttpClient"/>, avoids socket exhaustion under load). The raw response batch is cached
/// for <see cref="GiphyOptions.CacheTtlSeconds"/> to protect Giphy's rate limit during join/reveal
/// bursts, but every call still re-shuffles the cached batch so repeated calls within the TTL window
/// don't all return the identical selection.
/// </summary>
public sealed class GiphyClient : IGiphyClient
{
    private const string CacheKey = "giphy:batch";

    private readonly HttpClient _httpClient;
    private readonly GiphyOptions _options;
    private readonly IMemoryCache _cache;

    public GiphyClient(HttpClient httpClient, GiphyOptions options, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
    }

    public async Task<IReadOnlyList<string>> GetRandomImageUrlsAsync(int count, CancellationToken cancellationToken = default)
    {
        var batch = await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.CacheTtlSeconds);
            return await FetchBatchAsync(cancellationToken);
        });

        if (batch is null || batch.Count == 0)
        {
            return [];
        }

        return batch.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
    }

    private async Task<List<string>> FetchBatchAsync(CancellationToken cancellationToken)
    {
        var requestUri = $"{_options.BaseUrl}&{_options.Query}";

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var urls = new List<string>();
        foreach (var item in data.EnumerateArray())
        {
            if (item.TryGetProperty("images", out var images) &&
                images.TryGetProperty("original", out var original) &&
                original.TryGetProperty("url", out var urlElement) &&
                urlElement.GetString() is { Length: > 0 } url)
            {
                urls.Add(url);
            }
        }

        return urls;
    }
}
