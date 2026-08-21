using PlanningPoker.Contracts;
using DomainCardOption = PlanningPoker.Domain.Decks.CardOption;
using DomainDeckType = PlanningPoker.Domain.Decks.DeckType;
using DomainPlayerView = PlanningPoker.Domain.Rooms.PlayerView;
using DomainRoom = PlanningPoker.Domain.Rooms.Room;
using DomainRoundStatus = PlanningPoker.Domain.Rooms.RoundStatus;

namespace PlanningPoker.Api.Mapping;

/// <summary>
/// Explicit (non-cast) mapping between Domain and Contracts types. The two DeckType/RoundStatus
/// enums are declared separately (the Client must never reference the Domain assembly), so mapping
/// goes through an exhaustive switch rather than an unchecked numeric cast that would silently
/// break if either enum's declaration order ever drifted.
/// </summary>
public static class ContractMapper
{
    public static DeckType ToContract(this DomainDeckType deckType) => deckType switch
    {
        DomainDeckType.Fibonacci => DeckType.Fibonacci,
        DomainDeckType.ModifiedFibonacci => DeckType.ModifiedFibonacci,
        DomainDeckType.Powers => DeckType.Powers,
        DomainDeckType.TrustVote => DeckType.TrustVote,
        DomainDeckType.TShirts => DeckType.TShirts,
        _ => throw new ArgumentOutOfRangeException(nameof(deckType), deckType, null),
    };

    public static DomainDeckType ToDomain(this DeckType deckType) => deckType switch
    {
        DeckType.Fibonacci => DomainDeckType.Fibonacci,
        DeckType.ModifiedFibonacci => DomainDeckType.ModifiedFibonacci,
        DeckType.Powers => DomainDeckType.Powers,
        DeckType.TrustVote => DomainDeckType.TrustVote,
        DeckType.TShirts => DomainDeckType.TShirts,
        _ => throw new ArgumentOutOfRangeException(nameof(deckType), deckType, null),
    };

    public static RoundStatus ToContract(this DomainRoundStatus status) => status switch
    {
        DomainRoundStatus.Voting => RoundStatus.Voting,
        DomainRoundStatus.Revealed => RoundStatus.Revealed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static CardOptionDto ToContract(this DomainCardOption card) =>
        new(card.Index, card.Value, card.Label);

    public static PlayerDto ToContract(this DomainPlayerView player) => new(
        player.PlayerId,
        player.Name,
        player.IsSpectator,
        player.IsHost,
        player.AvatarUrl,
        player.HasPicked,
        player.Card?.ToContract());

    public static RoomStateDto ToStateDto(this DomainRoom room) => new(
        room.Id.Value,
        room.Name,
        room.DeckType.ToContract(),
        room.Status.ToContract(),
        room.GetState().Select(p => p.ToContract()).ToList());

    public static RoomSummaryDto ToSummaryDto(this DomainRoom room) =>
        new(room.Id.Value, room.Name, room.DeckType.ToContract());
}
