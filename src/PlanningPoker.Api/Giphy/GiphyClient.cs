using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace PlanningPoker.Api.Giphy;

/// <summary>
/// Registered via <c>AddHttpClient&lt;IGiphyClient, GiphyClient&gt;()</c> (pooled/reused
/// <see cref="HttpClient"/>, avoids socket exhaustion under load). The raw response batch is cached
/// for <see cref="GiphyOptions.CacheTtlSeconds"/> to protect Giphy's rate limit during join/reveal
/// bursts; every call re-shuffles the cached batch so repeated calls within the TTL window don't all
/// return the identical selection, and every fetch that refills the cache asks Giphy for a random
/// <c>offset</c> (see <see cref="GiphyOptions.MaxOffset"/>) so a fresh batch is a different slice of
/// the result set rather than always the same first page.
/// </summary>
public sealed class GiphyClient : IGiphyClient
{
    private const string CacheKey = "giphy:batch";

    // Giphy's ~100px-tall, still-animated rendition. The avatar picker only ever displays
    // these at 96x72px (see AvatarPicker.razor / .avatar-option in app.css), so this avoids
    // fetching Giphy's full-resolution "original" GIF (hundreds of KB to several MB, unresized).
    private const string ImageRenditionKey = "fixed_height_small";

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
        // Appended last so it wins over any (now-unneeded) static "offset=" a deployment's
        // GIPHY_API_QUERY might still set -- query-string parsers take the last occurrence of a
        // repeated key.
        var offset = Random.Shared.Next(0, _options.MaxOffset);
        var requestUri = $"{_options.BaseUrl}&{_options.Query}&offset={offset}";

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
                images.TryGetProperty(ImageRenditionKey, out var rendition) &&
                rendition.TryGetProperty("url", out var urlElement) &&
                urlElement.GetString() is { Length: > 0 } url)
            {
                urls.Add(url);
            }
        }

        return urls;
    }
}
