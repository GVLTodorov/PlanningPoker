using System.Collections.Immutable;

namespace PlanningPoker.Domain.Decks;

/// <summary>
/// Single source of truth for deck contents. Values are taken verbatim from the reference
/// implementation (axeleroy/self-host-planning-poker, flask/gamestate/deck.py).
/// </summary>
public static class DeckCatalog
{
    private static readonly ImmutableDictionary<DeckType, ImmutableArray<CardOption>> ByType =
        new Dictionary<DeckType, ImmutableArray<CardOption>>
        {
            [DeckType.Fibonacci] = BuildNumeric([0, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89]),
            [DeckType.ModifiedFibonacci] = BuildNumeric([0, 0.5, 1, 2, 3, 5, 8, 13, 20, 40, 100]),
            [DeckType.Powers] = BuildNumeric([0, 1, 2, 4, 8, 16, 32, 64]),
            [DeckType.TrustVote] = BuildNumeric([0, 1, 2, 3, 4, 5]),
            [DeckType.TShirts] = BuildTShirts(),
        }.ToImmutableDictionary();

    private static readonly ImmutableDictionary<DeckType, string> DisplayNames = new Dictionary<DeckType, string>
    {
        [DeckType.Fibonacci] = "Fibonacci",
        [DeckType.ModifiedFibonacci] = "Modified Fibonacci",
        [DeckType.Powers] = "Powers of 2",
        [DeckType.TrustVote] = "Trust Vote",
        [DeckType.TShirts] = "T-Shirt Sizes",
    }.ToImmutableDictionary();

    public static ImmutableArray<CardOption> Get(DeckType deckType) => ByType[deckType];

    public static string GetDisplayName(DeckType deckType) => DisplayNames[deckType];

    public static IEnumerable<DeckType> AllTypes => ByType.Keys;

    private static ImmutableArray<CardOption> BuildNumeric(double[] values) =>
        values
            .Select((value, index) => new CardOption(index, value, FormatLabel(value)))
            .ToImmutableArray();

    private static ImmutableArray<CardOption> BuildTShirts()
    {
        string[] labels = ["XXS", "XS", "S", "M", "L", "XL", "XXL"];
        double[] values = [1, 2, 3, 4, 5, 6, 7];
        return values
            .Select((value, index) => new CardOption(index, value, labels[index]))
            .ToImmutableArray();
    }

    private static string FormatLabel(double value) =>
        value == Math.Floor(value) ? ((long)value).ToString() : value.ToString("0.#");
}
