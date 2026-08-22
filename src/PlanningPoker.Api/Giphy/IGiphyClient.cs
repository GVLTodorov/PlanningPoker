namespace PlanningPoker.Api.Giphy;

public interface IGiphyClient
{
    Task<IReadOnlyList<string>> GetRandomImageUrlsAsync(int count, CancellationToken cancellationToken = default);
}
