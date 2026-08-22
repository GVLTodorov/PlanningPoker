---
name: frontend-conventions
description: CSS/Blazor conventions for PlanningPoker.Client — design tokens, the avatar-background/content-overlay pattern, contrast rules for overlaid indicators, name-label layout, and JS interop. Use whenever adding or editing a Blazor component, styling anything in app.css, or writing a bUnit component test.
---

# Frontend Conventions

Established over this project's UI work. These aren't defaults the Blazor/CSS ecosystem hands you
— they're decisions made (and in a couple of cases, corrected after a bug) in this repo
specifically.

## Design tokens — never a literal color or px value

Every color and spacing value comes from a `:root` custom property in
`PlanningPoker.Client/wwwroot/css/app.css`:

```css
--color-green-50/100/200/400/600/700/800/900   /* the palette, light to dark */
--color-surface, --color-surface-muted
--color-text, --color-text-muted, --color-danger
--space-1 (0.5rem) through --space-5 (2rem)
--radius (0.75rem), --touch-target (3rem)
```

New UI work reuses these (`var(--color-green-600)`, `var(--space-3)`, ...) rather than introducing
a new literal value. If a needed shade genuinely doesn't exist yet, add it to `:root` as a new
token — don't inline it once and call it done.

## No scoped `.razor.css` — one shared `app.css`

This repo deliberately has zero component-scoped stylesheets. All styling lives in
`wwwroot/css/app.css`, referenced once from `index.html`. (A leftover `<link>` to the
CSS-isolation bundle that Blazor never generates here — because nothing uses scoped CSS — was a
real, live 404 in this app; it's gone now.) Keep it that way: new component styles go into
`app.css` under a comment/section for that component, not into a new `ComponentName.razor.css`.

## The avatar background + content overlay pattern

Any card that shows a player avatar behind foreground content (currently `PlayerCard.razor`) uses
two layered elements, not one:

```html
<div class="player-card">
  <div class="player-card-background" style="background-image:url('...')"></div>
  <div class="player-card-content"> ... </div>
</div>
```

- `.player-card-background`: `position: absolute; inset: 0;`, low default opacity (currently
  `0.3`) so it doesn't fight the foreground.
- On hover (only when a background image is actually present — gate with
  `.player-card:has(.player-card-background):hover`), the background brightens and the content
  fades:
  ```css
  .player-card:has(.player-card-background):hover .player-card-background { opacity: 1; }
  .player-card:has(.player-card-background):hover .player-card-content { opacity: 0.25; }
  ```
- Never apply opacity to the whole card — that fades the background image and the readable content
  together, defeating the point. This was an actual bug caught by a bUnit test
  (`RendersBackgroundLayer_SeparateFromNameText_WhenAvatarPresent`): the background must be its own
  layer, never a filter on a shared ancestor.

## Overlaid indicators need their own contrast backing

Anything drawn directly over the avatar background (the picked-checkmark, the waiting-circle, the
revealed card value, the spectator badge) needs a solid backing of its own — a semi-opaque white
pill/disc with a drop shadow — because it has to stay legible against an arbitrary photo, not just
the flat surface color:

```css
background: rgba(255, 255, 255, 0.85-0.92);
box-shadow: 0 1px 3px rgba(13, 63, 32, 0.35);  /* only needed for small round indicators */
```

This was a real, reported bug: the waiting-circle and checkmark originally had no backing and were
"barely visible" against a photo avatar, while the value/badge text already had the white-pill
treatment. Any new overlay element follows the value/badge/indicator precedent, not a bare-text
default.

## Name labels: never truncate with ellipsis — wrap and reserve height

Player/room names are user input of unpredictable length. The fix for a name getting cut off
(`Georgi Todor…`) was not a smaller font alone — it's `-webkit-line-clamp: 2` (plus the standard
`line-clamp: 2`) with a `min-height` sized for two lines, so:

- A short name doesn't truncate.
- A genuinely long name wraps to a second line instead of being cut off.
- Every card in a row still aligns, because the name area's height is reserved up front rather than
  driven by however many lines that particular name happens to need.

Reach for this pattern any time a user-supplied string sits above/inside a fixed-width card.

## Component structure

- One Blazor component per `.razor` file; `@code` block at the bottom, not a separate
  `.razor.cs` partial.
- Parameters: `[Parameter, EditorRequired]` for anything the component can't function without,
  plain `[Parameter]` otherwise. Parent/child communication via `EventCallback`/`EventCallback<T>`,
  not two-way binding hacks.
- Grid layouts for card collections use `repeat(auto-fill, minmax(<min>, 1fr))` so the count of
  visible cards adapts to viewport width without a fixed column count.

## JS interop

- Import the module once per component instance:
  `await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/interop.js")`, cache the
  `IJSObjectReference`, then call functions on it.
- **bUnit gotcha**: module-scoped JS calls must be stubbed with
  `JSInterop.SetupModule(path).Setup<T>(...)`, not the bare `JSInterop.Setup<T>(...)` — the latter
  only matches calls made without the `import` indirection and silently fails to match here. This
  cost real debugging time once; don't repeat it.
