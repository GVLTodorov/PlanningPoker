using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PlanningPoker.Contracts.Messages;
using PlanningPoker.Contracts.Requests;

namespace PlanningPoker.Contracts.Serialization;

/// <summary>
/// Source-generated (de)serializers for every DTO crossing the wire — used for both the SignalR
/// JSON Hub Protocol and the REST endpoints' JSON options, avoiding reflection-based serialization
/// on the hottest path in the app (every pick/reveal fans out to every connected player).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CardOptionDto))]
[JsonSerializable(typeof(DeckDto))]
[JsonSerializable(typeof(IReadOnlyList<DeckDto>))]
[JsonSerializable(typeof(PlayerDto))]
[JsonSerializable(typeof(RoomStateDto))]
[JsonSerializable(typeof(RoomSummaryDto))]
[JsonSerializable(typeof(CreateRoomRequest))]
[JsonSerializable(typeof(RoomNameSuggestionResponse))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(JoinRoomResult))]
[JsonSerializable(typeof(PlayerPickStatusChanged))]
[JsonSerializable(typeof(RoundRevealed))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(Guid?))]
public partial class PlanningPokerJsonContext : JsonSerializerContext
{
    /// <summary>
    /// Builds options with this context first in the resolver chain (fast path for every DTO
    /// above) and a plain reflection resolver appended after it as a fallback — a
    /// <see cref="JsonSerializerContext"/> alone throws <see cref="NotSupportedException"/> for any
    /// type it wasn't told about, so the fallback is required, not optional, for e.g. SignalR's own
    /// framing types or hub method parameters we didn't explicitly list above.
    /// </summary>
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.TypeInfoResolverChain.Insert(0, Default);
        options.TypeInfoResolverChain.Add(new DefaultJsonTypeInfoResolver());
        return options;
    }
}
