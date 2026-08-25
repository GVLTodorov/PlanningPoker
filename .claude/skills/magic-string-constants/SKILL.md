---
name: magic-string-constants
description: Whenever a magic string or magic number literal shows up in C# code, move it into a static Constants.cs class instead of leaving it inline. Use whenever writing or reviewing C# code with a hardcoded string or number literal.
---

# Magic String/Number Constants

Whenever you write or come across a magic string or magic number in C# code, move it into a
`Constants.cs` file as a `public const` field on a `public static class`, and reference the constant
instead of the inline literal.

```csharp
namespace PlanningPoker.Contracts;

public static class Constants
{
    public const string RoomStateChangedEvent = "RoomStateChanged";
    public const int MaxPlayersPerRoom = 20;
}
```

File-scoped namespace, `public static class`, `public const` fields — matches the `DeckCatalog`
pattern already in this repo. Put `Constants.cs` in the project closest to where the value is used;
if it's shared across projects (e.g. `PlanningPoker.Api` and `PlanningPoker.Client`), put it in
`PlanningPoker.Infrastructure` so both can reference it.
