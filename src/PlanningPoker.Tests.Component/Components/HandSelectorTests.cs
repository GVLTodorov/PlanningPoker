using Bunit;
using PlanningPoker.Client.Components;
using PlanningPoker.Contracts;
using Xunit;

namespace PlanningPoker.Tests.Component.Components;

public class HandSelectorTests : BunitContext
{
    private static readonly CardOptionResponse[] Deck =
    [
        new CardOptionResponse(0, 1, "1"),
        new CardOptionResponse(1, 2, "2"),
        new CardOptionResponse(2, 3, "3"),
    ];

    [Fact]
    public void RendersNoButtons_WhenDeckIsEmpty()
    {
        var cut = Render<HandSelector>(p => p.Add(x => x.Deck, Array.Empty<CardOptionResponse>()));

        Assert.Empty(cut.FindAll("button"));
    }

    [Fact]
    public void MarksTheSelectedCard_AndOnlyThatCard()
    {
        var cut = Render<HandSelector>(p => p
            .Add(x => x.Deck, Deck)
            .Add(x => x.SelectedCardIndex, 1));

        var buttons = cut.FindAll("button");
        Assert.DoesNotContain("hand-card-selected", buttons[0].ClassList);
        Assert.Contains("hand-card-selected", buttons[1].ClassList);
        Assert.DoesNotContain("hand-card-selected", buttons[2].ClassList);
    }

    [Fact]
    public void NoCardIsMarkedSelected_WhenSelectedCardIndexIsNull()
    {
        var cut = Render<HandSelector>(p => p.Add(x => x.Deck, Deck));

        Assert.All(cut.FindAll("button"), b => Assert.DoesNotContain("hand-card-selected", b.ClassList));
    }

    [Fact]
    public void ButtonsAreDisabled_WhenDisabledIsTrue()
    {
        var cut = Render<HandSelector>(p => p
            .Add(x => x.Deck, Deck)
            .Add(x => x.Disabled, true));

        Assert.All(cut.FindAll("button"), b => Assert.True(b.HasAttribute("disabled")));
    }

    [Fact]
    public void ButtonsAreEnabled_ByDefault()
    {
        var cut = Render<HandSelector>(p => p.Add(x => x.Deck, Deck));

        Assert.All(cut.FindAll("button"), b => Assert.False(b.HasAttribute("disabled")));
    }

    [Fact]
    public void ClickingAnUnselectedCard_RaisesItsIndex()
    {
        int? changedTo = -1;
        var cut = Render<HandSelector>(p => p
            .Add(x => x.Deck, Deck)
            .Add(x => x.SelectedCardIndex, 0)
            .Add(x => x.SelectedCardIndexChanged, i => changedTo = i));

        cut.FindAll("button")[2].Click();

        Assert.Equal(2, changedTo);
    }

    [Fact]
    public void ClickingTheAlreadySelectedCard_ClearsTheSelection()
    {
        int? changedTo = -1;
        var cut = Render<HandSelector>(p => p
            .Add(x => x.Deck, Deck)
            .Add(x => x.SelectedCardIndex, 1)
            .Add(x => x.SelectedCardIndexChanged, i => changedTo = i));

        cut.FindAll("button")[1].Click();

        Assert.Null(changedTo);
    }
}
