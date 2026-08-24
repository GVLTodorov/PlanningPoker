using System.Net.Http.Json;
using System.Text.Json;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Requests;
using PlanningPoker.Contracts.Serialization;

namespace PlanningPoker.Client.Services;

/// <summary>Thin typed wrapper over the REST endpoints in <c>Api/Endpoints/RoomEndpoints.cs</c>.</summary>
public sealed class RoomApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = PlanningPokerJsonContext.CreateOptions();

    private readonly HttpClient _httpClient;

    public RoomApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetRoomNameSuggestionAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<RoomNameSuggestionResponse>(
            "/api/rooms/name-suggestion", JsonOptions, cancellationToken);
        return response?.Name ?? string.Empty;
    }

    public async Task<IReadOnlyList<DeckResponse>> GetDecksAsync(CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<List<DeckResponse>>("/api/decks", JsonOptions, cancellationToken) ?? [];

    public async Task<IReadOnlyList<string>> GetRandomAvatarsAsync(int count = 3, CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<List<string>>($"/api/avatars/random?count={count}", JsonOptions, cancellationToken) ?? [];

    public async Task<RoomSummaryResponse?> CreateRoomAsync(string name, DeckType deckType, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/rooms", new CreateRoomRequest(name, deckType), JsonOptions, cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions, cancellationToken)
            : null;
    }

    public async Task<RoomSummaryResponse?> GetRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/api/rooms/{roomId}", cancellationToken);

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RoomSummaryResponse>(JsonOptions, cancellationToken)
            : null;
    }
}
