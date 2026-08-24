---
name: contracts-naming
description: Naming convention for wire model types in PlanningPoker.Infrastructure/Contracts — Request/Response suffixes, not "Dto". Use whenever adding, renaming, or reviewing a type under PlanningPoker.Contracts (REST bodies, SignalR hub results, or any nested field type inside one).
---

# Contracts Naming

This repo used to suffix every wire model with `Dto` (`PlayerDto`, `RoomStateDto`, `JoinRoomResult`,
...). That's gone — everything in `PlanningPoker.Infrastructure/Contracts` now ends in `Request` or
`Response` instead.

## The rule

**Every type in `PlanningPoker.Contracts` gets `Request` or `Response`, uniformly** — including
types that are only ever a *nested field* inside a larger response, never a response body
themselves.

```csharp
public sealed record RoomStateResponse(
    string RoomId, string Name, DeckType DeckType, RoundStatus Status,
    IReadOnlyList<PlayerResponse> Players);   // PlayerResponse is never returned on its own,
                                               // still gets the suffix
```

`PlayerResponse` and `CardOptionResponse` are never the top-level body of any endpoint — they only
ever show up nested (`RoomStateResponse.Players`, `PlayerResponse.Card`, `DeckResponse.Cards`). The
"purer" alternative would drop the suffix on nested-only types and leave them bare (`Player`,
`CardOption`). Don't do that here — see [why](#why-uniform-not-just-real-bodies) below.

## Current mapping

| Old (`Dto`/bespoke) | Current |
|---|---|
| `CardOptionDto` | `CardOptionResponse` |
| `DeckDto` | `DeckResponse` |
| `PlayerDto` | `PlayerResponse` |
| `RoomStateDto` | `RoomStateResponse` |
| `RoomSummaryDto` | `RoomSummaryResponse` |
| `JoinRoomResult` | `JoinRoomResponse` |
| `Room.ToStateDto()` / `.ToSummaryDto()` | `.ToStateResponse()` / `.ToSummaryResponse()` |

`CreateRoomRequest` and `RoomNameSuggestionResponse` were already named correctly — no change.

## Why uniform, not just real bodies

The Domain layer already has its own `Player` (`PlanningPoker.Domain.Rooms.Player`) and
`CardOption` (`PlanningPoker.Domain.Decks.CardOption`). `ContractExtensions.cs` maps between the two
layers in the same file, and already has to alias-import the domain side to keep them apart:

```csharp
using DomainCardOption = PlanningPoker.Domain.Decks.CardOption;
using DomainPlayerView = PlanningPoker.Domain.Rooms.PlayerView;
```

Naming the contract side `CardOptionResponse`/`PlayerResponse` — instead of bare `CardOption`/
`Player` — is what keeps `ContractExtensions.cs` (and anywhere else that touches both layers) from
needing an alias on *both* sides. Bare nested-type names would just move the collision, not remove
it. If you're tempted to drop the suffix on a "just a field, not really a response" type, check
whether the Domain layer has a same-named class first — it usually does.

## What's exempt

**SignalR push/event messages are not part of this convention.** `PlayerPickStatusChanged` and
`RoundRevealed` (`PlanningPoker.Contracts.Messages`) are broadcasts — nothing "requests" them, so
neither `Request` nor `Response` fits. Leave them named for the event they represent, not renamed
to `*Response`. `JoinRoomResponse` is the one type in `Contracts/Messages` that *is* Response-suffixed,
because it's a real hub RPC return value (the result of calling `JoinRoom`), not a broadcast — don't
use its presence in that folder as precedent for renaming the other two.

**The `Requests` folder holds both Requests and Responses.** `RoomNameSuggestionResponse` lives in
`Contracts/Requests` alongside `CreateRoomRequest` — that's intentional, not a misfile. Don't "fix"
it into a separate `Contracts/Responses` folder without checking with whoever's asking; the
folder split by request-vs-response was never part of this convention, only the type suffix was.

**The Domain layer is untouched.** `PlanningPoker.Infrastructure/Domain/...` keeps plain names
(`Room`, `Player`, `CardOption`, `PlayerView`) with no `Request`/`Response` suffix at all — those
aren't wire models, they're the domain's own vocabulary. This convention applies only to
`PlanningPoker.Contracts`.

## Domain → Contract mapping methods: name each conversion, don't overload `ToContract()`

`ContractExtensions.cs` (`PlanningPoker.Api/Mapping/`) used to have four overloads of a single
generic `ToContract()` name, distinguished only by parameter type — readable at the call site
(`x.ToContract()`) but not at the declaration, and easy to reach for the wrong overload by accident.
Each conversion now has its own specific name instead:

| Converts | Method |
|---|---|
| Domain `DeckType` → contract `DeckType` | `ToDeckType()` |
| Domain `RoundStatus` → contract `RoundStatus` | `ToRoundStatus()` |
| Domain `CardOption` → `CardOptionResponse` | `ToCardOptionResponse()` |
| Domain `PlayerView` → `PlayerResponse` | `ToPlayerResponse()` |

Follow this for any new Domain → Contract conversion: name the extension method after what it
*returns*, not a generic `ToContract`/`ToDto`/`ToModel`. The reverse direction (contract → domain,
currently just `ToDomain(this DeckType)`) stays generic only because there's exactly one overload of
it — if a second contract → domain conversion is ever added, split both into specific names then too.
