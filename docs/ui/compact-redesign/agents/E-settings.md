# Agent E — compact Settings and secondary-window presentation

## Objective

Restyle real Settings to match the approved small Preferences surface, preserving all actual settings and their persistence/lifecycle.

## Owned files

- PKHeX.Avalonia/Views/SettingsView.axaml and SettingsView.axaml.cs
- PKHeX.Avalonia/Services/WindowService.cs
- New Tests/PKHeX.Avalonia.Tests/CompactSettingsTests.cs

Read SettingsViewModel, IWindowService, ViewLocator, ThemeService and AppSettings. Their behavior is already established; no new Preferences VM, no duplicate theme enum and no sample settings.

## Implementation sequence

1. Inventory every Settings binding including Startup, Backups, SlotWrite, Sprites, theme and density. Preserve all of them.
2. Recompose SettingsView with approximately 20px padding, 19–20px title, small section labels, native controls and subtle separators. Appearance first. Use compact section navigation or scroll content to fit 390×492; keep Save action accessible outside scroll content.
3. Preserve existing SelectedTheme/SelectedDensity immediate application/persistence; other settings still use SaveCommand. The prototype's Accent picker and Done-only semantics are not features to implement. Label live settings accurately using existing strings or F-provided translations.
4. Scope WindowService's size handling to SettingsViewModel (or a narrowly justified host-side sizing helper). Initial client size 390×492, min 390×420, reasonable maximum bounded by work area. Other windows retain their size policy. Do not globally cap all editors to the settings dimensions.
5. Keep current modal Settings lifecycle and ICloseableDialog close handling. Keep ShowTool singleton-per-VM, owner, remembered bounds and CloseAllTools behavior unchanged. Do not add a window shown automatically at application startup.
6. Check an already-open Settings window and representative detached tool both switch with the app theme. Check closing, reopening, live density, Save and keyboard focus. No duplicate theme cache.
7. Add real layout/persistence checks and light/dark renders. Settings may scroll for advanced sections; save/close stays reachable.

## Done when

The full real Settings surface is compact and every setting still works; no fake accent options; other tool windows are not shrunk. Report which settings apply live versus on Save, actual measured window bounds and native interaction evidence. Coordinate any theme synchronization needs with B via F.

## Mandatory working rules

Read root AGENTS.md and ../README.md, ../DESIGN.md, ../CONTRACTS.md, ../VERIFICATION.md before changing code. Inspect reference/compact-light.png and compact-dark.png; inspect preferences images where relevant. Use the supplied isolated worktree and foundation SHA. You are not alone in the codebase: do not revert others' edits. Own only the files below; request an explicit ownership transfer for anything else. Core, AutoMod, UIVersion and all unrelated behavior are immutable for this task. Preserve compiled bindings, localization, accessibility and native Fluent control behavior. Reference-prototype is visual evidence, not production architecture.

Send new resource/string requirements to F; do not independently edit shared styles/localization/root AGENTS.md during the parallel wave. Do not add hardcoded English or test allowlist exemptions. Use dynamic theme resources. Before your PR, run the repository pr-checklist skill, appropriate reviewer agents, a zero-warning Release build and focused meaningful tests. Deliver actual production renders in both themes, commands/results, preserved behavior inventory, and native-test limitations. An unavailable prerequisite is a handoff issue, not permission to implement another lane's files.
