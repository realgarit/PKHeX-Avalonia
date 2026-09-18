# Agent B — compact main shell and navigation

## Objective

Compose a functional production main window at 900×600 using A's resources, C's existing editor control contract and D's PartyStrip. Remove the large persistent workspace rail while keeping all destinations and commands reachable.

## Owned files

- PKHeX.Avalonia/Views/MainWindow.axaml and MainWindow.axaml.cs
- PKHeX.Presentation/ViewModels/MainWindowViewModel.Workspaces.cs
- New PKHeX.Presentation/ViewModels/MainWindowViewModel.Appearance.cs
- Tests/PKHeX.Avalonia.Tests/ResponsiveShellTests.cs AFTER A integration
- New Tests/PKHeX.Avalonia.Tests/CompactShellTests.cs

Read MainWindowViewModel.cs (already has _themeService and _uiDensityService), FileCommands, EditorDialogs and capability registry. No extra constructor injection is expected. MainWindowViewModel.cs itself remains F-owned if an actual change is needed.

## Implementation sequence

1. Record menu commands, key bindings, DataContext assignments, workspace indices and launcher routes. Preserve Ctrl+O/S/Shift+S/K/1/2/3, Escape and existing Undo/Redo access.
2. Change default/min client layout to DESIGN.md. Remove the 176px rail. Build compact native menu, save context row, two-column Pokémon workspace and status. Keep actual Save/Export semantics and welcome/error/update/HaX states.
3. Keep Save and Reports workspaces and their full content in the available body; hide the editor there. Keep existing workspace index semantics (0 Box, 1 Party, later save tabs) unless a documented compatible mapping is essential. Verify SelectWorkspaceTabCommand and menu registry still land correctly.
4. Compose BoxViewer and D's PartyStrip in the right pane. Delete the old passive party ItemsControl. Do not duplicate D's slot templates or pointer routing. If D is unmerged, wait for its accepted commit before final build; merge the accepted base into your branch without force-push.
5. Bind the top theme picker to a framework-free presentation property/command using existing _themeService. Synchronize when opened/activated and when returning from modal Settings; no stale local bool, new persistence file, or Avalonia types in Presentation. Label Light/Dark with existing localized theme labels. Do not expose legacy choices.
6. Preserve detached Box/Party launch paths and ctrl-k capability search. Labels/data must come from the loaded save, not hardcoded Scarlet/Patrik.
7. Replace obsolete old-width/rail assertions with arranged bounds at 900×600; retain 1024×720 coverage and all save/report/capability availability checks. Coordinate C's width needs instead of widening the shell.

## Done when

A real supported save has all box slots and the interactive party strip visible at 900×600; no whole-window scroll or rail; every old destination still works; no-save, loading/error and both themes render correctly. Validate theme persistence and modal Settings return. Report actual width measurements and native DPI limitations.

## Mandatory working rules

Read root AGENTS.md and ../README.md, ../DESIGN.md, ../CONTRACTS.md, ../VERIFICATION.md before changing code. Inspect reference/compact-light.png and compact-dark.png; inspect preferences images where relevant. Use the supplied isolated worktree and foundation SHA. You are not alone in the codebase: do not revert others' edits. Own only the files below; request an explicit ownership transfer for anything else. Core, AutoMod, UIVersion and all unrelated behavior are immutable for this task. Preserve compiled bindings, localization, accessibility and native Fluent control behavior. Reference-prototype is visual evidence, not production architecture.

Send new resource/string requirements to F; do not independently edit shared styles/localization/root AGENTS.md during the parallel wave. Do not add hardcoded English or test allowlist exemptions. Use dynamic theme resources. Before your PR, run the repository pr-checklist skill, appropriate reviewer agents, a zero-warning Release build and focused meaningful tests. Deliver actual production renders in both themes, commands/results, preserved behavior inventory, and native-test limitations. An unavailable prerequisite is a handoff issue, not permission to implement another lane's files.
