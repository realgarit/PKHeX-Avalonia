# Agent A — shared theme and density foundation

## Objective

Provide the shared resource API in CONTRACTS.md and apply the approved compact light/dark visual language to native controls. Do this before B–E begin. The current theme uses strictly achromatic tokens; the user has now approved restrained rose and charcoal colors. Update those old cosmetic policies explicitly.

## Owned files

- PKHeX.Avalonia/Styles/Theme.axaml
- PKHeX.Avalonia/Styles/ControlSystem.axaml
- PKHeX.Avalonia/Services/UiDensityService.cs
- Tests/PKHeX.Avalonia.Tests/ThemeTests.cs
- Tests/PKHeX.Avalonia.Tests/UiDensityTests.cs
- New Tests/PKHeX.Avalonia.Tests/CompactThemeTests.cs
- In ResponsiveShellTests.cs, ONLY VisibleThemeTokensStayNeutral and AppearanceAccent_IsStaticAndAchromaticAcrossViewStyles (and helpers used exclusively by them). This ownership ends once your PR is integrated; B then owns that file.

## Implementation sequence

1. Inventory existing color, brush and UiDensity keys and all style consumers. Preserve names and their semantic meanings.
2. Implement DESIGN.md palette in existing Dark/Light ThemeDictionaries; add the exact Compact* brushes and geometry keys from CONTRACTS.md. Measure contrast and adjust the prototype's weak muted/accent combinations. Keep semantic legality/shiny/warning colors distinct.
3. Define the six scoped style classes in CONTRACTS.md. Slot selection gains subtle tinted fill+border; action backgrounds use on-accent text. Keep native focus, hover, disabled and invalid states.
4. Style ComboBox popup/list/selection, FilterableComboBox, TextBox, NumericUpDown, ToggleSwitch/CheckBox, Menu and ContextMenu with consistent surfaces. Avoid overbroad selectors changing every legacy button.
5. Preserve AppDensity settings and live updates. Compact control heights target 29–30; Comfortable stays usable. Keep every existing density resource valid.
6. Replace only superseded achromatic test expectations; test semantic resource presence, actual theme changes and contrast. Keep unrelated responsive shell tests untouched.
7. Render a representative real editor, Settings and native control sheet in both themes. The old shell geometry may remain in this foundation PR; that is B's work.

## Done when

Every required resource/style exists; no missing-resource logs; both palettes switch live; appropriate existing tests and meaningful new checks pass; no non-color geometry assertion was silently removed. Report final accessible hex values, contrast ratios and exact APIs delivered to F. Do not begin shell/editor layout work.

## Mandatory working rules

Read root AGENTS.md and ../README.md, ../DESIGN.md, ../CONTRACTS.md, ../VERIFICATION.md before changing code. Inspect reference/compact-light.png and compact-dark.png; inspect preferences images where relevant. Use the supplied isolated worktree and foundation SHA. You are not alone in the codebase: do not revert others' edits. Own only the files below; request an explicit ownership transfer for anything else. Core, AutoMod, UIVersion and all unrelated behavior are immutable for this task. Preserve compiled bindings, localization, accessibility and native Fluent control behavior. Reference-prototype is visual evidence, not production architecture.

Send new resource/string requirements to F; do not independently edit shared styles/localization/root AGENTS.md during the parallel wave. Do not add hardcoded English or test allowlist exemptions. Use dynamic theme resources. Before your PR, run the repository pr-checklist skill, appropriate reviewer agents, a zero-warning Release build and focused meaningful tests. Deliver actual production renders in both themes, commands/results, preserved behavior inventory, and native-test limitations. An unavailable prerequisite is a handoff issue, not permission to implement another lane's files.
