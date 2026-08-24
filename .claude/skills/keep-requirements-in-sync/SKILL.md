---
name: keep-requirements-in-sync
description: REQUIREMENTS.MD is the living spec for this app, not a historical record of the original ask -- any code change that adds, removes, or changes a user-facing feature, workflow, tooling/CI pipeline, or performance/testing capability must update it in the same change. Use whenever writing or modifying code in PlanningPoker, before considering the task done.
---

# Keep REQUIREMENTS.MD In Sync

## The rule

If a change alters what's described in [REQUIREMENTS.MD](../../../REQUIREMENTS.MD) -- a new
feature, a changed workflow, a new CI pipeline, a new testing/perf tool, or a UI detail the doc
already claims (a count, a label, where something appears, who can see it) -- update the doc in
the *same* change that touches the code. Don't treat it as a follow-up or a "someone will get to
it later" item.

Before adding a new bullet, **grep the doc for related existing text first**. Most of the time the
fix isn't a new paragraph, it's correcting a sentence that's already there but now says something
false (a stale count, a stale location, a feature that got removed). Silently going stale is worse
than being verbose.

## Why

A single alignment pass on this doc (see git history around REQUIREMENTS.MD) turned up: two
different wrong avatar counts (doc said 6, README said 3, code said 5), a whole "reveal gif"
feature described in the README that was never built, a copy-invite button documented as living on
the create-room form when it's actually on the board screen, an entire load-testing tool
(`PlanningPoker.Tests.Play.Hundred`) built to satisfy an existing Section 10.3 requirement with no
mention added back to that section, and a demo-recording tool (`PlanningPoker.Tests.Play`) that had
existed for a while and was never documented at all. None of that would have accumulated if each
change had updated the doc when it happened.

## How to apply

**Update the doc for:**
- A new or changed UI element, workflow step, or behavior a user can see/trigger.
- A new project under `src/` (add it to the README's project-structure table too) or a new CI
  workflow -- especially one that fulfills or partially fulfills an existing requirement bullet
  (e.g. Section 10.3's load/soak-test ask). Reference the workflow file and what it produces.
- Any concrete detail already stated in the doc (a number, a default, a label, a location, who can
  see something) that the change makes inaccurate.
- A capability the doc describes as missing/future work (like [Section 5.7](../../../REQUIREMENTS.MD#57-roomplayer-rename-backend-only-no-ui))
  that gets built.

**Skip the doc for:**
- Pure refactors, renames, or internal implementation changes that don't alter described behavior.
- Bug fixes that restore documented behavior rather than changing it.
- Styling/CSS tweaks that don't change a documented visual detail (e.g. exact colors aren't
  usually spec'd; "green highlight border" as a concept is, so don't touch it for a shade change
  but do touch it if selection stops being shown as a border at all).
- Test-only changes with no behavior change.

**Where it goes:** match the doc's existing section structure (UX workflows in Section 5, Giphy in
6, testing in 8, performance/load-testing in 10.3, etc.) rather than bolting a new top-level
section on for every change. If something genuinely doesn't fit an existing section, that's a
signal to ask rather than force it somewhere.

Keep entries factual and terse, matching the doc's existing voice (see
[no-msbuild-node-reuse](../no-msbuild-node-reuse/SKILL.md) for the same "concrete, sourced from a
real incident" style applied to a different kind of skill in this repo).
