using PlanningPoker.Api.Mapping;
using PlanningPoker.Contracts;
using Xunit;
using DomainCardOption = PlanningPoker.Domain.Decks.CardOption;
using DomainDeckType = PlanningPoker.Domain.Decks.DeckType;
using DomainPlayerView = PlanningPoker.Domain.Rooms.PlayerView;
using DomainRoundStatus = PlanningPoker.Domain.Rooms.RoundStatus;

namespace PlanningPoker.Tests.Unit.Mapping;

public class ContractExtensionsTests
{
    // Every domain DeckType member must map to its contract counterpart — a case dropped from this
    // theory would silently fall through to the switch's default arm the next time a case is added
    // to the enum but forgotten here.
    [Theory]
    [InlineData(DomainDeckType.Fibonacci, DeckType.Fibonacci)]
    [InlineData(DomainDeckType.ModifiedFibonacci, DeckType.ModifiedFibonacci)]
    [InlineData(DomainDeckType.Powers, DeckType.Powers)]
    [InlineData(DomainDeckType.TrustVote, DeckType.TrustVote)]
    [InlineData(DomainDeckType.TShirts, DeckType.TShirts)]
    public void ToDeckType_MapsEveryDomainMember(DomainDeckType domain, DeckType expected)
    {
        Assert.Equal(expected, domain.ToDeckType());
    }

    [Fact]
    public void ToDeckType_Throws_ForUndefinedValue()
    {
        var undefined = (DomainDeckType)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => undefined.ToDeckType());
    }

    [Theory]
    [InlineData(DeckType.Fibonacci, DomainDeckType.Fibonacci)]
    [InlineData(DeckType.ModifiedFibonacci, DomainDeckType.ModifiedFibonacci)]
    [InlineData(DeckType.Powers, DomainDeckType.Powers)]
    [InlineData(DeckType.TrustVote, DomainDeckType.TrustVote)]
    [InlineData(DeckType.TShirts, DomainDeckType.TShirts)]
    public void ToDomain_MapsEveryContractMember(DeckType contract, DomainDeckType expected)
    {
        Assert.Equal(expected, contract.ToDomain());
    }

    [Fact]
    public void ToDomain_Throws_ForUndefinedValue()
    {
        var undefined = (DeckType)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => undefined.ToDomain());
    }

    [Theory]
    [InlineData(DomainRoundStatus.Voting, RoundStatus.Voting)]
    [InlineData(DomainRoundStatus.Revealed, RoundStatus.Revealed)]
    public void ToRoundStatus_MapsEveryDomainMember(DomainRoundStatus domain, RoundStatus expected)
    {
        Assert.Equal(expected, domain.ToRoundStatus());
    }

    [Fact]
    public void ToRoundStatus_Throws_ForUndefinedValue()
    {
        var undefined = (DomainRoundStatus)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => undefined.ToRoundStatus());
    }

    [Fact]
    public void ToCardOptionResponse_MapsAllFields()
    {
        var card = new DomainCardOption(2, 2, "2");

        var response = card.ToCardOptionResponse();

        Assert.Equal(2, response.Index);
        Assert.Equal(2, response.Value);
        Assert.Equal("2", response.Label);
    }

    [Fact]
    public void ToPlayerResponse_MapsPickedCard_WhenCardIsPresent()
    {
        var player = new DomainPlayerView(Guid.NewGuid(), "Alice", false, true, "https://example.test/a.gif", true, new DomainCardOption(1, 1, "1"));

        var response = player.ToPlayerResponse();

        Assert.NotNull(response.Card);
        Assert.Equal(1, response.Card!.Index);
    }

    [Fact]
    public void ToPlayerResponse_LeavesCardNull_WhenPlayerHasNotBeenRevealed()
    {
        var player = new DomainPlayerView(Guid.NewGuid(), "Bob", true, false, null, false, null);

        var response = player.ToPlayerResponse();

        Assert.Null(response.Card);
    }
}
