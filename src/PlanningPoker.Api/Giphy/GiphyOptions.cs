namespace PlanningPoker.Api.Giphy;

/// <summary>Bound directly from the <c>GIPHY_API_BASE_URL</c> / <c>GIPHY_API_QUERY</c> env vars in Program.cs.</summary>
public sealed class GiphyOptions
{
    public string? BaseUrl { get; init; }

    public string? Query { get; init; }

    public int CacheTtlSeconds { get; init; } = 30;

    /// <summary>Upper bound (exclusive) for the random <c>offset</c> added to every Giphy request, so
    /// a fresh cache fetch pulls a different slice of the result set instead of always the first page.</summary>
    public int MaxOffset { get; init; } = 50;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Query);
}
