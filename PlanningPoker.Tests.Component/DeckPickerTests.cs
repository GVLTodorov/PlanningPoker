using Bunit;
using PlanningPoker.Client.Components;
using PlanningPoker.Contracts;
using Xunit;

namespace PlanningPoker.Tests.Component;

public class DeckPickerTests : BunitContext
{
    private static readonly List<DeckDto> Decks =
    [
        new(DeckType.Fibonacci, "Fibonacci", [new CardOptionDto(0, 0, "0"), new CardOptionDto(1, 1, "1"), new CardOptionDto(2, 2, "2")]),
        new(DeckType.TShirts, "T-Shirt Sizes", [new CardOptionDto(0, 1, "XS"), new CardOptionDto(1, 2, "S")]),
    ];

    [Fact]
    public void RendersEachDeckOption_WithLabelAndBracketedValues()
    {
        var cut = Render<DeckPicker>(p => p
            .Add(x => x.Decks, Decks)
            .Add(x => x.SelectedDeckType, DeckType.Fibonacci));

        var options = cut.FindAll("option");

        Assert.Equal(2, options.Count);
        Assert.Equal("Fibonacci (0, 1, 2)", options[0].TextContent);
        Assert.Equal("T-Shirt Sizes (XS, S)", options[1].TextContent);
    }

    [Fact]
    public void SelectingAnOption_RaisesSelectedDeckTypeChanged()
    {
        DeckType? changedTo = null;
        var cut = Render<DeckPicker>(p => p
            .Add(x => x.Decks, Decks)
            .Add(x => x.SelectedDeckType, DeckType.Fibonacci)
            .Add(x => x.SelectedDeckTypeChanged, deckType => changedTo = deckType));

        cut.Find("select").Change(nameof(DeckType.TShirts));

        Assert.Equal(DeckType.TShirts, changedTo);
    }
}
