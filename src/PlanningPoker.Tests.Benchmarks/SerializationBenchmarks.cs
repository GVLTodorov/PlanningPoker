using System.Text.Json;
using BenchmarkDotNet.Attributes;
using PlanningPoker.Contracts;
using PlanningPoker.Contracts.Serialization;

namespace PlanningPoker.Tests.Benchmarks;

/// <summary>
/// Justifies the source-gen JSON context (REQUIREMENTS.MD Section 10.1): the hub message envelope
/// is the hottest path in the app, so this measures the win over plain reflection-based
/// serialization for a representative <see cref="RoomStateDto"/> payload.
/// </summary>
[MemoryDiagnoser]
public class SerializationBenchmarks
{
    private static readonly JsonSerializerOptions ReflectionOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions SourceGenOptions = PlanningPokerJsonContext.CreateOptions();

    private RoomStateDto _state = null!;

    [GlobalSetup]
    public void Setup()
    {
        var players = Enumerable.Range(0, 10)
            .Select(i => new PlayerDto(
                Guid.NewGuid(),
                $"Player{i}",
                IsSpectator: false,
                IsHost: i == 0,
                AvatarUrl: "https://example.test/a.gif",
                HasPicked: true,
                Card: new CardOptionDto(i % 11, i, i.ToString())))
            .ToList();

        _state = new RoomStateDto("ABCD1234", "Benchmark Room", DeckType.Fibonacci, RoundStatus.Revealed, players);
    }

    [Benchmark(Baseline = true)]
    public string Reflection() => JsonSerializer.Serialize(_state, ReflectionOptions);

    [Benchmark]
    public string SourceGenerated() => JsonSerializer.Serialize(_state, SourceGenOptions);
}
