---
name: concise-naming
description: Prefer short, mostly-two-word names for classes/interfaces over longer compound names that just chain qualifiers together. Use whenever naming or renaming a class, interface, or service in this repo.
---

# Concise Naming

## The rule

Class and interface names should read as **the most natural, logical name for the concept** —
usually two words, occasionally three when a third word is truly load-bearing. Don't default to
chaining every qualifier that technically applies just because each one is individually accurate.

**Example**: `PlayerTracker` (`PlanningPoker.Api/Services/`) tracks which SignalR connection belongs
to which player — it was originally `PlayerConnectionTracker`. `Connection` was technically
accurate (it's a `ConnectionId`-keyed lookup), but `PlayerTracker` is the more logical name: callers
care about *players* (`TryGetConnectionId(roomId, playerId, ...)`, `Track(connectionId, roomId,
playerId)`), and "tracker" already implies something connection/session-shaped without needing to
spell it out.

| Longer (avoid) | Shorter (prefer) | Why |
|---|---|---|
| `PlayerConnectionTracker` | `PlayerTracker` | Callers key on player identity; "tracker" already implies the connection/session angle |

## How to apply

- When naming something new, write the two-word version first (`{Subject}{Role}` — e.g.
  `PlayerTracker`, `RoomRepository`, `GiphyClient`) and only add a third word if dropping it would
  make the name ambiguous or collide with an existing type.
- When reviewing an existing name that chains multiple qualifiers (`XyzAbcThing`), ask which word is
  load-bearing and which is redundant with what the class already implies through its members/usage
  — drop the redundant one.
- This is a judgment call, not a mechanical word-count rule: `GameHubTimingOptions` vs. `TimingOptions`
  vs. `HubTimingOptions` all technically fit "two-ish words" differently — pick whichever reads most
  naturally to someone calling it, not whichever is shortest in isolation.
- Don't rename existing types just to satisfy this convention on sight — apply it when a class is
  already being touched (added, renamed for another reason, or reviewed) rather than as a drive-by
  churn pass.
