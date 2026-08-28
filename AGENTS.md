# PKHeX-Avalonia

> Canonical instructions for all coding agents (Claude Code, Codex, GitHub Copilot). Claude loads this via the CLAUDE.md stub.

A native Avalonia (11.x) port of [PKHeX](https://github.com/kwsch/PKHeX), the Pokémon save editor —
cross-platform (Windows/macOS/Linux) instead of WinForms-only. Built on .NET 10 + Avalonia 11.x with
CommunityToolkit.MVVM.

Development is AI-assisted (Claude Code, Codex), and this is now publicly disclosed. The `.claude/`
and `.codex/` directories in this repo are real, in-use automation (hooks, skills, subagents) — not
decoration. Treat them as part of the project's tooling.

## Hard Rules

1. **`PKHeX.Core/` is a byte-for-byte upstream mirror** of kwsch/PKHeX. Never edit it manually to make
   something compile — port the fix into the consumer layers (`PKHeX.Application`, `PKHeX.Infrastructure`,
   `PKHeX.Presentation`, `PKHeX.Avalonia`) instead. The only exception is the `chore/sync-pkhex-core-*`
   branch produced by the `sync-upstream-core` skill, which replaces `PKHeX.Core/` wholesale from upstream.
2. **`PKHeX.AutoMod/` is vendored** (the Auto Legality Mod legalization engine from santacrab2/PKHeX-Plugins).
   See `PKHeX.AutoMod/VENDORED.md` for the re-sync procedure — no `.cs` source edits under `AutoMod/` or
   `Enhancements/`; if a Core sync breaks compilation, fix it there and log the change in that file.
3. **No direct pushes to `main`.** Every change is a branch + PR. Enforced by `.claude/hooks/block-main-writes.sh`
   (and the equivalent `.codex/hooks/block-main-writes.sh` for Codex).
4. **Never touch `<UIVersion>` in a PR — CI owns the version bump.** `.github/workflows/release.yml`
   derives the next version from the highest existing `v*` git tag (never from the file), writes it to
   `Directory.Build.props`, commits, tags and publishes in one run on every push to `main`. A manual
   bump in a PR double-increments and can collide silently with a concurrent PR — see the 2026-08-28
   note in Working notes. `.claude/skills/pr-checklist` flags a hand-edited `<UIVersion>` as an error.
   **Your PR title is the version input**: `feat:` → minor, `fix`/`chore`/`deps`/`refactor`/`docs`/
   `test`/`ci`/`sync:` → patch, a `breaking` label or `!` in the prefix → major, anything unclassified
   → patch. (Top-level `<Version>` tracks upstream PKHeX.Core — that one is still hand-set, by a sync PR only.)

## Architecture

Five-project Clean Architecture split (verified against the `.csproj` files):

```
PKHeX.Core            no project references (vendored, upstream mirror)
PKHeX.Application      -> Core                                  (ports/abstractions, no UI deps)
PKHeX.Infrastructure   -> Application, Core, AutoMod             (implementations)
PKHeX.Presentation     -> Application, Core                      (Avalonia-free ViewModels)
PKHeX.Avalonia         -> Core, Application, Infrastructure, Presentation   (host/composition root)
PKHeX.AutoMod          -> Core                                   (vendored ALM engine)
```

Presentation depends on Application + Core only — it does **not** reference Infrastructure, so
ViewModels stay free of both Avalonia and implementation details. Avalonia is the only project that
references all four and is where DI wiring and Views live. This is enforced by
`Tests/PKHeX.Architecture.Tests/LayerDependencyTests.cs` (NetArchTest-based).

Patterns to know:
- **`ViewLocator`** (`PKHeX.Avalonia/ViewLocator.cs`) — the single place that maps a dialog ViewModel
  type to its View, via a compile-checked dictionary. It lives in the host so Presentation never
  references Views.
- **`IWindowService.ShowTool`** (`PKHeX.Application/Abstractions/IWindowService.cs`) — opens a modeless
  auxiliary tool window for panels that shouldn't crowd the main editor (e.g. batch search, box report).
  Singleton-per-VM (re-invoke focuses existing).
- **Thin per-gen editors** keep direct Core-block access (not wrapped in interactors).
- **Sprites cross as PNG `byte[]`** — `ISpriteRenderer` → `PngBytesToBitmapConverter` in Views.

### Architecture constraints

- **Application/Infrastructure** stay free of Avalonia, Skia, AND CommunityToolkit.Mvvm (plain
  events/POCOs). CommunityToolkit.Mvvm is allowed only in Presentation.
- **Sprite boundary:** `ISpriteRenderer` returns PNG `byte[]`; host `PngBytesToBitmapConverter`
  materializes the Bitmap in Views.
- **Navigation:** `IDialogService` (framework-free) + `IWindowService.ShowDialogAsync(vm, title)`.
- **`GameInfo.*` statics** (read in 51 files) are Core/Entities reads — left as-is; only language
  mutation is owned by Application LanguageService.
- **Workflow use cases** are stateless and `new`'d at call sites (not DI-injected) to avoid ctor bloat.

## Workflow

### Branch + PR flow
- Work in feature branches, commit there, push, and open a PR. Never `git push origin main`.
- A clean build is expected to produce **0 warnings**.

### Auto-merge policy
- Claude-created PRs are automatically merged once CI/checks pass — no manual check-in needed.
- Flow: `gh pr checks <n> --watch`, then `gh pr merge <n> --merge --delete-branch`, then delete
  local branch and switch back to main.

### Shipped update communication
- After a PR is merged and the release version and live merge state are verified, post a concise
  user-facing update in the project Discord through the connected Chrome extension. Include the
  shipped version, merged PR link, and a short summary of visible changes; do not post private save
  data or announce work before merge.

### Worktree shipping
- Git commit/push/PR must run from inside the agent's worktree (not the repo root).
- The `block-main-writes.sh` hook checks the shell's cwd branch — so shipping commands must come
  from a Bash session whose cwd is inside the feature-branch worktree.
- Bulk temp data goes in a temp dir inside the worktree on real disk, never on tmpfs (ENOSPC risk).

## Build / Test

```
dotnet build PKHeX.sln -c Release
dotnet test PKHeX.sln -c Release
```

A clean build is expected to produce **0 warnings**. Test projects live under `Tests/`:
`PKHeX.Core.Tests`, `PKHeX.Avalonia.Tests`, `PKHeX.Architecture.Tests`.

- `Tests/PKHeX.Avalonia.Tests/` — xUnit + Avalonia.Headless + Moq
- `Tests/PKHeX.Core.Tests/Legality/Legal/` — 133 legal PKM fixtures
- `Tests/PKHeX.Core.Tests/Legality/Illegal/` — 43 illegal PKM fixtures

## Guardrail Tests (fail a PR if ignored)

- **`Tests/PKHeX.Avalonia.Tests/AccessibilityAuditTests.cs`** — regex-scans every `.axaml` view for
  icon-only interactive controls (`Button`/`ToggleButton`/`RepeatButton` with no visible text) and
  requires `AutomationProperties.Name`. Justified exceptions go in `accessibility-allowlist.txt` next
  to the test.
- **`Tests/PKHeX.Avalonia.Tests/LocalizationAuditTests.cs`** — regex-scans `.axaml` views and
  ViewModels for hardcoded user-facing English string literals instead of `{loc:Loc Key}` /
  `LocalizedStrings`. New/migrated files are enforced by default; the not-yet-migrated backlog is
  listed in `localization-allowlist.txt`.
- **`Tests/PKHeX.Architecture.Tests/LayerDependencyTests.cs`** — enforces the project reference
  direction above (e.g. Application must not depend on Avalonia/Infrastructure/Presentation).

## Localization

Resource files live in `PKHeX.Presentation/Localization/Strings/` (`LocalizedStrings.cs` / `LocExtension.cs`
drive lookup) with one JSON file per language: **9 languages** — `de`, `en`, `es`, `fr`, `it`, `ja`, `ko`,
`zh-Hans`, `zh-Hant`. Any new user-facing string needs a key added to **all 9** files, not just `en.json`.

## Theming

Theming is driven by `IThemeService` (`PKHeX.Application/Abstractions/IThemeService.cs`, `AppTheme` enum)
and implemented in `PKHeX.Avalonia/Services/ThemeService.cs` using Avalonia's `ThemeVariant`/
`ThemeDictionaries` APIs (`PKHeX.Avalonia/Styles/Theme.axaml`), including tracking the OS light/dark
preference for the `System` option. Covered by `Tests/PKHeX.Avalonia.Tests/ThemeTests.cs`.

## Dependency policy

- Stay on latest **11.x Avalonia** and **SkiaSharp 3.x**.
- Avalonia 12 / SkiaSharp 4 deferred — they are major versions with breaking API changes and need
  dedicated, UI-tested PRs. Don't bundle them into routine sweeps.

## Upstream Sync Automation

A daily workflow checks kwsch/PKHeX against `.github/upstream-sync/last-synced-sha.txt` and opens a
`PKHeX.Core Sync Required` issue (labeled `sync`) when upstream has moved. The full sync process —
mirroring Core 1:1, fixing consumer call sites, an Avalonia frontend-parity review of upstream's
WinForms UI changes, version bump, PR, and auto-merge once CI is green — is encoded in
`.claude/skills/sync-upstream-core/SKILL.md`. In detail:
1. Fetch the latest PKHeX.Core SHA from kwsch/PKHeX
2. Branch `chore/sync-pkhex-core-<short7>`; mirror `PKHeX.Core/` 1:1
3. Fix broken call sites in consumers only (never in Core)
4. Write the synced SHA to `last-synced-sha.txt` (and `<Version>` if upstream's moved) — do **not** touch `UIVersion`; CI bumps it from the `sync:` PR title
5. Check for frontend parity gaps — classify upstream non-Core commits; open `frontend-parity`
   issues for genuine gaps without blocking the Core auto-merge
6. Verify build (0 warn/0 err) + tests + diff=0; open PR; auto-merge once CI is green

## Style preferences

- **Clean architecture over shims** — for new features/integrations, prefer the cleanest
  architecture-correct solution even if it needs a rewrite, over expedient hacks.
- **No planning docs in repo** — don't commit AI-planning artifacts (specs/plans) to git history
  or GitHub. If committed, rewrite them out of branch history before pushing.
- **Prefer clean solution** — lead with the architecture-correct design, not expedient options.

## Known bugs fixed (reference)

- MemoryEditorViewModel.Save(): HT memory feeling/intensity written from OT values (copy-paste bug)
- PokemonEditorViewModel.LoadFromPKM(): Premature Validate() before memory fields loaded
- MainWindowViewModel: BatchEditor.BatchEditCompleted event leak on save close
- PartyViewerViewModel: redundant always-true pattern check
- Various dead fields and redundant OnPropertyChanged calls

## UI testing (computer-use)

- Publish the .app bundle: `dotnet publish PKHeX.Avalonia/PKHeX.Avalonia.csproj -c Debug -r osx-arm64 --self-contained -o <dir>`
- Open with: `open <dir>/PKHeX.Avalonia.app`
- Grant accessibility by bundle ID: `io.pkhex.avalonia`
- Test saves load from `Tests/savefiles/` via File > Open (app does NOT accept CLI file-path arg)
- Re-screenshot before each click — window may shift between actions

## Modeless tool-window pattern

- `IWindowService.ShowTool(vm, title)` + `CloseAllTools()` for auxiliary panels
- Singleton-per-VM (re-invoke focuses existing)
- Remembers size/position per VM type for the session via static `ToolBounds` dict
- `MainWindowViewModel.OnSaveFileChanged` calls `CloseAllTools()`
- First consumer: box seek (`EntitySeekViewModel` + `IBoxNavigator`)

## Automation tooling

- `.claude/` hooks, skills, and agents are committed to the repo (AI assistance publicly disclosed).
- `.claude/worktrees/`, `.claude/settings.local.json`, `.claude/scheduled_tasks.lock` stay gitignored.
- Changes to `.claude/` content go through branch + PR like everything else.

## Project memory (distilled)

<!-- Curated snapshot of prior agent session knowledge (2026-07-17). Claude's private memory remains canonical; update via Working notes. -->

- **PKHeX.Core sync workflow** (`sync-upstream-core` skill): fetch latest SHA from kwsch/PKHeX (often ahead of the issue's named SHA — sync to tip), branch `chore/sync-pkhex-core-<short7>`, mirror `PKHeX.Core/` via `rsync -a --delete --exclude bin --exclude obj` and verify `diff -rq` = 0 (no fork edits, not even the `.csproj`), fix only consumer call sites, set `<Version>` to match upstream's (leave `<UIVersion>` alone — CI bumps it from the `sync:` PR title), write the 40-char SHA to `.github/upstream-sync/last-synced-sha.txt`, then verify build/tests and auto-merge once CI is green. Watch CI in the background (`gh pr checks <n> --watch --fail-fast`) so it doesn't block the turn.
- **Frontend parity**: a green build only proves nothing broke, not that the Avalonia UI gained upstream's WinForms-side features. Syncs must classify upstream non-Core commits and open `frontend-parity`-labelled issues for genuine UI gaps without blocking the Core auto-merge.
- **UIVersion policy**: CI owns the bump — never hand-edit `<UIVersion>` in a PR (changed 2026-08-28; see Working notes). `.github/workflows/release.yml` computes the next version from the highest existing `v*` **git tag**, not from `Directory.Build.props`, because file-derived bumps collide silently across concurrent PRs. The bump size still follows SemVer by change type, now read from the **PR title prefix**: feat→minor, fix/chore/refactor/deps/docs/test/ci/sync→patch, `breaking` label or `!` prefix→major, unclassified→patch (logged loudly in the run). Classification and the arithmetic live in `.github/scripts/resolve-release-version.sh`; `workflow_dispatch` with `dry_run=true` prints the resolution without writing anything. One-time catch-up 1.1.44→1.20.0 happened 2026-06-25; the convention is documented in `docs/development.md`.
- **Auto-merge**: Claude-created PRs merge automatically once checks pass (`gh pr merge --squash --delete-branch` or `--merge --delete-branch` depending on repo history), no manual check-in required, including edge cases like suspected duplicate work in another session — still always via branch+PR, never direct pushes to main.
- **Worktree shipping gotcha**: the `block-main-writes.sh` hook checks the shell's *cwd branch*; sandboxed Bash/subagents spawned from a main checkout get cwd pinned to repo root and `cd` doesn't persist across their tool calls, so they cannot ship from a worktree. Ship (commit/push/PR) from the agent that owns the worktree, or from the main session's Bash issuing `cd <worktree>` as its own separate call before subsequent git commands. Never stage bulk generated data on a tmpfs scratchpad — use a temp dir inside the worktree on real disk.
- **Dependency policy**: stay on latest 11.x Avalonia (11.3.18) + SkiaSharp 3.x; Avalonia 12 / SkiaSharp 4 are deferred, breaking-API majors needing a dedicated, UI-tested PR — don't bundle into routine dependency sweeps (`Avalonia.Diagnostics` itself caps at 11.3.18, so bumping core Avalonia to 12 would drop the Debug inspector).
- **Clean Architecture migration** (PR #80, `refactor/clean-architecture`): full 5-project split done as one big-bang PR; sprites cross layer boundaries as PNG `byte[]` (`ISpriteRenderer` → `PngBytesToBitmapConverter` in Views); navigation via `IDialogService` (framework-free) + `IWindowService`; `ViewLocator` is a compile-checked VM→View map living in the host; Application/Infrastructure stay free of Avalonia/Skia/CommunityToolkit.Mvvm; workflow use cases are stateless and `new`'d at call sites, not DI-injected.
- **Modeless tool-window pattern** (PR #111): `IWindowService.ShowTool(vm, title)` + `CloseAllTools()` for auxiliary panels that shouldn't crowd a view (not Flyout/Popup, not context menus); singleton-per-VM, remembers size/position per VM type for the session; `MainWindowViewModel.OnSaveFileChanged` closes all tools on save change. Batch-instruction search is NOT Core-blocked — `SearchSettings.BatchInstructions` already exists in Core.
- **Style preference**: for new features/integrations, lead with the cleanest architecture-correct solution even if it needs a rewrite, rather than an expedient shim (stated explicitly re: Auto Legality Mod support, issue #89).
- **No planning docs in repo**: never commit superpowers brainstorming specs/plans into repo history or GitHub; keep them outside the repo or unstaged, and rewrite branch history if they slip in.
- **UI testing via computer-use**: publish the real `.app` bundle (`dotnet publish ... -o <dir>` then `open <dir>/PKHeX.Avalonia.app`) rather than `dotnet run` — only a real bundle can be granted accessibility access (by bundle ID `io.pkhex.avalonia`) and screenshotted; clicks then actuate normally. The app has no CLI file-path launch arg — test saves must be loaded via File > Open, which defaults to `Tests/savefiles/`.
- **`.claude/` automation is committed** to the repo (reversed from an earlier local-only policy on 2026-07-11, since AI-assisted development is now publicly disclosed): hooks, skills, and agents go through the normal branch+PR flow. `.claude/worktrees/`, `.claude/settings.local.json`, and `.claude/scheduled_tasks.lock` stay gitignored.

## Cross-agent conventions

- This file (`AGENTS.md`) is the single source of truth for agent instructions in this repo. `CLAUDE.md` and `.github/copilot-instructions.md` are pointers to it — never edit them, never duplicate content into them.
- Reusable skills live in `.claude/skills/` (one folder per skill with a `SKILL.md`). GitHub Copilot reads that directory natively; Codex sees it via the `.agents/skills` symlink. New skills always go in `.claude/skills/`.
- Claude-specific subagent definitions live in `.claude/agents/`. If you are not Claude Code, you may read them as role/process guidance.
- Session continuity across tools: before ending substantial work in ANY tool (Claude Code, Codex, Copilot), record durable context — decisions made, gotchas discovered, in-progress state worth resuming — in the "Working notes" section below, or fold it into the relevant section above. This is the shared memory between agents.

## Working notes

<!-- Any agent: append short dated notes here (YYYY-MM-DD — note). Prune notes when stale or once folded into the sections above. -->
- 2026-08-27 — Upstream sync issue #221 targets Core tip `19c356c` (5 commits, 17 Core files), newer than the issue's `ad69eae`. The 3,547-file Core mirror required no consumer changes; Release build is 0-warning/0-error and full tests are 3,024 passed / 2 existing skips. Frontend parity review found the Batch Editor translation changes already covered by issue #209 / PR #219, Stadium 2 slot conditioning automatic through the existing save load path, and a genuine gap for the new Gen 3 Battle Tower team-swap legality option tracked in issue #232. UIVersion is 1.48.1; upstream Version remains 26.07.07.
- 2026-08-24 — UX workbench 1.48.0 completes detached live Box/Party workspaces, session-safe Ctrl-copy/move with confirmation and atomic undo, imported-file replacement confirmation, reusable tab-header double-tap routing, and a 1024x720 resizable shell (380px editor, 360-480 bounds). The editor navigation uses a neutral charcoal selected surface with a 3px red spine; compact Stats/Hyper Training headers render without overlap at the 360px minimum. Final Release build is 0 warnings/0 errors; full gates are Core 449 passed/1 existing skip, Architecture 6/0, Avalonia 2,569/1 native-lifetime skip; post-gate color/layout coverage is 11/11 and Skia captures were visually reviewed. Core/AutoMod remain untouched.
- 2026-08-22 — Verified the Discord form question against the current app: the Pokémon Editor Main tab exposes a Form combo for multi-form species, and Core returns four forms for both Sawsbuck and Oricorio in Gen 9. No GitHub issue was opened because the capability is already present.
- 2026-08-21 — GitHub issue #215 reproduces a Discord report where Artwork-mode shiny Bewear and Vikavolt used normal-color artwork because `a_760s.png`/`a_738s.png` are not bundled; `SpriteLoader` now prefers the available classic shiny sprite before falling back to normal artwork. UI version is 1.45.2, with focused coverage in `SpriteStyleTests`.
- 2026-08-21 — GitHub issue #213 reproduces a Discord report where changing a Gen 7 egg Pokémon's ability in the Avalonia editor changed the ability ID but left `AbilityNumber` stale, causing `Ability mismatch for encounter`; `PokemonEditorViewModel.ApplyAbility()` now pairs known IDs with Core `RefreshAbility`, with regression coverage for Jangmo-o → Soundproof and Beldum → Metagross hidden-to-normal edits. UI version is 1.45.1.
- 2026-08-21 — PKHaX support on branch feat/illegal-mode-hax: Avalonia startup now uses Core StartupUtil (--HaX/-HaX or persisted ForceHaXOnLaunch) to set runtime-only AppSettings.IsHaXMode. The mode propagates through filtered species/move/item/ability sources, editable six stored battle stats, full inventory item IDs and HaX quantities, persistent title/status warnings, and suppressed legality overlays in box/party/editor views. UI version is 1.45.0; focused HaX tests pass (5), Avalonia (2,493), architecture (5), and Core (449 + 1 existing skip).

- 2026-08-18 — Upstream sync issue #208 targets tip `c0b78e6` (1 commit, 3 Core files changed). Core's `SlotChangelog` moved from `AddNewChange(ISlotInfo)` to a capture/commit model (`Begin(...)` → disposable `Change` with `Commit`/`Rollback`/`Cancel`), `Undo()`/`Redo()` now return `IReadOnlyList<ISlotInfo>`, and `Redo()` no longer clears the redo stack — so `UndoRedoService`'s local `GroupUnit` snapshots are now redundant in principle (Core groups natively via `Begin(params IEnumerable<ISlotInfo>)`); that refactor was deliberately left out of the sync. Frontend-parity gap for the Batch Editor (undo, in-dialog reset, live affected-slot count, count-gated buttons) is tracked in issue #209; our `BatchEditorViewModel` writes to the save without touching `UndoRedoService` at all.
- 2026-08-15 — Issue #205 implementation on `realgar/feat/join-avenue-shop-tuples` adds explicit desired/active Join Avenue shop tuple type/version/rank controls, a localized empty option, nine-language labels, and headless round-trip coverage. The consumer writes the inverse mixed-radix raw shop field because upstream `2d970dd` Core's tuple setter is not the inverse of its decoder; `PKHeX.Core/` remains untouched.
- 2026-07-19 — Issue #167 adds a save-side Switch Mystery Gift record manager in `feat/switch-gift-records`: SWSH (50 WR8 records), BDSP (50 records + 10 one-day entries, 2048 received flags, serial lock), PLA (50 trimmed WA8 records), and SV (32 retained trimmed WC9 records). Imports are deliberately limited to documented conversions; no BCAT redemption forging. The BDSP flag adapter accesses the full bitfield directly because upstream Core's helper shifts by 8 instead of 3.
- 2026-07-19 — Switch gift records now have full-composition Avalonia headless coverage for SWSH/BDSP/PLA/SV, including rendered slot counts, SV deletion, and BDSP flag/serial-lock controls. The README screenshot is reproducibly generated by the opt-in Skia-backed `HeadlessGiftRecordTests.CaptureSvGiftRecords_WhenEnabled_WritesPng`; normal CI keeps lightweight headless drawing.
- 2026-07-19 — Public release identity uses `PKHeX-Avalonia` as the product/display name and `Patrik Lleshaj` as publisher, company, author, copyright holder, and future self-signed certificate CN. Keep `realgarit` unchanged in GitHub URLs and the winget package identifier.
- 2026-07-27 — The deferred `b483ad4` sync (issue #192) is resolved: upstream `5cceba5` (Gen4 misc encounter generating fixes) added `pk.Ball = (byte)Ball.Poke` on hatch in the new `EggHatchLegality.ForceHatch`, clearing the HG/SS ball flag, so `ShowdownSetTests.SimulatorGetEncounters` passes again. Synced straight to upstream tip `b916b06`, skipping the stuck point. Upstream's `IEncounterSlot4` gained `IsRerollMinimum31`/`IsBugContest`/`IsSafariHGSS`/`Location`; our `Tests/PKHeX.Core.Tests/Legality/RNG/MockSlot4.cs` was updated to match (consumer-layer fix, Core untouched).
- 2026-07-30 — Issue #196 sync targets upstream tip `7890222` (newer than the issue's `ce08c00`). Core's `IGenerateSeed64.GenerateSeed64` now requires `ITrainerInfo` so Legends: Arceus generation can use save-specific shiny rolls; the vendored AutoMod call forwards its existing source trainer and the divergence is logged in `PKHeX.AutoMod/VENDORED.md`. Frontend parity review found only Core/test changes and existing translation-resource updates, with no Avalonia gap.
- 2026-08-15 — Upstream sync issue #204 targets tip `2d970dd` (15 commits, 3,547 Core files); the Join Avenue consumer now maps `ShopTypeTuple`/`ShopRank`, and the Oddish Gen 2 fixture moved to the upstream illegal `noChain` location after the new chain-breeding legality rule. Frontend parity gap for Join Avenue version/type/rank controls is tracked in issue #205; Release build is 0-warning/0-error and the sequential full test gate is 2,939 passed / 1 skipped.
- 2026-08-22 — macOS slot modifier wording: Avalonia's platform-neutral `KeyModifiers.Alt` maps to macOS Option (⌥). Box and party slot routing now share `SlotClickActionResolver`, localized labels use `Alt/Option+Click`, and four resolver regression tests cover delete/view/set/selection mapping. The headless harness cannot synthesize native modifier clicks; real macOS desktop verification remains the final platform-specific check. Form changing stays deferred.
- 2026-08-22 — Box/party modifier clicks were not functional because Avalonia `Button` consumes `PointerPressed` before direct XAML handlers receive it. Register the viewer-level handler during tunneling with `handledEventsToo`, then resolve the originating slot button through the visual ancestors. Routed regression tests cover Ctrl/View, Shift/Set, Alt/Delete for both viewers.
- 2026-08-23 — Pokemon editor report work on `codex/pokemon-editor-reports`: filterable Species/Held Item controls now expose the full list on focus while preserving typing filters; default/custom nickname state follows species changes and the `IsNicknamed` flag; OT/HT identity, friendship, and current-handler fields round-trip for traded Gen 6+ entities; form selection is guarded and covered for Flabebe; hypertraining, PID dice alignment, PKRS spacing, readable date pickers, and responsive left-pane sizing were polished. README now carries the PKHeX-Avalonia Discord branding and support guidance. UI version is 1.47.0. Focused headless/editor, density, localization, and accessibility tests pass; Skia captures reviewed for hypertraining and OT/HT/PKRS layout. Open issue #223 is the direct nickname match; #217 remains a separate met-location tooltip concern.
- 2026-08-23 — Editor workflow follow-up: form selection now re-notifies Avalonia after an ItemsSource replacement so a non-default form is selected on first load; gender/form-specific species keep their form and gender bits synchronized, addressing Meowstic issue #229; move suggestions require confirmation; Box and Party expose explicit first-empty-slot moves, with Party targeting the active Box; filterable fields draw a platform-neutral chevron and the editor sidebar/PID die received visual polish. Legends: Z-A Core verification showed changing an IV invalidates the encounter seed on a legal PA9; the bundled Flabébé forms all validate, so the screenshot-only mismatch needs the original PA9 to diagnose. UI version is 1.47.1. Shipped updates are announced in Discord after merge via Chrome.
- 2026-08-23 — Review follow-up: Party-to-empty-box moves must call SaveFile.DeletePartySlot after writing the destination so later party members are compacted instead of hidden; Box-to-Party is guarded and hidden for LGPE storage (`HasParty == false`). Form-gender-specific species expose only male/female choices and normalize stale Genderless values. Regression coverage now exercises actual party mutation, LGPE command safety, and Meowstic gender normalization.
- 2026-08-28 — Added the reusable `discord-feedback-triage` skill. It enforces Chrome-extension-only Discord access, reviewed-message checkpoints, stable parent identity and deduplication, open-and-closed GitHub duplicate checks, evidence limits, ASD-STE100 copy without em dashes, action-time send confirmation, and post-send verification. The new-message scan found the v1.48.0 app-version follow-up for issue #234; the fix is already merged in PR #270, so no new issue was opened. (Corrected: this note first said #270 released as v1.48.4 — it did not; see the version-collision note below. #270 first ships in 1.48.5.)
- 2026-08-28 — Five bug fixes shipped: #256 (backup manager selection gating), #250 (ZA Royale ticket point controls), #262 (Living Dex placed on the UI thread, `_pendingBatch` leak closed), #226 (move/PP-Up PP recalculation), and #234 (encounter entities adapted to the save format before editing). UI version is 1.48.5, the first release containing all five.
- 2026-08-28 — Concurrent PRs that each "read current `UIVersion` and bump patch" collide silently. #267/#268/#269/#270 all made the byte-identical 1.48.3 → 1.48.4 edit, so git auto-merged every one with no conflict and four distinct fixes shipped under one version; `v1.48.4` was tagged at the #267 merge, before the other three landed, so the published artifact is missing them. A conflict-resolution rule does not help, because no conflict ever occurs. `release.yml` also triggers on `paths: Directory.Build.props`, and the three later merges produced no net change to that file, so no release run even started for them. Version bumps must be serialized (merge one PR fully before the next computes its bump) or moved to a CI-side bump. The published `v1.48.4` tag/release was deliberately left in place — the correction is additive.
- 2026-08-28 — Encounter-database results arrive in their *native* format (e.g. `PK2`), and `PK2.Species` is a single byte, so assigning a species above 255 truncated silently (424 & 0xFF = 168 — Ambipom rendered as Ariados, issue #234). `LoadPKM` now adapts through `EntityConverter.TryMakePKMCompatible` before editing, matching upstream `PKMEditor.LoadFieldsFromPKM`.
- 2026-08-28 — `UndoRedoService` has no synchronization of its own and raises `StateChanged`/`UndoPerformed`/`RedoPerformed` synchronously into UI-bound listeners, so every member must be called on the UI thread (now stated in its XML docs, issue #262); compute a change off-thread, then apply and record it on the UI thread. Separately, `SaveFile.State.Edited` has writers across Presentation/Infrastructure but no production reader (only test assertions), so no unsaved-changes prompt exists — tracked in issue #272.
- 2026-08-28 — Version bumping moved into CI, and per-PR `<UIVersion>` edits are now forbidden (Hard Rule 4 inverted). `.github/workflows/release.yml` runs on every push to `main`, resolves the next version from the highest existing `v*` git tag via `.github/scripts/resolve-release-version.sh`, writes it into `Directory.Build.props`, and pushes the bump commit and the tag as one `git push --atomic` before building — so bump, tag, build and publish are a single run (a `GITHUB_TOKEN` push does not trigger downstream workflows, so a second run can never be relied on). The tag set replaces the file as the source of truth precisely because reading the file is what let #267/#268/#269/#270 all compute 1.48.4; a tag either exists on the remote or it does not. The bump size comes from the merged PR's conventional-commit title prefix, so PR titles now carry release meaning. `concurrency: release-version` serialises runs; because GitHub cancels a *pending* run when a newer one queues behind it, a burst of merges can collapse into one release run, so the resolver takes the strongest bump across every unreleased first-parent commit — the cost is a skipped version number, never a missing fix. Verify without releasing: `gh workflow run release.yml --ref <branch> -f dry_run=true`. `main` is unprotected and repo default workflow permissions are read-only, so the workflow declares `permissions: contents: write` explicitly; if branch protection is ever added, the bot push needs an exemption or a PAT/App token.
- 2026-08-28 — Follow-up notification audit found that message history alone does not prove Discord is caught up. The Chrome-only process now checks server and channel badges, Inbox `Unreads` and `Mentions`, and every flagged visible channel. The remaining `NEW` marker resolved to the existing welcome channel, with no new feedback.
- 2026-08-28 — `gh pr merge <n> --merge --delete-branch` run from inside a worktree fails at its LOCAL branch-delete step with `fatal: 'main' is already used by worktree at 'C:/Git/PKHeX-Avalonia'` — gh tries to check out `main` to delete the merged branch, but `main` is already checked out at the repo root. The API merge SUCCEEDS before that failure, and gh then aborts WITHOUT deleting the remote branch. So a non-zero exit from that command does not mean the merge failed: always confirm with `gh pr view <n> --json state,mergedAt,mergeCommit`, then finish cleanup manually from the repo root (`git worktree remove`, `git branch -D`, `git push origin --delete`, `git worktree prune`). Observed four times on 2026-08-28.
