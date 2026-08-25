using PlanningPoker.Api.Giphy;
using Xunit;

namespace PlanningPoker.Tests.Unit.Giphy;

public class GiphyOptionsTests
{
    [Fact]
    public void IsConfigured_IsTrue_WhenBothBaseUrlAndQueryAreSet()
    {
        var options = new GiphyOptions { BaseUrl = "https://example.test", Query = "q=test" };

        Assert.True(options.IsConfigured);
    }

    [Theory]
    [InlineData(null, "q=test")]
    [InlineData("", "q=test")]
    [InlineData("   ", "q=test")]
    [InlineData("https://example.test", null)]
    [InlineData("https://example.test", "")]
    [InlineData("https://example.test", "   ")]
    [InlineData(null, null)]
    public void IsConfigured_IsFalse_WhenEitherValueIsMissing(string? baseUrl, string? query)
    {
        var options = new GiphyOptions { BaseUrl = baseUrl, Query = query };

        Assert.False(options.IsConfigured);
    }
}
