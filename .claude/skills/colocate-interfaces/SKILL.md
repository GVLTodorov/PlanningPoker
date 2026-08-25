---
name: colocate-interfaces
description: C# file-organization convention for this repo — when to put an interface in the same file as its implementation vs. keep it standalone. Use whenever adding a new interface, a new class implementing an existing interface, or reviewing/refactoring existing service and repository files in PlanningPoker.
---

# Colocate Interfaces

Reverse-engineered from [GVLTodorov/Telegram](https://github.com/GVLTodorov/Telegram), a sibling
.NET project by the same author, and adopted here for the same reason: fewer files to jump between
for a service that only ever has one shape.

## The rule

**Single implementer → same file.** If an interface exists solely to let one class be abstracted
for DI/testability, and exactly one class implements it, define the interface in the *same* `.cs`
file as the class — placed *after* the class body, not before it.

**Multiple implementers → standalone file.** If an interface is a real shared abstraction with two
or more implementers (e.g. a strategy/plugin-style contract), it stays in its own file. Do not
merge it into any one implementer's file — there's no single "owning" file for it.

Before merging or splitting, grep for implementers:

```bash
grep -rn ": IYourInterface" src/
```

- 1 match → colocate.
- 2+ matches → leave standalone (or split it back out if you find it merged into one implementer's
  file by mistake).

## File shape when colocating

```csharp
using SomeNamespace;

namespace PlanningPoker.SomeArea;

public sealed class ThingService : IThingService
{
    private readonly IDependency _dependency;

    public ThingService(IDependency dependency)
    {
        _dependency = dependency;
    }

    public Task DoWorkAsync() => _dependency.RunAsync();
}

public interface IThingService
{
    Task DoWorkAsync();
}
```

- Class first, interface after.
- File-scoped namespace (`namespace X;`), not block-scoped.
- Standard constructor injection with `_camelCase` readonly private fields — no primary
  constructors.
- XML doc comments (when the type warrants one) go on the interface declaration as a short summary,
  not repeated per member and not also duplicated on the class.

## Precedent in this repo

- `InMemoryRoomRepository.cs` — merged from a standalone `IRoomRepository.cs` (single implementer).
- `PlayerTracker.cs` — merged from a standalone `IPlayerTracker.cs` (single implementer).
- `IGiphyClient.cs` — deliberately **left standalone**: both `GiphyClient` and `NullGiphyClient`
  implement it, so there's no single file it belongs in. This mirrors Telegram's own
  `Service/Extension/IService.cs`, which is implemented by ~10 different command services and is
  never merged into any of them.

When adding a new interface, check the implementer count first — most of the time in this codebase
it'll be one, but don't assume it without checking.
