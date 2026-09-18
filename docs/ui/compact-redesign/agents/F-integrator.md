# Agent F — integration coordinator and final verifier

## Objective

Make the independent lanes converge on one compact, functional app. You coordinate; you do not race workers in their owned files. The planning task did not implement anything. Begin only when the user dispatches implementation.

## Owned coordination/shared files

- All PKHeX.Presentation/Localization/Strings/*.json
- PKHeX.Avalonia/App.axaml and App.axaml.cs only for an actual composition requirement
- PKHeX.Presentation/ViewModels/MainWindowViewModel.cs only for agreed shared requirements (it already has IThemeService)
- Tests/PKHeX.Avalonia.Tests/Harness/CompactUiCaptureTests.cs (new), existing shared capture infrastructure only if required
- Root AGENTS.md Working notes
- After lanes finish, explicit ownership transfer for final cross-component fixes

## Execution sequence

1. Record live main/base SHA, clean/dirty status, target branch strategy and current root instructions. If main differs from the documented snapshot, verify APIs and test names. Protect unrelated worktrees. Choose main-target independent PRs or a checked integration branch; do not let agents guess.
2. Hand A its worktree and prompt. Verify its resource API, contrast changes and intentional obsolete-test updates before integration. Register dependencies and the accepted foundation SHA.
3. Add/reserve new translated keys before parallel work; correct stale Settings_Theme_Desc in all nine languages. Reuse existing labels. Handle future string/resource requests centrally so agents do not collide in JSON/style files.
4. Dispatch B/C/D/E as in DISPATCH.md if authorized to manage agents, otherwise give the user their exact prompts/worktree paths. Receive per-lane inventories. No two agents own the same file concurrently. Do not modify root Working notes from each lane.
5. Integrate D, C, E then B, updating each branch against accepted changes and checking CI. Inspect unresolved interaction differences, particularly PartyStrip focus/drag routing, editor minimum width, Settings theme synchronization and tool-window sizing.
6. Add production capture tests for the matrix. Run build/full tests once on the assembled revision, then rerun appropriately after any fixes. Measure actual client bounds and inspect every image.
7. Run the native UI interaction matrix. Use disposable repository save fixtures; preserve originals. Verify real dropdowns (separate popup roots), secondary windows, theme persistence, slot modifier actions, drag/drop/undo, save/reload, localization and scaled displays. Report any platform that cannot be tested.
8. Request xaml-mvvm-reviewer and, for cross-project work, architecture-boundary-reviewer. Resolve actionable findings without diluting the approved design or deleting tests.
9. Before final PR/release, record a feature-parity checklist and honest validation evidence. Use pr-checklist, wait for required CI, merge per authorized repo workflow, verify live release, then follow repository shipped-update communication policy. Do not announce a partially integrated UI as complete.
10. Update Working notes with final design decisions, implementation commits, known platform validation limits and any follow-up issues. Clean only your own merged clean worktrees; preserve the user's reference handoff.

## Refuse these shortcuts

- Replacing actual app with reference-prototype or loading its sample Pokémon in production.
- “Fixing” minimum-width failures by returning to 1024/1480 default or clipping tabs.
- Removing settings/workspaces/advanced editor sections to match a screenshot.
- Deleting neutral-theme tests without replacing them with new meaningful checks.
- Shipping before native dropdown/secondary-window evidence just because headless tests are green.
- Hand-editing UIVersion, editing Core, leaking private save data, or force-pushing.

## Final user report

State shipped version/PRs only after verification. Include the actual production screenshot at 900×600, screenshots of dark/light and real secondary window/popup, build/tests, behavior preserved, and remaining native-platform limitations. If a genuine external blocker prevents completion, give the exact branch/worktree, failing gate and next concrete action.

## Mandatory working rules

Read root AGENTS.md and ../README.md, ../DESIGN.md, ../CONTRACTS.md, ../VERIFICATION.md before changing code. Inspect reference/compact-light.png and compact-dark.png; inspect preferences images where relevant. Use the supplied isolated worktree and foundation SHA. You are not alone in the codebase: do not revert others' edits. Own only the files below; request an explicit ownership transfer for anything else. Core, AutoMod, UIVersion and all unrelated behavior are immutable for this task. Preserve compiled bindings, localization, accessibility and native Fluent control behavior. Reference-prototype is visual evidence, not production architecture.

Send new resource/string requirements to F; do not independently edit shared styles/localization/root AGENTS.md during the parallel wave. Do not add hardcoded English or test allowlist exemptions. Use dynamic theme resources. Before your PR, run the repository pr-checklist skill, appropriate reviewer agents, a zero-warning Release build and focused meaningful tests. Deliver actual production renders in both themes, commands/results, preserved behavior inventory, and native-test limitations. An unavailable prerequisite is a handoff issue, not permission to implement another lane's files.
