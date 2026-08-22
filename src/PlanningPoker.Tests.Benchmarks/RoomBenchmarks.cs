using BenchmarkDotNet.Attributes;
using PlanningPoker.Domain.Decks;
using PlanningPoker.Domain.Rooms;

namespace PlanningPoker.Tests.Benchmarks;

/// <summary>Hot-path domain operations: every pick and every reveal fans out to every connected player.</summary>
[MemoryDiagnoser]
public class RoomBenchmarks
{
    private Room _room = null!;
    private Guid _playerId;

    [GlobalSetup]
    public void Setup()
    {
        RoomId.TryParse("Benchmark Room", out var id);
        _room = new Room(id, "Benchmark Room", DeckType.Fibonacci);

        for (var i = 0; i < 10; i++)
        {
            var player = _room.AddPlayer($"Player{i}", isSpectator: false, avatarUrl: "https://example.test/a.gif");
            _room.PickCard(player.Id, i % 11);

            if (i == 0)
            {
                _playerId = player.Id;
            }
        }
    }

    [Benchmark]
    public void PickCard() => _room.PickCard(_playerId, 3);

    [Benchmark]
    public IReadOnlyList<PlayerView> GetState() => _room.GetState();

    [Benchmark]
    public void Reveal() => _room.Reveal(_playerId);
}
