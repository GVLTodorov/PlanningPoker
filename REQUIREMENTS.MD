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

- The solution (`.sln`) and project (`.csproj`) files must live at the root
  level of the repository, not nested inside an extra `src/` (or similar)
  subdirectory.

## 2. Developer Experience

- Must be debuggable from VS Code with F5 — a working `.vscode/launch.json`
  (+ `tasks.json` if a build step is needed first) is required.

## 3. DevOps / CI-CD

- Produce a versioned Docker image, following the CI pattern used in
  [GVLTodorov/Resume](https://github.com/GVLTodorov/Resume) (tests gate a
  version bump, which gates the image build/push).
- The pipeline must run the test suite (see [Section 9](#9-testing-strategy))
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

1. On load, the **room name** field is auto-populated with a friendly
   generated value: `<random word>-<short id>` (e.g. `clever-a3f9k2`), so the
   user doesn't have to think one up but can still edit it.
2. Keyboard focus starts on the **your name** field (the thing the user
   actually needs to type).
3. Show **3 random avatar images fetched from Giphy** for the user to pick
   from as their player avatar.
4. **Deck picker** is a dropdown; each option shows the deck's short label
   plus its actual values in brackets, e.g.:
   - `Fibonacci (0, 1, 2, 3, 5, 8, 13, 21, ...)`
   - `T-Shirt Sizes (XS, S, M, L, XL)`
   - `Powers of 2 (0, 1, 2, 4, 8, 16, 32, 64)`

   (Exact deck names/values confirmed against the reference repo per
   [Section 0](#0-reference-implementation).)

5. Copy-URL button — copies the shareable room link to the clipboard.
6. **Visual style:** green color palette; larger fonts, buttons, and spacing
   than a typical dense web app — optimized for readability/ease of use for
   older users.

### 5.2 Workflow B — Joining via a shared link

1. A second user opens the shared room URL.
2. Show **3 random avatar images from Giphy** to pick from, same as the
   create flow.
3. Keyboard focus starts on the **your name** textbox.
4. Pressing **Enter** submits the join (no separate button click required).

### 5.3 Voting round lifecycle

1. Players pick a card. Picked/not-picked state is visible to everyone;
   actual values stay hidden until reveal.
2. The **Reveal** action is only enabled once **every non-spectator player
   has picked a card**.
3. On reveal:
   - All hands become visible.
   - The **Reveal** button is replaced by a **Reset** button in the same
     spot.
   - A **random Giphy gif** appears on the right side of the screen (see
     [Section 8](#8-giphy-integration)).
4. Clicking **Reset**:
   - Clears all picks and starts a new round.
   - Clears the gif panel.
   - The button reverts back to **Reveal** (disabled until everyone has
     picked again).

### 5.4 Player card appearance

- Once chosen (in [5.1](#51-workflow-a--home-screen-create-a-room) /
  [5.2](#52-workflow-b--joining-via-a-shared-link)), a player's avatar gif is
  used as a low-opacity background image inside their player card on the
  board — it's decorative texture, not the focal point.
- The player's **name is the focal point**: rendered on top in high-contrast,
  bold, readable text (solid/opaque text color, sized and weighted so it
  stays legible regardless of what the background gif looks like — no
  low-contrast text over busy backgrounds).

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
- See [Section 10](#10-performance) for performance-specific tests.

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
- Short-lived `IMemoryCache` for Giphy responses so a burst of reveals/joins
  doesn't hammer the Giphy API or its rate limit.
- Broadcast only the state diff a client needs, not the full room payload,
  where practical.
- Response compression (Brotli/Gzip) enabled for the static Blazor WASM
  assets — the WASM download is the single biggest perf cost in this app.

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
