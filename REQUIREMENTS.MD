# Planning Poker — Requirements

## 0. Reference Implementation

Analyze the business logic of the archived project
[axeleroy/self-host-planning-poker](https://github.com/axeleroy/self-host-planning-poker)
(Flask + Flask-SocketIO + Angular + SQLite) and follow it here, with some
small, deliberate modifications rather than a 1:1 port. Before implementation
starts, extract and understand:

- Room/game lifecycle (create, join, rename, delete on empty)
- Deck types and their card values
- Player states (active / spectator, picked / not picked)
- Reveal → new-round (reset) cycle
- Error conditions (invalid deck, game not found, spectator can't play, etc.)

## 1. Tech Stack

.NET (ASP.NET Core Minimal API) + Blazor WebAssembly frontend, single
container image. One language (C#) end-to-end, no separate JS/npm toolchain —
the API serves the Blazor WASM build as static files and exposes real-time
game state over SignalR or raw WebSockets.

- The solution (`.sln`) file lives at the root level of the repository; every
  project (`.csproj`) lives under `src/`, one folder per project, split
  along dependency lines.

## 2. Developer Experience

- Must be debuggable from VS Code with F5 — a working `.vscode/launch.json`
  (+ `tasks.json` if a build step is needed first) is required.

## 3. DevOps / CI-CD

- Produce a versioned Docker image, following the CI pattern used in
  [GVLTodorov/Resume](https://github.com/GVLTodorov/Resume) (tests gate a
  version bump, which gates the image build/push).
- The pipeline must run the test suite (see [Section 8](#8-testing-strategy))
  before building/publishing the image.

## 4. Deployment

### 4.1 Reference — previous version's compose file

For context only, this is how the old (archived) app was deployed. Adapt this
shape for the new image/stack; do not reuse the image name.

```yaml
planningpoker:
  image: ghcr.io/rezahoque/planning-poker:latest
  container_name: planningpoker
  restart: unless-stopped
  ports:
    - 8140:8080
  volumes:
    - /srv/planningpoker:/app
  environment:
    - GIPHY_API_BASE_URL=https://api.giphy.com/v1/gifs/search?api_key=YOUR_GIPHY_API_KEY
    - GIPHY_API_QUERY=limit=5&offset=0&rating=g&lang=en&bundle=low_bandwidth
  labels:
    - 'traefik.enable=true'
    - 'traefik.http.routers.planningpoker.rule=Host(`poker.devspace.tech`)'
    - 'traefik.http.routers.planningpoker.entrypoints=https'
    - 'traefik.http.routers.planningpoker.tls.certresolver=myresolver'
  networks:
    - proxy
```

> **Note:** the real Giphy API key was shared out-of-band and must never be
> committed to this repo. Always use `YOUR_GIPHY_API_KEY` / an env var
> placeholder in any tracked file (Dockerfile, docker-compose.yml, README,
> CI workflows). The real key is only ever injected at deploy time as an
> environment variable.

### 4.2 Target `docker-compose.yml`

- Same two env vars: `GIPHY_API_BASE_URL`, `GIPHY_API_QUERY`.
- Image name/port adapted to the new .NET build (single container, serves
  API + Blazor WASM static files).
- Include as an example file in the repo with a placeholder key.

### 4.3 Operational endpoints

- `GET /healthz` — unauthenticated liveness check (`{"status":"ok"}`), for
  container orchestration / uptime monitoring. Not gated behind the `/api`
  prefix the room/deck/avatar endpoints use.

> **Giphy endpoint gotcha:** `GIPHY_API_BASE_URL` must point at
> `https://api.giphy.com/v1/gifs/trending`, not `.../search`. The `search`
> endpoint silently returns zero results (`HTTP 200`, `"data":[]`) without a
> `q=` search term, which `GIPHY_API_QUERY` doesn't supply — the avatar
> picker degrades to "No avatars available" exactly as if no key were
> configured at all, with no error to signal why. `trending` needs no search
> term and is what this app actually uses.

## 5. UX Workflows

### 5.1 Workflow A — Home Screen (create a room)

1. On load, the **room name** field is auto-populated with a friendly
   generated value: `<random word>-<short id>` (e.g. `clever-a3f9k2`), so the
   user doesn't have to think one up but can still edit it. The room's URL is
   a slug derived from this name (e.g. `/clever-a3f9k2`, or `/sprint-planning`
   for a custom name with spaces) rather than an opaque generated id. If the
   resulting slug is already taken by another active room, room creation
   fails with an error asking for a different name.
2. Keyboard focus starts on the **your name** field (the thing the user
   actually needs to type).
3. Show **5 random avatar images fetched from Giphy**, rendered as rounded
   square tiles (not circles), for the user to pick from as their player
   avatar. The **first avatar is selected by default**, and the currently
   selected avatar shows a **green highlight border** around it so the user
   can tell which one is picked. A **refresh button** next to the "Choose an
   avatar" label re-fetches a new random batch of 5 without touching
   anything else on the form.
4. A **spectator checkbox** ("Join as spectator (watch without picking
   cards)"), unchecked by default. Shared with the join flow — it lives in
   the same `PlayerSetupForm` component used by both screens, so the room
   creator can join as a spectator too (e.g. a facilitator who never
   estimates), not just people joining via link. See
   [5.4](#54-voting-round-lifecycle) for how a spectator's state differs
   once inside the room.
5. **Deck picker** is a dropdown; each option shows the deck's short label
   plus its actual values in brackets, e.g.:
   - `Fibonacci (0, 1, 2, 3, 5, 8, 13, 21, ...)`
   - `T-Shirt Sizes (XS, S, M, L, XL)`
   - `Powers of 2 (0, 1, 2, 4, 8, 16, 32, 64)`

   (Exact deck names/values confirmed against the reference repo per
   [Section 0](#0-reference-implementation).)

6. A checked, non-interactive **Host checkbox** — the room creator is always
   the host (see [Section 5.3](#53-host--room-ownership)); this just makes
   that visible, it isn't a toggle.
7. Once the room is created, its board screen header shows a copy-invite
   button next to the room name — an icon-only button (copy glyph, swapping
   to a checkmark once clicked; accessible via `aria-label`/`title` since
   there's no visible text) that copies the shareable room link to the
   clipboard. Visible to every player in the room, not just the host. The
   link is the bare room URL with no `/join` suffix — visiting it directly
   already shows the join screen when the visitor has no active session (see
   [5.6](#56-session-persistence-across-a-page-refresh)), so the suffix
   added nothing.
8. **Visual style:** green color palette; larger fonts, buttons, and spacing
   than a typical dense web app — optimized for readability/ease of use for
   older users.

### 5.2 Workflow B — Joining via a shared link

1. A second user opens the shared room URL.
2. Show **5 random avatar images from Giphy** to pick from, same as the
   create flow (rounded square tiles, first avatar selected by default,
   selected avatar shows a green highlight border, refresh button re-fetches
   a new batch).
3. Same **spectator checkbox** as the create flow (see
   [5.1](#51-workflow-a--home-screen-create-a-room) item 4).
4. **No Host checkbox is shown** — a player who joins via link is never the
   host (see [Section 5.3](#53-host--room-ownership)).
5. Keyboard focus starts on the **your name** textbox.
6. Pressing **Enter** submits the join (no separate button click required).

### 5.3 Host / room ownership

- The player who **creates** the room is its **host** (enforced server-side
  as "first player to join the room," which the creator always is — not a
  client-supplied flag, so it can't be spoofed by a later joiner).
- **Only the host can reveal or reset a round.** Non-host players see no
  Reveal/Reset button at all — just a "Waiting for the host to reveal/reset…"
  message — and attempting either as a non-host is also rejected
  server-side, not merely hidden client-side.
- A room's **deck is fixed at creation time and cannot be changed** once
  inside the room — the deck picker on the create screen is the only place
  it can be set. The board screen shows the deck name and values as
  read-only text, not a dropdown.
- **The host can remove other players from the room.** Each player card
  shows a small **X button in its top-right corner**, visible only to the
  host and only on other players' cards (never on the host's own card).
  Clicking it asks for **confirmation** before removing that player.
  Removal is enforced server-side (not just hidden client-side for
  non-hosts), and the removed player is disconnected and returned to the
  home screen with a notice that they were removed.

### 5.4 Voting round lifecycle

1. Players pick a card. Picked/not-picked state is visible to everyone;
   actual values stay hidden until reveal.
2. The **Reveal** action is only enabled for the **host**, and only once
   **every non-spectator player has picked a card**.
3. On reveal:
   - All hands become visible.
   - The **Reveal** button is replaced by a **Reset** button, visible only to
     the host, in the same spot.
4. Clicking **Reset** (host only):
   - Clears all picks and starts a new round.
   - The button reverts back to **Reveal** (disabled until everyone has
     picked again).
5. A **spectator** (see [5.1](#51-workflow-a--home-screen-create-a-room) item
   4) never sees a hand of cards to pick from at all — their player card
   shows a "Spectator" badge in place of a pick-state indicator, and they're
   excluded from the "everyone has picked" reveal gate in step 2. A
   spectator can still be the host (spectator and host are independent —
   e.g. a facilitator who never estimates but still drives reveal/reset).
6. A room isn't deleted the instant it empties. A page refresh briefly
   drops the only open connection before the reload's rejoin lands a moment
   later; deleting on the first disconnect would race that rejoin against
   the delete. The last player leaving starts a short grace window (server
   default: 15 seconds) before the room is actually removed, so a refresh
   mid-session doesn't destroy the room out from under the person doing it.

### 5.5 Player card appearance

- A player's **name is a label above their card**, not overlaid on the
  avatar image — always fully legible regardless of the avatar, and wraps
  onto a second line (reserving space for it up front, so cards in the same
  row stay aligned) rather than truncating a long name.
- Once chosen (in [5.1](#51-workflow-a--home-screen-create-a-room) /
  [5.2](#52-workflow-b--joining-via-a-shared-link)), a player's avatar gif is
  used as a low-opacity background image inside their player card on the
  board — it's decorative texture, not the focal point. Hovering a card
  brightens the avatar to full opacity and fades the pick-state content, so
  a player can see the avatar clearly without needing to click into it.
- Whatever's drawn over the avatar — the picked checkmark, the
  waiting-to-pick indicator, the revealed value, the spectator badge — sits
  on its own solid, semi-opaque backing (not bare text/shapes directly on
  the image), so it stays legible regardless of what the avatar looks like.
  This applies equally to the small pick-state indicators as to the larger
  revealed value; a plain-colored glyph with no backing is not legible
  enough against an arbitrary photo.

### 5.6 Session persistence across a page refresh

The identity a player picked on the create/join screen (name, avatar,
spectator flag) is mirrored into the browser's `sessionStorage`, not just
kept in memory. A full page refresh while on the board screen restores it
and rejoins the room automatically, instead of bouncing the player back to
the join screen and asking them to re-enter everything. Scoped to
`sessionStorage` (not `localStorage`) deliberately: it survives a refresh
within the same tab but doesn't linger indefinitely across unrelated future
visits once the tab closes.

### 5.7 Room/player rename (backend-only, no UI)

The hub supports renaming a room (`RenameRoom`) and renaming a player
in-room (`SetPlayerName`), and the client has wrapper methods for both
(`GameHubClient.RenameRoomAsync` / `SetPlayerNameAsync`). Neither is
currently exposed anywhere in the UI — there's no button or field that
calls them. This is a known gap, not a deliberate scope decision either
way; building the UI for one or both is a candidate for future work.

## 6. Giphy Integration

- Configured via `GIPHY_API_BASE_URL` and `GIPHY_API_QUERY` env vars (see
  [Section 4](#4-deployment)); the app must run fine with no visible Giphy
  features when unset.
- Used in one place: the **avatar picker** — 5 random avatars, refreshable on
  demand, on both the create-room screen
  ([5.1](#51-workflow-a--home-screen-create-a-room)) and the join screen
  ([5.2](#52-workflow-b--joining-via-a-shared-link)). There is no reveal-gif
  feature — the round-reveal step is Giphy-free.

## 7. Visual Design

- Primary color palette: green.
- Larger-than-default UI: bigger fonts, buttons, and touch targets; generous
  spacing — accessibility-minded, comfortable for older users.

## 8. Testing Strategy

- Everything must be unit testable:
  - Backend: domain/game logic (deck rules, reveal/consensus, room
    lifecycle), Giphy client (mocked HTTP).
  - Frontend: Blazor components (deck picker rendering, reveal/reset button
    state, avatar picker).
- **Integration tests** covering the real-time flow end-to-end (join → pick →
  reveal requires all-picked → reset), and the REST/SignalR API surface.
- The CI pipeline (see [Section 3](#3-devops--ci-cd)) must run both the unit
  and integration test suites and fail the build/version-bump/image-push on
  any failure.
- `PlanningPoker.Tests.Play` (and `PlanningPoker.Tests.Play.Twelve`, the same
  flow scaled to 12 players) drive the real UI through Playwright — join,
  pick, reveal, reset — and double as a live smoke test: every wait targets
  a specific element, so a stuck flow fails with a `TimeoutException`
  instead of silently producing a broken demo. Not gated in CI (same
  non-gating treatment as [Section 10.3](#103-performance--load-testing));
  triggered manually via the
  [Demo Video](.github/workflows/demo-video.yml) /
  [Demo Video (Twelve Players)](.github/workflows/demo-video-twelve.yml)
  workflows, which commit the regenerated `docs/demo.gif` /
  `docs/twelve.gif`.
- See [Section 10](#10-performance) for performance-specific tests.

## 9. Documentation & Branding

- `README.md` must be fully populated with real project documentation, not
  left as a placeholder title. At minimum cover: what the app is/does, a
  feature list, a quick-start / run guide (local dev + Docker), and the tech
  stack. Loosely follow the structure of a well-documented project README
  such as [github/spec-kit](https://github.com/github/spec-kit#-get-started)
  (table of contents, clear feature list, get-started steps) as inspiration,
  not a template to copy verbatim.
- The project must have a dedicated **SVG icon/logo**, checked into the repo,
  so the product has a real visual identity (used in the README header, and
  as the favicon / Blazor app icon). Style should reflect the green, friendly
  visual language described in [Section 7](#7-visual-design).

## 10. Performance

The app should be fast by construction, not just "tested for speed" after the
fact — apply the relevant .NET and frontend best practices below, then
measure them with real benchmarks/load tests.

### 10.1 Backend (.NET) technique checklist

- Async end-to-end; no sync-over-async blocking on the request/broadcast
  path.
- `IHttpClientFactory` with a pooled/reused `HttpClient` for the Giphy
  client (avoids socket exhaustion under load).
- `System.Text.Json` source-generated (de)serializers instead of reflection
  for the WebSocket/SignalR message envelope — this is the hottest path in
  the app (every pick/reveal fans out to every connected player).
- Short-lived `IMemoryCache` for Giphy responses so a burst of joins (avatar
  picker) doesn't hammer the Giphy API or its rate limit.
- Broadcast only the state diff a client needs, not the full room payload,
  where practical.
- Response compression (Brotli/Gzip) enabled for the static Blazor WASM
  assets — the WASM download is the single biggest perf cost in this app.
- Expected game-rule violations (spectator trying to pick, non-host trying
  to reveal/reset/remove a player, reveal attempted before everyone has
  picked, ...) are raised as built-in .NET exception types
  (`InvalidOperationException`, `UnauthorizedAccessException`,
  `ArgumentOutOfRangeException`, `KeyNotFoundException`), not a custom
  exception hierarchy — the domain layer stays dependency-free, and a single
  SignalR hub filter translates those specific types into a client-visible
  `HubException`. This is a project-level decision, not a general
  recommendation: it trades a small risk (an unrelated bug inside a hub
  method throwing one of these same built-in types would also get forwarded
  to the client) for not maintaining a parallel exception hierarchy whose
  only real job was letting tests assert on distinct types.

### 10.2 Frontend (Blazor WASM) technique checklist

- Trim/AOT tradeoff evaluated deliberately for publish (`PublishTrimmed`
  shrinks download size; AOT trades size for runtime speed) — pick based on
  measured startup time vs. bundle size, don't cargo-cult one setting.
- Browser/CDN caching (`Cache-Control`, content-hashed filenames) so repeat
  visits skip re-downloading the WASM runtime.
- Scope component re-renders (e.g. `ShouldRender()`, splitting the player
  list into per-card components) so one player picking a card doesn't
  re-render the whole board.

### 10.3 Performance / load testing

- Micro-benchmarks (e.g. BenchmarkDotNet) for hot paths: game-state
  mutation, broadcast serialization, Giphy client call. The CI benchmarks job
  runs these with a shortened job (`--job short` — trades statistical
  precision for a runtime that fits a shared runner) and publishes each
  class's results table straight to the GitHub Actions job summary, plus the
  full CSV/HTML/markdown artifacts, so results are visible on the run page
  without digging into logs.
- A load/soak test simulating many concurrent rooms and players
  picking/revealing rapidly — measure broadcast latency and CPU/memory
  under load, not just correctness. Implemented as two separate tools, each
  runnable manually against a live instance:
  - `PlanningPoker.Tests.LoadTest` — N rooms x M players hammering
    pick/reveal/reset as fast as possible over real SignalR connections,
    reporting the `PickCard` round trip's p50/p95/p99/max latency.
  - `PlanningPoker.Tests.Play.Hundred` — 10 rooms x 10 players (100 real
    SignalR connections, no browsers) voting at a human pace (each room's
    round takes 15-20 wall-clock seconds, picks staggered rather than
    simultaneous) while sampling `PlanningPoker.Api`'s own CPU% and
    working-set memory and rendering the series as an SVG chart. Triggered
    manually via the [Demo Hundred workflow](.github/workflows/demo-hundred.yml),
    which commits the refreshed chart to `docs/hundred-resource-usage.svg`
    (embedded in the README's Performance section).
- Track the published Blazor WASM bundle size and flag/fail on regressions
  past a set threshold, since download size is the primary perf metric
  users actually feel.
- These don't need to gate every commit the way unit/integration tests do,
  but must exist as runnable, repeatable pipeline steps (see
  [Section 3](#3-devops--ci-cd)).
