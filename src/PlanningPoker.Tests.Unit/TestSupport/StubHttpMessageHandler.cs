using System.Net;

namespace PlanningPoker.Tests.Unit.TestSupport;

/// <summary>Hand-written HttpClient fake (this repo uses no mocking framework) that returns a
/// caller-supplied response per request and records how many requests it saw, so tests can assert
/// cache-hit/cache-miss behavior without a real HTTP endpoint.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(HttpStatusCode statusCode, string? responseBody = null)
        : this(_ => new HttpResponseMessage(statusCode)
        {
            Content = responseBody is null ? null : new StringContent(responseBody),
        })
    {
    }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public int RequestCount { get; private set; }

    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequest = request;
        return Task.FromResult(_responder(request));
    }
}
