# PKHeX-Avalonia

> Canonical instructions for all coding agents (Claude Code, Codex, GitHub Copilot). Codex reads this directly; Claude and GitHub Copilot use pointer files when present.

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
   note in Working notes. `.agents/skills/pr-checklist` flags a hand-edited `<UIVersion>` as an error.
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
`.agents/skills/sync-upstream-core/SKILL.md`. In detail:
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
  **Explicit exception (2026-09-18):** the user requested the compact UI implementation handoff in
  `docs/ui/compact-redesign/`, including approved screenshots and an isolated runnable reference.
  Keep this handoff versioned; it overrides the planning-doc prohibition only for that directory.
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

- `.claude/` hooks and agents remain committed to the repo for Claude-specific automation (AI assistance publicly disclosed). Shared repository skills are committed under `.agents/skills/` for Codex; `.claude/skills/` is only a compatibility bridge.
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
- **`.claude/` automation is committed** to the repo (reversed from an earlier local-only policy on 2026-07-11, since AI-assisted development is now publicly disclosed): hooks and agents go through the normal branch+PR flow; shared skills live under `.agents/skills/` for Codex. `.claude/worktrees/`, `.claude/settings.local.json`, and `.claude/scheduled_tasks.lock` stay gitignored.

## Cross-agent conventions

- This file (`AGENTS.md`) is the single source of truth for agent instructions in this repo. `CLAUDE.md` and `.github/copilot-instructions.md` are pointers to it — never edit them, never duplicate content into them.
- Shared repository skills live in `.agents/skills/` (one folder per skill with a `SKILL.md`). Codex scans this location natively. Keep any `.claude/skills/` compatibility bridge pointer-only or generated from this directory; never maintain two independent sources. New shared skills always go in `.agents/skills/`.
- Claude-specific subagent definitions live in `.claude/agents/`. If you are not Claude Code, you may read them as role/process guidance.
- Session continuity across tools: before ending substantial work in ANY tool (Claude Code, Codex, Copilot), record durable context — decisions made, gotchas discovered, in-progress state worth resuming — in the "Working notes" section below, or fold it into the relevant section above. This is the shared memory between agents.

## Working notes

- 2026-09-20 — Corrective review fixes on `fix/review-issue-sweep-implementation`: BDSP Pokédex teardown no longer saves the working clone, Gen 9a Technical Records separate zero-based storage from one-based display and expose Legal Current, Legal All, Force All, and None actions, Ribbon legality status reaches the dialog and parent validation refreshes after Save, selected slot focus overrides selected-hover styling, and BDSP Seal Sticker Count/Total/Obtained values are normalized transactionally. Focused tests are 13/13; Release build is 0 warnings/errors; full gates are Avalonia 2,733 passed/1 existing skip, Core 449 passed/1 existing skip, Architecture 6/0. Opt-in Skia captures for Pokédex, Ribbons, Seal Stickers, and Technical Records were reviewed.

- 2026-09-20 — Next issue wave on `fix/issue-sweep-10-next` covers #217, #232, #240, #242, #246, #279, #328, #342, #343, and #345. The work adds Met Location ID tooltips, localized Gen 3 Battle Tower legality settings, full-value Pokémon editor tooltips and Stats characteristics, distinct slot interaction states, a shared PR-title release gate, localized transactional Seal Stickers, transactional Technical Records, transactional Ribbon editing, and transactional BDSP Pokédex Save/Cancel. Release build is 0 warnings/errors; final gates are Avalonia 2,730 passed/1 existing skip, Core 449 passed/1 existing skip, Architecture 6/0. Skia captures were rendered and reviewed for Seal Stickers, Technical Records, Ribbons, BDSP Pokédex, and Pokémon editor states.

- 2026-09-20 — Corrective review implementation on `fix/review-findings-implementation`: Global Link invalid-date clearing and native date bounds now work, inner Pokémon editor templates inherit contextual automation names, Box Layout reorders adjacent boxes with `SwapBox`, USUM Totem Sticker terminology/counters and immediate-apply notice are explicit, compact Daycare/dialog sizing is restored, Medal Rally has localized search, and Legality Audit actions are disabled while running with a polite live status region. Release build is 0 warnings/errors; final gates are Avalonia 2,719 passed/1 existing skip, Core 449 passed/1 existing skip, Architecture 6/0. Opt-in Skia captures for Box Layout, Daycare, Totem Stickers, Legality Audit, Global Link, Medal Rally, and Pokémon native controls were rendered and visually reviewed.

- 2026-09-20 — Follow-up review fixes on `fix/review-issue-sweep`: modern trainer IDs now validate the combined ID32 before atomic writes; Gen 7 Hall of Fame Cancel is Escape-aware and covered for SM/USUM; empty Hall of Fame/Mail delete commands are disabled; blank Gen 3 Hall of Fame saves show an explicit empty state; Pokéblock/Poffin grids use `SizeToHeader` plus minimum widths. Full Release build is 0 warnings/errors; final gates are Avalonia 2,694 passed/1 existing skip, Core 449/1 existing skip, Architecture 6/0. Skia captures were reviewed again.

- 2026-09-20 — Issue sweep on `codex/issue-sweep-10` covers #263, #265, #277, #285, #299, #301, #305, #317, #322, and #325. Core settings now apply through a side-effect-limited `StartupUtil` adapter; BoxViewer honors stored active boxes; SWSH badges use count semantics; Trainer IDs use Core display formats; Gen 7 Hall of Fame edits are clone-based with Save/Cancel; Gen 4 Roamer is no longer advertised; empty records and case-grid/Battle Tree layouts have regression coverage. Full Release build is 0 warnings/errors; final gates are Avalonia 2,690 passed/1 existing skip, Core 449/1 existing skip, Architecture 6/0. Headless Skia captures were visually reviewed for the affected editors; native Windows inspection verified the published shell and file-picker boundary.

- 2026-09-18 — The compact UI goal was resumed after the local review checkpoint. Final integration adds a regression guard for live language changes: replacing localized dropdown items must reapply the unchanged selected values, and the prepared PKM bytes must remain identical. Two reviewed production frames are retained under `docs/ui/compact-redesign/evidence/`. The earlier pause note below is historical; current PR/release status is tracked in GitHub.

- 2026-09-18 — Compact UI review checkpoint on `codex/compact-ui-implementation`: the user rejected the initial recolor/resize and requested faithful 900x600 prototype composition. The shell now has slim menu/save rows with no legacy rail/header; full-width primary editor fields, viewmodel-owned navigation, More details disclosure, compact moves/Stats totals, a truthful legal/illegal pill (hidden for empty/HaX), interactive party strip, flat Settings, and localized theme controls. Real production frames for both themes, dropdowns, editor sections, legal fixtures, German and Japanese are in `tmp/compact-ui-captures`; regenerate with `PKHEX_HEADLESS_CAPTURE=1` and the `CompactUiCaptureTests` filter. Release build: 0 warnings/errors. Full gate: Avalonia 2,673 passed/1 existing skip, Core 449/1 existing skip, Architecture 6/0. Architecture/MVVM reviews completed. The user paused the goal and asked to finish the current pass for review: keep work local, do not resume shipping automatically. Details in `docs/ui/compact-redesign/REVIEW.md`.
- 2026-09-18 — Separate pre-existing backup retention issue found during validation: same-millisecond saves use `T.bak`, `T_1.bak`, etc.; pruning can free `T.bak`, allowing the next write to reuse and then prune the newest content. `SaveBackupServiceTests.CreateBackup_PrunesOldestBeyondMaxBackups` failed once with version-3 instead of version-4; the final suite passed. No backup implementation changes were made in this UI work. A dedicated fix should allocate suffixes monotonically and compare them numerically, with a deterministic clock test.

<!-- Any agent: append short dated notes here (YYYY-MM-DD — note). Prune notes when stale or once folded into the sections above. -->
- 2026-09-20 — Follow-up issue sweep on `codex/issue-sweep-10-more` implements #241, #259, #283, #284, #290, #295, #300, #304, #313, and #315. The work adds transactional Box Layout editing, localized Pokémon editor automation names, stateful Legality Audit actions, stable dialog geometry, localized Gen 5 medal names, Global Link date set/clear handling, USUM Totem Sticker semantics, LGPE/SWSH read-only Daycare slots, Emerald National Dex unlock, and synchronized Gen 3-origin shiny PID/EC behavior. Focused coverage passes; Release build is 0 warnings/errors; full gates are Avalonia 2,713 passed/1 existing skip, Core 449/1 existing skip, Architecture 6/0. Published Windows native inspection covered the shell, Pokémon editor, Gen 4 Misc tab stability, Box Layout, Daycare, and Legality Audit; Skia captures reviewed Global Link and Medal Rally layouts. PR shipping remains outstanding.
- 2026-09-17 — Frontend refactor checkpoint on `refactor/frontend-overhaul`: persisted `AppDensity` tokens and settings, branded task-aware shell, full-width Save and Reports workspaces, searchable production-tool launcher, horizontal Pokémon editor sections, labeled box slots, party drawer, vector shell actions, and Dark/Light/HighContrast headless captures. Core and AutoMod are untouched. The umbrella goal remains active; next slices must replace the temporary static tool/menu catalog with the shared capability registry and continue the visual migration across auxiliary and generation-specific editors.
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
- 2026-09-15 — Issue #282 was independently synced through upstream tip `bf57ff1`, which includes its `e15d246`/`74b8890` commits plus nine newer commits. PR #347's Core tree and metadata matched upstream exactly; the clean sync branch updates `<Version>` to `26.08.26` and the full synced SHA while leaving `<UIVersion>` at `1.48.6`. Release build is 0-warning/0-error and the full test gate is 3,054 passed / 2 existing skips. Upstream parity follow-ups remain separate: PR #348 covers the Batch Editor regression and issue #349 covers the Gen4 Battle Video editor.
- 2026-09-16 — Frontend refactor checkpoint for issue #355: the ignored tmp/frontend-refactor-prototype now contains a static browser prototype with real repository Pokémon sprites, product icon, compact/comfortable density, dark/light themes, task-aware Pokémon/Save/Search workspaces, menus, launcher, and visual-only interactions. The prototype was browser-checked with no console errors; its run command and durable design decisions are documented in issue #355. The local Python server on port 5179 was stopped at session end; no application source changed.
- 2026-09-17 — PR #360 shipped five quality fixes in v1.48.10: serialized and stale-session-safe LiveHeX lifecycle, background bulk box transfer with cancellation/error handling, serialized save load/save with detached snapshots, Living Dex retirement on save changes, and bounded cancellable PKM Database folder scans. Issues #352, #353, #354, #356, and #357 are closed. Final verification is 0-warning/0-error Release build, 3,062 passed / 2 existing skips, and opt-in headless captures under `tmp/headless-evidence/`; `PKHeX.Core/` and `PKHeX.AutoMod/` were untouched.
- 2026-09-17 — Frontend overhaul follow-up on `refactor/tool-menu-registry`: visible Dark and Light palettes are now strictly neutral grayscale, Fluent system accents and update-banner references use the same tokens, and a no-tint regression test covers core theme colors and gradients. The shared capability catalog now also drives the deduplicated Tools menu with only available generation groups rendered, while native PathIcon geometry and bound accessibility names keep the menu and launcher consistent. Full verification is 0-warning/0-error, 2,630 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; the umbrella refactor continues into auxiliary and generation-specific editors.
- 2026-09-17 — Auxiliary-surface follow-up on `refactor/normalize-view-surfaces`: removed the remaining legacy Fluent `SystemControl...` brush references and undefined `ThemeBorderBrush` fallbacks from 41 views, routing them through the shared neutral surface/text/border tokens. Native Button, ToggleButton, CheckBox, RadioButton, and ComboBoxItem content now have an explicit vertical-center contract; headless runtime coverage guards that alignment and the no-tint foreground behavior. Full verification is 0-warning/0-error, 2,633 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched.
- 2026-09-17 — Pokémon editor composition redesign on `refactor/normalize-view-surfaces`: the entity header is now a compact species-only identity bar, the editor tab strip is a flat one-rule native strip with no selected card fill, and all seven editor content stacks use tighter spacing, gutters, section padding, and bottom margins to reclaim vertical space without changing commands or content. Narrow Skia capture at 360px shows long species names fitting without the old `Editing:` overflow. Full verification is 0-warning/0-error, 2,635 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched.
- 2026-09-17 — Reference editor visual overhaul follow-up on `refactor/normalize-view-surfaces`: Pokémon form sections now use a flat divider-based surface instead of repeated rounded cards, the native tab strip has a neutral backing rail, and compact spacing/gutters are applied consistently across the full editor composition. Skia captures at 360px and in the full shell were reviewed; the content remains command- and localization-compatible. Full verification remains 0-warning/0-error, 2,635 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched.
- 2026-09-17 — Save workspace composition follow-up on `refactor/save-workspace-composition`: Trainer now has one persistent top action bar and flat compact sections instead of scroll-hidden duplicate footer actions; Inventory now uses a compact neutral pouch tab rail, full-height native DataGrid surface, and centered toolbar/cell controls. Headless captures were reviewed for Trainer and Inventory at the full shell size. Full verification is 0-warning/0-error, 2,636 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; Events, Gifts, Batch, Reports, and remaining generation-specific pages continue in later slices.
- 2026-09-17 — Auxiliary editor surface follow-up on `refactor/auxiliary-editor-surfaces`: Event Flags, Mystery Gifts, Batch, and Gen 3/4/9 Misc views now use flat divider sections, compact native spacing, one top action surface, and no duplicate bottom Save row; selectable gift/record rows use one bottom rule instead of stacked rounded buttons. Six opt-in Skia captures were added and reviewed. Full verification is 0-warning/0-error, 2,640 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; remaining generation-specific pages continue in later slices.
- 2026-09-17 — Reports/rail visual follow-up on `refactor/reports-workspace-composition`: Reports now uses a compact three-column tool grid and slimmer launcher; report/launcher glyphs are outlined Fluent-style paths with no colored tile fill, and the workspace rail uses recognizable monochrome glyphs with a static `#808080` icon color. Selected rail items are outline-only, including the Fluent ToggleButton template presenter, with no gray fill. Full verification is 0-warning/0-error, 2,638 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed; fresh Skia captures reviewed. Core and AutoMod remain untouched; auxiliary and generation-specific pages continue in later slices.
- 2026-09-17 — Database tool surface follow-up on `refactor/database-tool-surfaces`: PKM Database and Mystery Gift Database now use full-width themed filter rails and native grid surfaces; Encounter Database uses a compact search surface and themed result area; Box Report and Legality Audit use titled tool headers, shared grid styling, and a two-row audit toolbar to prevent action clipping. Five opt-in Skia captures were added and reviewed. Full verification is 0-warning/0-error, 2,642 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; remaining generation-specific pages continue in later slices.
- 2026-09-17 — Pokédex editor family follow-up on `refactor/pokedex-editor-surfaces`: all ten Pokédex views (Gen 4/5/6/7/7b/8/8b/9, Legends: Arceus, and Simple) now use flat detail sections, outline-only species-list selection, compact shared list styling, and top action surfaces with no duplicate footer actions. Gen 8/9 gender dropdowns now bind localized content correctly instead of displaying `{loc:...}` markup. Four representative Skia captures were reviewed. Full verification is 0-warning/0-error, 2,644 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; remaining generation-specific pages continue in later slices.
- 2026-09-17 — Legacy Misc editor family follow-up on `refactor/legacy-misc-editor-surfaces`: Gen 2, Gen 5, Gen 7, LGPE, Gen 8, and BDSP Misc views now use flat divider sections, one top Save action surface, compact native spacing, and consistent compact tab rails. Gen 7 regular/super streak grids were corrected to use their spacer rows, removing label/value overlap. Six opt-in Skia captures were reviewed. Full verification is 0-warning/0-error, 2,646 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; remaining generation-specific pages continue in later slices.
- 2026-09-17 — Complex editor surface follow-up on `refactor/complex-editor-surfaces`: Pokeathlon, Join Avenue, Global Link, Medal Rally, Fashion, and Donut dialogs now use the shared tool surface, compact editor tabs, flat padded divider sections, and native save-grid styling; Fashion actions are in one top action surface. Six opt-in Skia captures were reviewed, including the supported Fashion state and Donut unavailable state. Full verification is 0-warning/0-error, 2,648 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; remaining generation-specific pages continue in later slices.
- 2026-09-17 — Legacy utility surface follow-up on `refactor/legacy-utility-surfaces`: Accessor, box/database/encounter lists, Event Work, Gear, Folder List, K-Chart, Link, Legends: Arceus Misc, Move Shop, Seal Stickers, Secret Bases, Simple Trainer, Tech Record, and Underground views now use shared neutral view/tool surfaces, flat sections, compact top action bars, and native save-grid chrome. All remaining hard-coded gray borders/full grid lines in this family were removed, and Box List no longer repeats the default box name (`Box 1 (0/30)`). Seven opt-in Skia captures were reviewed. Full verification is 0-warning/0-error, 2,652 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; the umbrella visual refactor continues into the remaining utility and dialog views.
- 2026-09-17 — Remaining utility dialog follow-up on `refactor/remaining-utility-dialogs`: Event Flags/Reset, Gen 9 Fashion, Group Viewer, Legality, Mail Box, Poké Puff, Save Diff, and Trash now use the shared view container, neutral compact tool headers, flat divider rows, outline-safe generated tabs, and native save-grid styling. The obsolete `section-card` style was removed, and DataGrid row selection suppresses the default filled highlight in favor of the same neutral outline language. Seven opt-in Skia captures were reviewed. Full verification is 0-warning/0-error, 2,654 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; the umbrella visual refactor continues into the last legacy utility surfaces.
- 2026-09-17 — Final neutral-status cleanup on `refactor/neutral-status-text`: the last hard-coded gray foreground in the Raid editor now uses the shared muted theme token, with a source-level guard preventing new gray text regressions across views. Full verification is 0-warning/0-error, 2,654 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched.
- 2026-09-17 — Theme palette cleanup on `refactor/trim-theme-palette`: removed unused legacy accent colors, accent/card/header gradients, accent glow, and the unreachable header-accent style so the live product palette is strictly Dark/Light neutral plus semantic status colors. Entity Seek now uses the shared view-title style, and the theme regression test guards the removal. Fresh Dark/Light and shell captures were reviewed. Full verification is 0-warning/0-error, 2,654 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched.
- 2026-09-17 — Remaining dialog surface follow-up on `refactor/remaining-dialog-surfaces`: 37 legacy views now use the shared flat padded divider surface, with local rounded-card overrides and excess section padding removed. All native `ListBoxItem` selections now stay transparent and show one neutral outline on Avalonia's Fluent content presenter, fixing the remaining filled selection state in raid and other generation-specific lists. Six opt-in Skia captures were reviewed, including a fresh shell capture proving the selected Pokémon rail is outline-only. Full verification is 0-warning/0-error, 2,650 Avalonia passed / 1 existing native-window skip, 449 Core passed / 1 existing skip, and 6 architecture tests passed. Core and AutoMod remain untouched; the umbrella visual refactor continues into any remaining legacy utility surfaces.

- 2026-09-19 - Live UI audit follow-up on `codex/polish-live-ui-audit`: corrected the Avalonia ARGB focus-glow palette, suppressed Fluent ComboBox's duplicate highlight surface, widened database/audit report headers, and constrained modeless tool windows to the monitor work area so title-bar controls remain visible. Added a focused ComboBox template regression test. Release build is 0 warnings/errors; Avalonia non-harness 2,399 passed / 1 existing native-window skip plus harness 247 passed, Core 449 passed / 1 existing skip, and Architecture 6 passed. Core and AutoMod remain untouched.

- 2026-09-18 - Compact UI handoff prepared on codex/compact-ui-handoff at docs/ui/compact-redesign/README.md. User approved the 900x600 light/dark prototype and requested a detailed in-repo plan for separately dispatched agents, explicitly excluding production implementation in this task. Package contains design/resource/component contracts, exclusive file ownership, six agent prompts, phased merge order, validation matrix, four approved images and a portable isolated reference project. Foundation must land before parallel shell/editor/storage/settings lanes; integrate storage, editor, settings, then shell. Reference prototype builds with zero warnings/errors. Production source and solution unchanged; no UI rollout or release occurred.
- 2026-09-20 - Top-bar polish on codex/topbar-menu-polish: Tools dynamic entries now use Avalonia MenuItem ItemContainerTheme containers instead of nested MenuItem templates, Settings and theme controls share the same compact height/font contract, and the redundant menu-row logo was removed. Release build is 0 warnings/errors; Avalonia non-harness 2,400 passed / 1 existing skip, focused shell 22 passed, and Architecture 6 passed. Core and AutoMod remain untouched.
- 2026-09-20 - Native language menu on fix/native-language-menu: replaced the Help > Language embedded ComboBox with a native Avalonia radio-style MenuItem submenu bound to ChangeLanguageCommand; LanguageOption now exposes current-selection notifications for live checkmarks. Full shell coverage is 23/23, and the published Windows branch build was visually verified with all nine languages and a live English/German switch.
- 2026-09-20 - About update-action spacing on fix/about-update-button-spacing: the About dialog now has enough measured height for its content, an 8px bottom action inset, and compact-secondary geometry for Check for Updates. The focused AboutView layout test passes and the published Windows branch build was visually reviewed.
