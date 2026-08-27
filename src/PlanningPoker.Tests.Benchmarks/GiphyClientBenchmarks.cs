using System.Net;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Memory;
using PlanningPoker.Api.Giphy;

namespace PlanningPoker.Tests.Benchmarks;

/// <summary>Justifies the <see cref="IMemoryCache"/> layer in front of the Giphy HTTP call.</summary>
[MemoryDiagnoser]
public class GiphyClientBenchmarks
{
    private const string SampleResponse = """
        {"data":[
            {"images":{"fixed_height_small":{"url":"https://example.test/1.gif"}}},
            {"images":{"fixed_height_small":{"url":"https://example.test/2.gif"}}},
            {"images":{"fixed_height_small":{"url":"https://example.test/3.gif"}}}
        ]}
        """;

    private HttpClient _httpClient = null!;
    private GiphyOptions _options = null!;
    private GiphyClient _warmClient = null!;

    [GlobalSetup]
    public void Setup()
    {
        _httpClient = new HttpClient(new StubHttpMessageHandler(SampleResponse));
        _options = new GiphyOptions { BaseUrl = "https://example.test/search", Query = "q=test", CacheTtlSeconds = 9999 };

        _warmClient = new GiphyClient(_httpClient, _options, new MemoryCache(new MemoryCacheOptions()));
        _warmClient.GetRandomImageUrlsAsync(3).GetAwaiter().GetResult(); // prime the cache once
    }

    [Benchmark(Baseline = true)]
    public async Task<IReadOnlyList<string>> CacheHit() => await _warmClient.GetRandomImageUrlsAsync(3);

    [Benchmark]
    public async Task<IReadOnlyList<string>> CacheMiss()
    {
        // A fresh cache every invocation forces the HTTP round trip each time.
        var client = new GiphyClient(_httpClient, _options, new MemoryCache(new MemoryCacheOptions()));
        return await client.GetRandomImageUrlsAsync(3);
    }

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseBody) });
    }
}
