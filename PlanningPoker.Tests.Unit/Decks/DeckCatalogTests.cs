using PlanningPoker.Domain.Decks;
using Xunit;

namespace PlanningPoker.Tests.Unit.Decks;

public class DeckCatalogTests
{
    [Theory]
    [InlineData(DeckType.Fibonacci, new double[] { 0, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89 })]
    [InlineData(DeckType.ModifiedFibonacci, new double[] { 0, 0.5, 1, 2, 3, 5, 8, 13, 20, 40, 100 })]
    [InlineData(DeckType.Powers, new double[] { 0, 1, 2, 4, 8, 16, 32, 64 })]
    [InlineData(DeckType.TrustVote, new double[] { 0, 1, 2, 3, 4, 5 })]
    [InlineData(DeckType.TShirts, new double[] { 1, 2, 3, 4, 5, 6, 7 })]
    public void Get_MatchesReferenceImplementationValues(DeckType deckType, double[] expectedValues)
    {
        var cards = DeckCatalog.Get(deckType);

        Assert.Equal(expectedValues, cards.Select(c => c.Value));
    }

    [Fact]
    public void Get_IndexesAreSequentialFromZero()
    {
        var cards = DeckCatalog.Get(DeckType.Fibonacci);

        Assert.Equal(Enumerable.Range(0, cards.Length), cards.Select(c => c.Index));
    }

    [Fact]
    public void Get_TShirts_UsesSizeLabelsNotRawValues()
    {
        var cards = DeckCatalog.Get(DeckType.TShirts);

        Assert.Equal(["XXS", "XS", "S", "M", "L", "XL", "XXL"], cards.Select(c => c.Label));
    }

    [Fact]
    public void Get_ModifiedFibonacci_FormatsHalfStepWithoutTrailingZero()
    {
        var cards = DeckCatalog.Get(DeckType.ModifiedFibonacci);

        Assert.Equal("0.5", cards.Single(c => c.Value == 0.5).Label);
    }

    [Theory]
    [InlineData(DeckType.Fibonacci)]
    [InlineData(DeckType.ModifiedFibonacci)]
    [InlineData(DeckType.Powers)]
    [InlineData(DeckType.TrustVote)]
    [InlineData(DeckType.TShirts)]
    public void GetDisplayName_IsNonEmpty_ForEveryDeckType(DeckType deckType)
    {
        Assert.False(string.IsNullOrWhiteSpace(DeckCatalog.GetDisplayName(deckType)));
    }

    [Fact]
    public void AllTypes_ContainsEveryEnumValue()
    {
        var allEnumValues = Enum.GetValues<DeckType>();

        Assert.Equal(allEnumValues.OrderBy(v => v), DeckCatalog.AllTypes.OrderBy(v => v));
    }
}
