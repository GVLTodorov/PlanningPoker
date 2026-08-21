namespace PlanningPoker.Api.Giphy;

/// <summary>Bound directly from the <c>GIPHY_API_BASE_URL</c> / <c>GIPHY_API_QUERY</c> env vars in Program.cs.</summary>
public sealed class GiphyOptions
{
    public string? BaseUrl { get; init; }

    public string? Query { get; init; }

    public int CacheTtlSeconds { get; init; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Query);
}
