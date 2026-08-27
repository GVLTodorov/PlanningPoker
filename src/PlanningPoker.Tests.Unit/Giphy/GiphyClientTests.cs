using System.Net;
using Microsoft.Extensions.Caching.Memory;
using PlanningPoker.Api.Giphy;
using PlanningPoker.Tests.Unit.TestSupport;
using Xunit;

namespace PlanningPoker.Tests.Unit.Giphy;

public class GiphyClientTests
{
    private const string ThreeItemBatch = """
        {"data":[
            {"images":{"fixed_height_small":{"url":"https://example.test/1.gif"}}},
            {"images":{"fixed_height_small":{"url":"https://example.test/2.gif"}}},
            {"images":{"fixed_height_small":{"url":"https://example.test/3.gif"}}}
        ]}
        """;

    private static GiphyOptions Options => new() { BaseUrl = "https://example.test/search", Query = "q=test", CacheTtlSeconds = 9999 };

    [Fact]
    public async Task GetRandomImageUrlsAsync_ReturnsUrls_OnSuccessfulResponse()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, ThreeItemBatch);
        var client = new GiphyClient(new HttpClient(handler), Options, new MemoryCache(new MemoryCacheOptions()));

        var urls = await client.GetRandomImageUrlsAsync(3);

        Assert.Equal(3, urls.Count);
        Assert.All(urls, url => Assert.StartsWith("https://example.test/", url));
    }

    [Fact]
    public async Task GetRandomImageUrlsAsync_RequestsBaseUrlAndQueryJoinedByAmpersand()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, ThreeItemBatch);
        var client = new GiphyClient(new HttpClient(handler), Options, new MemoryCache(new MemoryCacheOptions()));

        await client.GetRandomImageUrlsAsync(3);

        Assert.Equal("https://example.test/search&q=test", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GetRandomImageUrlsAsync_ReturnsEmpty_WhenResponseIsNotSuccessStatusCode()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError);
        var client = new GiphyClient(new HttpClient(handler), Options, new MemoryCache(new MemoryCacheOptions()));

        var urls = await client.GetRandomImageUrlsAsync(3);

        Assert.Empty(urls);
    }

    [Fact]
    public async Task GetRandomImageUrlsAsync_ReturnsEmpty_WhenDataKeyIsMissing()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = new GiphyClient(new HttpClient(handler), Options, new MemoryCache(new MemoryCacheOptions()));

        var urls = await client.GetRandomImageUrlsAsync(3);

        Assert.Empty(urls);
    }

    [Fact]
    public async Task GetRandomImageUrlsAsync_ReturnsEmpty_WhenDataIsNotAnArray()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"data":"not-an-array"}""");
        var client = new GiphyClient(new HttpClient(handler), Options, new MemoryCache(new MemoryCacheOptions()));

        var urls = await client.GetRandomImageUrlsAsync(3);

        Assert.Empty(urls);
    }

    // Each case below drops one link in the images.fixed_height_small.url chain (or leaves the url
    // empty), which should skip just that malformed item rather than throwing or including a garbage entry.
    [Theory]
    [InlineData("""{"data":[{"notImages":{}}]}""")]
    [InlineData("""{"data":[{"images":{"notFixedHeightSmall":{}}}]}""")]
    [InlineData("""{"data":[{"images":{"fixed_height_small":{"notUrl":"x"}}}]}""")]
    [InlineData("""{"data":[{"images":{"fixed_height_small":{"url":""}}}]}""")]
    public async Task GetRandomImageUrlsAsync_SkipsMalformedItems(string responseBody)
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var client = new GiphyClient(new HttpClient(handler), Options, new MemoryCache(new MemoryCacheOptions()));

        var urls = await client.GetRandomImageUrlsAsync(3);

        Assert.Empty(urls);
    }

    [Fact]
    public async Task GetRandomImageUrlsAsync_ClampsToBatchSize_WhenCountExceedsAvailableItems()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, ThreeItemBatch);
        var client = new GiphyClient(new HttpClient(handler), Options, new MemoryCache(new MemoryCacheOptions()));

        var urls = await client.GetRandomImageUrlsAsync(10);

        Assert.Equal(3, urls.Count);
    }

    [Fact]
    public async Task GetRandomImageUrlsAsync_DoesNotRefetch_WithinCacheTtl()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, ThreeItemBatch);
        var client = new GiphyClient(new HttpClient(handler), Options, new MemoryCache(new MemoryCacheOptions()));

        await client.GetRandomImageUrlsAsync(3);
        await client.GetRandomImageUrlsAsync(3);

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetRandomImageUrlsAsync_Refetches_AfterCacheExpires()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, ThreeItemBatch);
        var options = new GiphyOptions { BaseUrl = "https://example.test/search", Query = "q=test", CacheTtlSeconds = 1 };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new GiphyClient(new HttpClient(handler), options, cache);

        await client.GetRandomImageUrlsAsync(3);
        // Wait out the 1s TTL so the second call must hit the handler again instead of silently
        // reusing an already-expired cache entry.
        await Task.Delay(TimeSpan.FromMilliseconds(1100));
        await client.GetRandomImageUrlsAsync(3);

        Assert.Equal(2, handler.RequestCount);
    }
}
