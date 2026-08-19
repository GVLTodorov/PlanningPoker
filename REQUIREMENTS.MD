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

In addition, [Section 5](#5-ux-workflows) and [Sections 8–10](#8-estimation-and-consensus-rules)
incorporate specific patterns pulled from a survey of other open-source
planning poker tools (Poinz, PokeR, AgileKit, Thunderdome,
PlanningPoker4Azure, PokerTime, hellomuthu23/planning-poker) — each such
addition is attributed inline so its origin/rationale stays traceable.

## 1. Tech Stack

.NET (ASP.NET Core Minimal API) + Blazor WebAssembly frontend, single
container image. One language (C#) end-to-end, no separate JS/npm toolchain —
the API serves the Blazor WASM build as static files and exposes real-time
game state over SignalR or raw WebSockets.

## 2. Developer Experience

- Must be debuggable from VS Code with F5 — a working `.vscode/launch.json`
  (+ `tasks.json` if a build step is needed first) is required.

## 3. DevOps / CI-CD

- Produce a versioned Docker image, following the CI pattern used in
  [GVLTodorov/Resume](https://github.com/GVLTodorov/Resume) (tests gate a
  version bump, which gates the image build/push).
- The pipeline must run the test suite (see [Section 11](#11-testing-strategy))
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

## 5. UX Workflows

### 5.1 Workflow A — Home Screen (create a room)

1. **Three ways into a room** (pattern from Poinz): a prominent **"Create
   Room"** button using the auto-generated name below, an inline toggle to
   type a **custom room name** instead (creates it if it doesn't exist, joins
   it if it does), or landing directly on a room URL that doesn't exist yet —
   which silently creates it. All three converge on the same room instead of
   a "not found" error.
2. On load, the **room name** field is auto-populated with a friendly
   generated value: `<random word>-<short id>` (e.g. `clever-a3f9k2`), so the
   user doesn't have to think one up but can still edit it.
3. Keyboard focus starts on the **your name** field (the thing the user
   actually needs to type).
4. Show **3 random avatar images fetched from Giphy** for the user to pick
   from as their player avatar.
5. **Deck picker** is a dropdown; each option shows the deck's short label
   plus its actual values in brackets, e.g.:
   - `Fibonacci (0, 1, 2, 3, 5, 8, 13, 21, ...)`
   - `T-Shirt Sizes (XS, S, M, L, XL)`
   - `Powers of 2 (0, 1, 2, 4, 8, 16, 32, 64)`

   (Exact deck names/values confirmed against the reference repo per
   [Section 0](#0-reference-implementation).)

6. Copy-URL button — copies the shareable room link to the clipboard.
7. **Visual style:** green color palette; larger fonts, buttons, and spacing
   than a typical dense web app — optimized for readability/ease of use for
   older users (see [Section 7](#7-visual-design)).

### 5.2 Workflow B — Joining via a shared link

1. A second user opens the shared room URL.
2. Show **3 random avatar images from Giphy** to pick from, same as the
   create flow.
3. Keyboard focus starts on the **your name** textbox.
4. If this browser has a previously-used display name saved (see below), the
   name field is pre-filled with it — still editable, still focused, so a
   returning user can just hit Enter (pattern from Poinz).
5. Pressing **Enter** submits the join (no separate button click required);
   on success the chosen name is saved to `localStorage` for next time.

### 5.3 Voting round lifecycle

1. Players pick a card. Picked/not-picked state is visible to everyone;
   actual values stay hidden until reveal.
2. The **Reveal** action is only enabled once **every non-spectator player
   has picked a card**.
3. Once the last required player picks, the round **auto-reveals after a
   short countdown** (~2–3s) instead of requiring an extra click (pattern
   from AgileKit's "Auto-Complete Voting"). The facilitator (see
   [5.5](#55-room-roles-and-facilitation)) can additionally set a hard voting
   deadline that force-reveals the round even if some players haven't picked
   (pattern from PokeR).
4. On reveal:
   - All hands become visible.
   - The **Reveal** button is replaced by a **Reset** button in the same
     spot.
   - A **random Giphy gif** appears on the right side of the screen (see
     [Section 6](#6-giphy-integration)).
   - If every revealed hand matches exactly, also fire a quick client-side
     celebration animation (e.g. confetti) alongside the gif — a fast,
     Giphy-independent consensus payoff (pattern from PokeR; see
     [Section 6](#6-giphy-integration)).
5. The facilitator can **cancel an in-progress round** and reopen picking
   without a full reset — useful when discussion needs to continue before
   anyone commits (pattern from PlanningPoker4Azure).
6. Clicking **Reset**:
   - Clears all picks and starts a new round.
   - Clears the gif panel.
   - The button reverts back to **Reveal** (disabled until everyone has
     picked again).
7. If a player changes their pick before reveal, animate the swap (old card
   visibly withdrawn, new one played) rather than silently replacing it, so
   the change reads as intentional feedback, not a glitch (pattern from
   PokeR).

### 5.4 Player card appearance

- Once chosen (in [5.1](#51-workflow-a--home-screen-create-a-room) /
  [5.2](#52-workflow-b--joining-via-a-shared-link)), a player's avatar gif is
  used as a low-opacity background image inside their player card on the
  board — it's decorative texture, not the focal point.
- The player's **name is the focal point**: rendered on top in high-contrast,
  bold, readable text (solid/opaque text color, sized and weighted so it
  stays legible regardless of what the background gif looks like — no
  low-contrast text over busy backgrounds).
- Picked/not-picked state is shown as a clear per-player emoji badge (e.g.
  👍 picked / 🤔 waiting) rather than a subtle dot or checkmark (pattern
  from hellomuthu23/planning-poker).
- A player who disconnects (closes their tab without leaving) is visually
  marked **"disconnected"** instead of silently vanishing from the board
  (pattern from Poinz; see [Section 10](#10-data-sessions-and-reliability)
  for the reconnect grace period).

### 5.5 Room roles and facilitation

- The first player to join a room becomes its **facilitator** (host).
  Facilitator status is transferable to another player in the room at any
  time (pattern from PokeR).
- Facilitator-only actions: force an early reveal, set a voting deadline,
  cancel/restart a round, remove ("kick") a player, rename the room, change
  the deck.
- The **spectator** toggle remains available to any player (existing domain
  concept from the reference repo, [Section 0](#0-reference-implementation))
  — spectators are excluded from the "everyone has picked" reveal gate in
  [5.3](#53-voting-round-lifecycle).
- Optional **room password**, with a separate **facilitator code** so a room
  link can be shared read/write with the whole team while the facilitator
  role requires a second, separately-shared code (pattern from
  Thunderdome / PokerTime). Off by default — only relevant if a password is
  explicitly set when creating the room.

## 6. Giphy Integration

- Configured via `GIPHY_API_BASE_URL` and `GIPHY_API_QUERY` env vars (see
  [Section 4](#4-deployment)); the app must run fine with no visible Giphy
  features when unset.
- Used in two places:
  1. **Avatar picker** — 3 random avatars on both the create-room screen
     ([5.1](#51-workflow-a--home-screen-create-a-room)) and the join screen
     ([5.2](#52-workflow-b--joining-via-a-shared-link)).
  2. **Reveal gif** — one random gif shown alongside the revealed hands
     ([5.3](#53-voting-round-lifecycle)), cleared on reset.
- **Consensus confetti (optional, non-Giphy):** a lightweight, purely
  client-side celebration animation fired when a round's revealed hands all
  match exactly — complements the Giphy gif rather than replacing it, and
  works even if no Giphy key is configured (pattern from PokeR).

## 7. Visual Design

- Primary color palette: green, with both a light and dark variant. Theme
  follows the OS/browser preference by default, with a manual toggle
  override (pattern from PokeR, AgileKit, hellomuthu23/planning-poker).
- Larger-than-default UI: bigger fonts, buttons, and touch targets; generous
  spacing — accessibility-minded, comfortable for older users.
- Keep state-change animations (vote swap, reveal, reset from
  [5.3](#53-voting-round-lifecycle)) quick and subtle — enough to
  communicate what happened without feeling gimmicky, consistent with the
  "easy for older people" goal above.

## 8. Estimation and Consensus Rules

- **Point-average rounding mode:** when displaying a summary/average of
  revealed picks, round using a configurable mode — ceiling (default),
  nearest, or floor (pattern from Thunderdome).
- **Consensus detection:** all non-spectator hands match exactly → triggers
  the celebration treatment in [5.3](#53-voting-round-lifecycle) /
  [6](#6-giphy-integration).
- **Confidence tagging (stretch):** optionally let a player flag their pick
  as "unsure" alongside its numeric value (pattern from Poinz) — purely
  informational, surfaced to the facilitator, never blocking.

## 9. Story Backlog (stretch goal, post-MVP)

Bigger-scope features seen across mature tools in the survey — build these
after the core workflow ([Section 5](#5-ux-workflows),
[Section 6](#6-giphy-integration), [Section 8](#8-estimation-and-consensus-rules))
is solid, not before:

- A **story queue**: the facilitator adds a short list of items (title +
  optional link/description) and steps through them one at a time, each
  getting its own voting round (pattern from Thunderdome, Poinz).
- **CSV import/export** of stories and their estimation results (picks,
  average, consensus) (pattern from AgileKit).
- External issue-tracker sync (Jira, Azure DevOps, GitHub, GitLab) is
  explicitly **out of scope**. Several surveyed tools (Diveni, Thunderdome's
  premium tier) build entire OAuth/webhook integrations around this — it's a
  project of its own and must not gate the MVP.

## 10. Data, Sessions and Reliability

- Room metadata persists across a server restart (existing decision, see
  [Section 0](#0-reference-implementation)), extended with a configurable
  **room expiration**: delete a room after N days of inactivity so storage
  doesn't grow unbounded (pattern from AgileKit, Thunderdome).
- **Client reconnect handling:** a brief network drop should not eject a
  player from the room or lose their current pick — reconnecting within a
  grace period resumes state silently. A player who stays disconnected past
  the grace period shows the "disconnected" marker from
  [5.4](#54-player-card-appearance) (pattern from PlanningPoker4Azure).

## 11. Testing Strategy

- Everything must be unit testable:
  - Backend: domain/game logic (deck rules, reveal/consensus, room
    lifecycle), Giphy client (mocked HTTP).
  - Frontend: Blazor components (deck picker rendering, reveal/reset button
    state, avatar picker).
- **Integration tests** covering the real-time flow end-to-end (join → pick →
  reveal requires all-picked → reset), and the REST/SignalR API surface.
- **Authorization tests** for facilitator-only actions
  ([5.5](#55-room-roles-and-facilitation)) — a non-facilitator attempting an
  early reveal, kick, deadline change, or round cancel must be rejected.
- The CI pipeline (see [Section 3](#3-devops--ci-cd)) must run both the unit
  and integration test suites and fail the build/version-bump/image-push on
  any failure.
- See [Section 12](#12-performance) for performance-specific tests.

## 12. Performance

The app should be fast by construction, not just "tested for speed" after the
fact — apply the relevant .NET and frontend best practices below, then
measure them with real benchmarks/load tests.

### 12.1 Backend (.NET) technique checklist

- Async end-to-end; no sync-over-async blocking on the request/broadcast
  path.
- `IHttpClientFactory` with a pooled/reused `HttpClient` for the Giphy
  client (avoids socket exhaustion under load).
- `System.Text.Json` source-generated (de)serializers instead of reflection
  for the WebSocket/SignalR message envelope — this is the hottest path in
  the app (every pick/reveal fans out to every connected player).
- Short-lived `IMemoryCache` for Giphy responses so a burst of reveals/joins
  doesn't hammer the Giphy API or its rate limit.
- Broadcast only the state diff a client needs, not the full room payload,
  where practical.
- Response compression (Brotli/Gzip) enabled for the static Blazor WASM
  assets — the WASM download is the single biggest perf cost in this app.

### 12.2 Frontend (Blazor WASM) technique checklist

- Trim/AOT tradeoff evaluated deliberately for publish (`PublishTrimmed`
  shrinks download size; AOT trades size for runtime speed) — pick based on
  measured startup time vs. bundle size, don't cargo-cult one setting.
- Browser/CDN caching (`Cache-Control`, content-hashed filenames) so repeat
  visits skip re-downloading the WASM runtime.
- Scope component re-renders (e.g. `ShouldRender()`, splitting the player
  list into per-card components) so one player picking a card doesn't
  re-render the whole board.

### 12.3 Performance / load testing

- Micro-benchmarks (e.g. BenchmarkDotNet) for hot paths: game-state
  mutation, broadcast serialization, Giphy client call.
- A load/soak test simulating many concurrent rooms and players
  picking/revealing rapidly — measure broadcast latency and CPU/memory
  under load, not just correctness.
- Track the published Blazor WASM bundle size and flag/fail on regressions
  past a set threshold, since download size is the primary perf metric
  users actually feel.
- These don't need to gate every commit the way unit/integration tests do,
  but must exist as runnable, repeatable pipeline steps (see
  [Section 3](#3-devops--ci-cd)).
