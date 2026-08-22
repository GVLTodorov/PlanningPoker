<div align="center">
  <img src="icon.svg" alt="Planning Poker" width="120" height="120" />

  # Planning Poker

  <em>Real-time, self-hosted Planning Poker for agile estimation.</em>

  [![CI](https://github.com/GVLTodorov/PlanningPoker/actions/workflows/ci.yml/badge.svg)](https://github.com/GVLTodorov/PlanningPoker/actions/workflows/ci.yml)

  <video src="docs/demo.mp4" controls muted width="720">
    Demo: 5 players join a room, vote, the host reveals, resets, and everyone votes again.
  </video>
</div>

---

## Table of Contents

- [What is this?](#what-is-this)
- [Features](#features)
- [Quick start](#quick-start)
- [Tech stack](#tech-stack)
- [Project structure](#project-structure)
- [Testing](#testing)
- [Configuration](#configuration)

## What is this?

A single-container web app for running Planning Poker sessions: create a room, share a link,
pick cards together, and reveal everyone's estimate at once — all over a real-time connection, no
sign-up required. It's a from-scratch .NET/Blazor WebAssembly rebuild of the business rules from
[axeleroy/self-host-planning-poker](https://github.com/axeleroy/self-host-planning-poker), with a
few deliberate changes (see [REQUIREMENTS.MD](REQUIREMENTS.MD) for the full spec this was built
against).

## Features

- **Create or join a room** in seconds — a friendly room-name suggestion is generated for you, and
  joining via a shared link takes one form submit (Enter works, no extra click).
- **Five decks** to estimate with: Fibonacci, Modified Fibonacci, Powers of 2, Trust Vote, and
  T-Shirt Sizes — switch decks mid-session and the round resets automatically.
- **Real-time board**: see who has picked (without seeing their pick) until everyone reveals
  together. Reveal is only available once every non-spectator has picked, enforced by the server,
  not just a disabled button.
- **Spectator mode** for anyone who wants to watch without voting.
- **Giphy-powered avatars and reveal gifs** — three random avatars to choose from when you join,
  and a celebratory gif alongside every reveal. Fully optional: the app runs cleanly with no Giphy
  features when unconfigured.
- **Accessible-by-default styling**: a green palette, large fonts, and generous touch targets
  aimed at comfortable use for players of any age.

## Quick start

### Local (.NET)

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (the exact version is pinned in
[global.json](global.json)).

```bash
dotnet run --project PlanningPoker.Api
```

This builds the Blazor client, hosts it as static files, and serves the API/real-time hub from the
same process. Open the URL printed in the console (e.g. `http://localhost:5232`).

Debugging from VS Code: open the repo folder and press **F5** — [.vscode/launch.json](.vscode/launch.json)
is already wired up to build and launch `PlanningPoker.Api`.

### Docker

```bash
docker compose up --build
```

Serves the app on [http://localhost:8140](http://localhost:8140) (see
[docker-compose.yml](docker-compose.yml)). See [Configuration](#configuration) below for the
optional Giphy environment variables.

## Tech stack

.NET (ASP.NET Core Minimal API) + Blazor WebAssembly, one language end-to-end, single container
image, no separate JS/npm toolchain. Real-time state (join/pick/reveal/reset) is pushed over
SignalR with a source-generated JSON payload; REST endpoints handle room creation/lookup, deck
metadata, and the Giphy avatar proxy.

## Project structure

The solution ([PlanningPoker.slnx](PlanningPoker.slnx)) sits at the repo root; every project lives
under `src/`, one folder per project, split along dependency lines:

```
src/PlanningPoker.Infrastructure/     Domain rules (Room/Player/Deck, no ASP.NET dependency) + wire
                                       DTOs and the JSON source-gen context, shared by Api and Client
src/PlanningPoker.Api/                Minimal API + SignalR hub + Giphy client; hosts the Client's
                                       output
src/PlanningPoker.Client/             Blazor WebAssembly frontend
src/PlanningPoker.Tests.Unit/         xUnit: domain logic, deck values, Giphy client, JSON round-trips
src/PlanningPoker.Tests.Component/    bUnit: Blazor component behavior
src/PlanningPoker.Tests.Integration/  WebApplicationFactory + a real SignalR client, full hub flow
src/PlanningPoker.Tests.Benchmarks/   BenchmarkDotNet: domain hot paths, JSON serialization, Giphy
                                       cache
src/PlanningPoker.Tests.LoadTest/     Console tool: N rooms x M players, reports pick/reveal latency
src/PlanningPoker.Tests.Play/         Playwright: joins, picks, reveals, and resets a room through
                                       the real UI -- records the demo video above
```

The Client depends only on Contracts — never Domain — so the browser bundle never ships
server-side validation logic or knows how the Giphy integration works (its API key never leaves
the server).

## Testing

```bash
dotnet test PlanningPoker.slnx
```

Runs the unit, component, and integration suites together. The [CI workflow](.github/workflows/ci.yml)
runs the same command on every push/PR and gates the version-bump/image-build/push job on it
passing. Performance checks (`PlanningPoker.Tests.Benchmarks`, `PlanningPoker.Tests.LoadTest`, and the bundle
size check in CI) are run separately and don't gate every commit — see REQUIREMENTS.MD Section 10.3.

The demo video above is produced by `PlanningPoker.Tests.Play`, a Playwright-driven browser
simulation (5 players join, vote, reveal, reset, and vote again). It's not part of `dotnet test` —
run it manually against a live instance (`dotnet run --project PlanningPoker.Tests.Play -- http://localhost:5232`),
or trigger the [Demo Video workflow](.github/workflows/demo-video.yml) from the Actions tab to
regenerate and commit `docs/demo.mp4`. That workflow needs `GIPHY_API_BASE_URL`/`GIPHY_API_QUERY`
configured as repo secrets to show real gifs.

## Configuration

Two optional environment variables enable the Giphy integration (avatar picker + reveal gifs). If
either is unset, those features simply don't render — nothing else breaks.

| Variable              | Example                                                                |
|------------------------|-------------------------------------------------------------------------|
| `GIPHY_API_BASE_URL`   | `https://api.giphy.com/v1/gifs/trending?api_key=YOUR_GIPHY_API_KEY`    |
| `GIPHY_API_QUERY`      | `limit=10&offset=0&rating=g&lang=en`                                   |

Never commit a real Giphy API key — set it only as an environment variable at deploy time (see
[docker-compose.yml](docker-compose.yml) for a placeholder example).
