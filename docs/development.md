# Development guide

## Build, run, test

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
# Run the app
dotnet run --project PKHeX.Avalonia

# Build
dotnet build PKHeX.sln -c Release

# Test (full suite)
dotnet test PKHeX.sln -c Release

# Publish a self-contained build (example: macOS ARM)
dotnet publish PKHeX.Avalonia -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

CI (`.github/workflows/ci.yml`) builds and tests on Windows, macOS, and Linux for every push and
pull request. `release.yml` builds installers/packages on a version bump — see
[packaging.md](packaging.md).

## Clean Architecture layer map

The code is split into five projects so the UI never touches PKHeX's core logic directly, and so
dependencies only ever point one way:

```
PKHeX.Avalonia  ──depends on──▶  PKHeX.Presentation, PKHeX.Application, PKHeX.Infrastructure, PKHeX.Core
PKHeX.Infrastructure ──depends on──▶  PKHeX.Application, PKHeX.Core
PKHeX.Presentation ──depends on──▶  PKHeX.Application, PKHeX.Core
PKHeX.Application ──depends on──▶  PKHeX.Core
PKHeX.Core ──depends on──▶  (nothing in this repo)
```

| Project | What it does |
|---|---|
| **PKHeX.Core** | Save, entity, and legality logic. Kept **1:1** with [upstream PKHeX](https://github.com/kwsch/PKHeX) — never edited directly (see below). |
| **PKHeX.Application** | Use-cases and service interfaces (ports) on top of Core. No UI framework, no file/network I/O. |
| **PKHeX.Infrastructure** | Implements the Application-layer ports: file access, settings storage, backups, update checks, LiveHeX networking, and the vendored Auto-Legality engine's integration. |
| **PKHeX.Presentation** | ViewModels and localization plumbing. No Avalonia/UI-framework references. |
| **PKHeX.Avalonia** | The Avalonia desktop app: views (`.axaml`), styles/themes, converters, and composition root. |
| **PKHeX.AutoMod** | Vendored Auto-Legality Mod legalization engine (separate from the Core mirror — see below). |

`Tests/PKHeX.Architecture.Tests` enforces the dependency direction above — it fails the build if a
lower layer references a higher one (e.g. `PKHeX.Application` referencing `PKHeX.Avalonia`).

Rule of thumb when adding a feature: define the capability as an interface in
`PKHeX.Application/Abstractions`, implement it in `PKHeX.Infrastructure`, drive it from a
`PKHeX.Presentation` ViewModel, and wire the view in `PKHeX.Avalonia`.

## PKHeX.Core: 1:1 mirror policy

`PKHeX.Core` is never modified directly in this repo — it's kept byte-for-byte identical to
[kwsch/PKHeX](https://github.com/kwsch/PKHeX)'s `PKHeX.Core` folder. Any fix or behavior change
needed for the Avalonia UI is ported into `PKHeX.Application`/`PKHeX.Infrastructure`/
`PKHeX.Presentation` instead — never patched into Core.

Sync automation:

- `.github/workflows/check-upstream-sync.yml` runs daily, compares
  `.github/upstream-sync/last-synced-sha.txt` against the latest upstream commit touching
  `PKHeX.Core`, and opens a **"PKHeX.Core Sync Required"** issue (with the new commits and a diff
  link) when out of date. It can also be triggered manually from the Actions tab. See
  [`.github/upstream-sync/README.md`](../.github/upstream-sync/README.md) for how the workflow and
  the SHA file fit together.
- Syncing from that issue mirrors the upstream `PKHeX.Core` folder byte-for-byte, ports any
  resulting API changes into the Avalonia layers (never into Core itself), updates
  `last-synced-sha.txt` to the new commit, and ships the result as a normal PR titled `sync: ...`
  (CI bumps `UIVersion` from that prefix after the merge — the PR itself must not touch it).

## PKHeX.AutoMod: vendored, not mirrored

Unlike `PKHeX.Core`, `PKHeX.AutoMod` (the Auto-Legality Mod legalization engine) is vendored from a
different upstream — [santacrab2/PKHeX-Plugins](https://github.com/santacrab2/PKHeX-Plugins) — and
is **not** under the 1:1 mirror rule; it's a separate vendored sync target with its own re-sync
procedure. Source is copied verbatim (no `.cs` edits) with only the `.csproj` adapted to build
against this repo's `PKHeX.Core` via project reference instead of a NuGet pin. Full details,
including the re-sync steps and what was deliberately *not* vendored (the WinForms plugin UI, and
the USB/3DS-capable `PKHeX.Core.Injection` project — see LiveHeX below), are in
[`PKHeX.AutoMod/VENDORED.md`](../PKHeX.AutoMod/VENDORED.md).

LiveHeX's console connectivity (`PKHeX.Infrastructure/LiveHex/`) is **not** vendored from that same
upstream — it's a clean-room re-implementation of the small, public sys-botbase protocol. The
rationale (USB dependency out of scope, dead 3DS code, compile-time coupling that would break the
byte-for-byte rule) is documented in
[`PKHeX.Infrastructure/LiveHex/NOTICE.LiveHeX.md`](../PKHeX.Infrastructure/LiveHex/NOTICE.LiveHeX.md).

## UIVersion (SemVer) convention — CI owns the bump

`Directory.Build.props`'s `<UIVersion>` tracks Avalonia-layer releases independently of
`PKHeX.Core`'s own `<Version>`.

**Do not edit `<UIVersion>` in a pull request.** `.github/workflows/release.yml` owns it. On every
push to `main` it resolves the next version, writes it into `Directory.Build.props`, commits, tags
and publishes — all in one run.

The bump size still follows SemVer by change type, but the change type is now read from the merged
**PR title's conventional-commit prefix**:

| PR title prefix / signal | Bump |
|---|---|
| `feat:` | minor |
| `fix:` `chore:` `deps:` `refactor:` `docs:` `test:` `ci:` `build:` `perf:` `style:` `sync:` `revert:` | patch |
| `!` in the prefix (`feat!:`), or a `breaking` label on the PR | major |
| anything else | patch (logged in the run as `default:`) |

### Why the tag, not the file

The next version is computed from the **highest existing `v*` git tag**, never from
`Directory.Build.props`. Reading the file is what broke on 2026-08-28: four PRs each branched off
1.48.3, each computed "patch + 1" = 1.48.4, and each wrote the byte-identical line — so git
auto-merged all four with **zero conflict**. Four distinct fixes shipped under one version, and
because the workflow then triggered on `paths: [Directory.Build.props]`, the three later merges
produced no net change to that file and never triggered a release at all. A conflict-resolution
rule cannot help, because no conflict ever occurs. A tag either exists on the remote or it does
not, and `git push --atomic` makes claiming one a single server-side check.

Bump, tag, build and publish happen in **one** run: a `GITHUB_TOKEN` push does not trigger
downstream workflows, so a second run can never be relied on to pick the bump commit up.

### Previewing a release without cutting one

`workflow_dispatch` takes a `dry_run` boolean (default `true`). It resolves and prints the PR, the
matched rule, the current tag and the next version, then stops before writing, committing, tagging
or releasing:

```bash
gh workflow run release.yml --ref <branch> -f dry_run=true
gh run watch "$(gh run list --workflow=release.yml --limit 1 --json databaseId --jq '.[0].databaseId')"
```

The classification and arithmetic live in `.github/scripts/resolve-release-version.sh`, which runs
identically on a runner and on your machine:

```bash
REPO=realgarit/PKHeX-Avalonia HEAD_SHA=$(git rev-parse origin/main) \
  bash .github/scripts/resolve-release-version.sh
```

## Localization contribution guide

UI-chrome translations (menus, dialogs, settings, status text — not game data, which
`PKHeX.Core`'s `GameInfo.Strings` already localizes) live in
`PKHeX.Presentation/Localization/Strings/*.json`, one file per language, with `en.json` as the
source of truth. See [CONTRIBUTING.md](../CONTRIBUTING.md) for the full guide: file format,
placeholder/mnemonic rules, how to add a new string, and how to migrate one of the still-unlocalized
editor dialogs off the allowlist.

## Test suite overview

```
Tests/
  PKHeX.Core.Tests/        Legality, save format, PKM, simulator, and general PKHeX.Core coverage — mirrors upstream's own test suite.
  PKHeX.Avalonia.Tests/    ViewModel/use-case tests for the Avalonia layer, plus feature-specific suites (LiveHex/, BackupManagerViewModelTests.cs, LivingDexTests.cs, ...).
  PKHeX.Architecture.Tests/  Enforces the Clean Architecture dependency rules described above.
```

Guardrail/audit tests worth knowing about, all under `Tests/PKHeX.Avalonia.Tests/`:

- **`AccessibilityAuditTests.cs`** — scans every `.axaml` view for icon-only buttons missing an
  `AutomationProperties.Name`.
- **`LocalizationAuditTests.cs`** — fails the build if a view or ViewModel introduces a hardcoded
  user-facing string instead of going through the localization resource system (dialogs not yet
  migrated are tracked in `localization-allowlist.txt`).
- **`LegalityAuditTests.cs`** — covers the whole-save legality audit tool.
- **`SaveLoadAuditTests.cs`** — round-trips every bundled test save through load/save.

Run the full suite with `dotnet test PKHeX.sln -c Release`, or scope to one area, e.g.:

```bash
dotnet test Tests/PKHeX.Avalonia.Tests --filter "FullyQualifiedName~Localization"
```
