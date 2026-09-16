---
name: pr-checklist
description: Use before opening a pull request in PKHeX-Avalonia, or when asked to check a branch/diff is PR-ready — verifies UIVersion was NOT hand-edited (CI owns the bump), no PKHeX.Core edits snuck in, the PR title carries the conventional-commit prefix CI reads, and upstream UI changes have frontend-parity coverage.
---

# PR Checklist

## Overview

Three checks this repo needs on every PR that a clean `dotnet build` won't catch. Run all three before `gh pr create`.

## The 3 Checks

### 1. `<UIVersion>` NOT hand-edited — and the PR title carries the bump

**Inverted on 2026-08-28: CI owns the version bump. A manual `<UIVersion>` change is now an ERROR.**

```bash
git diff main...HEAD -- Directory.Build.props
```

- Expected output for an ordinary PR: **empty**.
- Any `<UIVersion>` line in that diff → **fail the check**. Revert it:
  `git checkout origin/main -- Directory.Build.props` (or drop just that hunk if the PR
  legitimately changes `<Version>`).
- Why: every PR used to read the current value and add one. Four PRs branched off 1.48.3 on
  2026-08-28 and each wrote the byte-identical 1.48.4 line, so git auto-merged all four with
  **zero conflict** — four fixes under one version, and three of them never triggered a release
  at all. `.github/workflows/release.yml` now derives the next version from the highest existing
  `v*` git tag and bumps, tags and publishes in one run.
- The top-level `<Version>` (date-stamped, e.g. `26.05.05`) tracks upstream `PKHeX.Core` and is
  still hand-set — but only inside a `chore/sync-pkhex-core-*` PR.

**The PR title is now the version input.** CI reads its conventional-commit prefix:

| Title prefix / signal | Bump |
|---|---|
| `feat:` | MINOR |
| `fix:` `chore:` `deps:` `refactor:` `docs:` `test:` `ci:` `build:` `perf:` `style:` `sync:` `revert:` | PATCH |
| `!` in the prefix (`feat!:`) **or** a `breaking` label on the PR | MAJOR |
| anything else | PATCH (logged loudly as `default:`) |

So an unprefixed or mistyped title silently ships a patch. Check the title before opening:

```bash
gh pr create --title "fix: <what changed>" ...   # prefix is mandatory
```

Preview exactly what CI will compute, without releasing anything:

```bash
gh workflow run release.yml --ref <your-branch> -f dry_run=true
```

### 2. No PKHeX.Core edits (unless this is a sync PR)

```bash
git diff main...HEAD --stat -- PKHeX.Core/
```

- Expected output: empty.
- `PKHeX.Core` must stay a byte-for-byte mirror of upstream `kwsch/PKHeX`. Any consumer-side fix belongs in `PKHeX.Application`/`PKHeX.Infrastructure`/`PKHeX.Presentation`/`PKHeX.Avalonia` instead.
- Exception: PRs from the `sync-upstream-core` skill on a `chore/sync-pkhex-core-*` branch — those are expected to touch `PKHeX.Core`.

### 3. Frontend-parity coverage (sync PRs only)

Only applies when this PR is an upstream `PKHeX.Core` sync (branch `chore/sync-pkhex-core-*`):

- Confirm the sync's Frontend Parity Review step ran — check the PR body/commit for a note on upstream's non-Core (WinForms UI) changes and whether they need Avalonia equivalents.
- Any genuine gap should already be a tracked `frontend-parity`-labeled issue, not silently dropped.
- See the `sync-upstream-core` skill for the full review process — this check only confirms it happened, it doesn't replace it.

## Quick Reference

| Check | Command | Pass condition |
|---|---|---|
| UIVersion NOT hand-edited | `git diff main...HEAD -- Directory.Build.props` | Empty — CI owns `<UIVersion>` |
| PR title has a conventional prefix | read the title you are about to use | `feat:`/`fix:`/`chore:`/... — it selects the bump |
| No Core edits | `git diff main...HEAD --stat -- PKHeX.Core/` | Empty (unless sync PR) |
| Frontend parity | Check PR body / `frontend-parity` issues | Reviewed if this is a sync PR |

## Common Mistakes

| Mistake | Fix |
|---|---|
| Bumping `<UIVersion>` by hand | CI owns it since 2026-08-28. Revert the line; the release workflow bumps from the tag set |
| Bumping `<Version>` | `<Version>` mirrors upstream's date stamp — never hand-edit it outside a sync |
| Untyped PR title ("Update editor") | CI cannot classify it and defaults to PATCH — a new editor needs `feat:` to get a MINOR |
| Editing `PKHeX.Core` to fix a compile error after an upstream change | Port the fix into the consuming layer instead — see the `sync-upstream-core` skill's "Core principle" |
| Opening a sync PR without a Frontend Parity Review note | Re-run step 5 of `sync-upstream-core` before opening |
