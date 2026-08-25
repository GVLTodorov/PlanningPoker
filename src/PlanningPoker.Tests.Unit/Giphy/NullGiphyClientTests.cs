using PlanningPoker.Api.Giphy;
using Xunit;

namespace PlanningPoker.Tests.Unit.Giphy;

public class NullGiphyClientTests
{
    [Fact]
    public async Task GetRandomImageUrlsAsync_AlwaysReturnsEmpty()
    {
        var client = new NullGiphyClient();

        var urls = await client.GetRandomImageUrlsAsync(5);

        Assert.Empty(urls);
    }
}
