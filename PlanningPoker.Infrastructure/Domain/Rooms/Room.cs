using PlanningPoker.Domain.Decks;

namespace PlanningPoker.Domain.Rooms;

/// <summary>
/// Aggregate root for a single Planning Poker room. All mutations are synchronized with a plain
/// lock: these are pure in-memory operations (not I/O), but concurrent hub calls from different
/// players can race on the same room's player dictionary.
/// </summary>
public sealed class Room
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, Player> _players = new();

    public RoomId Id { get; }
    public string Name { get; private set; }
    public DeckType DeckType { get; private set; }
    public RoundStatus Status { get; private set; } = RoundStatus.Voting;

    public Room(RoomId id, string name, DeckType deckType)
    {
        Id = id;
        Name = name;
        DeckType = deckType;
    }

    /// <summary>The first player to join an empty room becomes its host.</summary>
    public Player AddPlayer(string name, bool isSpectator, string? avatarUrl)
    {
        lock (_lock)
        {
            var isHost = _players.Count == 0;
            var player = new Player(Guid.NewGuid(), name, isSpectator, isHost, avatarUrl);
            _players[player.Id] = player;
            return player;
        }
    }

    /// <returns>True if the room has no players left, so the caller should delete it entirely.</returns>
    public bool RemovePlayer(Guid playerId)
    {
        lock (_lock)
        {
            _players.Remove(playerId);
            return _players.Count == 0;
        }
    }

    /// <summary>Only the host may remove another player, enforced here, not just as a hidden button.</summary>
    public void RemovePlayerAsHost(Guid hostPlayerId, Guid targetPlayerId)
    {
        lock (_lock)
        {
            if (!GetPlayerCore(hostPlayerId).IsHost)
            {
                throw new UnauthorizedAccessException("Only the room host can remove players.");
            }

            GetPlayerCore(targetPlayerId);
            _players.Remove(targetPlayerId);
        }
    }

    public void Rename(string newName)
    {
        lock (_lock)
        {
            Name = newName;
        }
    }

    public void SetPlayerName(Guid playerId, string newName)
    {
        lock (_lock)
        {
            GetPlayerCore(playerId).Name = newName;
        }
    }

    public void SetSpectator(Guid playerId, bool isSpectator)
    {
        lock (_lock)
        {
            var player = GetPlayerCore(playerId);
            player.IsSpectator = isSpectator;
            if (isSpectator)
            {
                player.PickedCardIndex = null;
            }
        }
    }

    public void PickCard(Guid playerId, int? cardIndex)
    {
        lock (_lock)
        {
            var player = GetPlayerCore(playerId);

            if (cardIndex is not null)
            {
                if (player.IsSpectator)
                {
                    throw new InvalidOperationException("Spectators cannot pick a card.");
                }

                var deck = DeckCatalog.Get(DeckType);
                if (cardIndex < 0 || cardIndex >= deck.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(cardIndex), cardIndex, "Card index is not valid for the current deck.");
                }
            }

            player.PickedCardIndex = cardIndex;
        }
    }

    /// <summary>
    /// True only when there is at least one non-spectator player and all of them have picked — an
    /// all-spectator room must not vacuously allow a reveal.
    /// </summary>
    public bool HasAllNonSpectatorsPicked()
    {
        lock (_lock)
        {
            return HasAllNonSpectatorsPickedCore();
        }
    }

    /// <summary>Only the host may reveal (see <see cref="AddPlayer"/>), enforced here, not just as a disabled button.</summary>
    public void Reveal(Guid playerId)
    {
        lock (_lock)
        {
            if (!GetPlayerCore(playerId).IsHost)
            {
                throw new UnauthorizedAccessException("Only the room host can reveal cards.");
            }

            if (!HasAllNonSpectatorsPickedCore())
            {
                throw new InvalidOperationException("Every non-spectator player must pick a card before revealing.");
            }

            Status = RoundStatus.Revealed;
        }
    }

    /// <summary>Only the host may reset (see <see cref="AddPlayer"/>), enforced here, not just as a hidden button.</summary>
    public void Reset(Guid playerId)
    {
        lock (_lock)
        {
            if (!GetPlayerCore(playerId).IsHost)
            {
                throw new UnauthorizedAccessException("Only the room host can reset the round.");
            }

            EndTurnCore();
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _players.Count == 0;
            }
        }
    }

    public Player GetPlayer(Guid playerId)
    {
        lock (_lock)
        {
            return GetPlayerCore(playerId);
        }
    }

    public IReadOnlyList<PlayerView> GetState()
    {
        lock (_lock)
        {
            var revealed = Status == RoundStatus.Revealed;
            return _players.Values
                .Select(p => new PlayerView(
                    p.Id,
                    p.Name,
                    p.IsSpectator,
                    p.IsHost,
                    p.AvatarUrl,
                    p.HasPicked,
                    revealed ? ResolveCard(p.PickedCardIndex) : null))
                .ToList();
        }
    }

    private bool HasAllNonSpectatorsPickedCore()
    {
        var hasNonSpectator = false;
        foreach (var player in _players.Values)
        {
            if (player.IsSpectator)
            {
                continue;
            }

            hasNonSpectator = true;
            if (!player.HasPicked)
            {
                return false;
            }
        }

        return hasNonSpectator;
    }

    private void EndTurnCore()
    {
        Status = RoundStatus.Voting;
        foreach (var player in _players.Values)
        {
            player.PickedCardIndex = null;
        }
    }

    private Player GetPlayerCore(Guid playerId)
    {
        if (!_players.TryGetValue(playerId, out var player))
        {
            throw new KeyNotFoundException($"Player '{playerId}' is not in this room.");
        }

        return player;
    }

    private CardOption? ResolveCard(int? index) =>
        index is null ? null : DeckCatalog.Get(DeckType)[index.Value];
}
