# Shared implementation contracts and ownership

## Foundation resource API (A creates, B–E consume)

Keep all existing Theme* and UiDensity* keys valid. Recolor their appropriate semantic roles; do not remove a key while another view still consumes it. Add these resources in both Dark and Light ThemeDictionaries in `PKHeX.Avalonia/Styles/Theme.axaml`:

- `CompactAccentBrush`: accessible primary action background.
- `CompactOnAccentBrush`: accessible text/icon on that background.
- `CompactAccentTextBrush`: accent foreground on pane/canvas.
- `CompactSelectionBrush`, `CompactSelectionBorderBrush`.
- `CompactFocusBrush`: visible keyboard focus, distinct from hover.

A defines these style classes through the existing loaded style files (no new style file registration required): `compact-primary`, `compact-secondary`, `compact-editor-tabs`, `compact-slot`, `compact-settings`, `compact-party-strip`. Scope selectors to their intended controls. Do not globally turn every Button into a colored action.

A adds stable density resources to `UiDensityService.cs` and `UiDensityResourceKeys`: `CompactShellEditorWidth` (double 306), `CompactShellMenuHeight` (double 35), `CompactShellContextHeight` (double 49), `CompactShellStatusHeight` (double 27), `CompactPartyStripHeight` (double 76), `CompactPanePadding` (Thickness 16), `CompactSlotGap` (Thickness 0,0,5,5). In Comfortable, retain shell/editor widths and minimum row heights; increase control padding through existing density tokens and allow local scroll. Never silently overwrite the user's saved density.

Exact geometry can vary by a few DIPs to meet readable text/native platform constraints; the 900×600 default, component interfaces and feature preservation are hard requirements.

## Component API (D owns, B consumes)

D adds `PKHeX.Avalonia/Views/PartyStrip.axaml` and `.axaml.cs`, a compiled-binding UserControl with `x:DataType="vm:PartyViewerViewModel"`. It accepts the existing PartyViewerViewModel through DataContext; no new VM, duplicate slot collection, or constructor dependency. It is reusable inline and works with zero through six occupied slots. It uses existing availability state to hide/disable unsupported party operations. Inspect PartyViewer before choosing shared routing extraction; retain tunneled pointer handling and hit-test-transparent sprite content.

B composes it as `<views:PartyStrip DataContext="{Binding PartyViewer}" />`. B owns removing/replacing MainWindow's existing passive party template. D never edits MainWindow.axaml. Until D merges, B can prepare locally but its final PR must compile against D's accepted commit; no duplicate temporary PartyStrip definition.

BoxViewer remains a UserControl accepting BoxViewerViewModel. PokemonEditor continues accepting its existing ViewModel through CurrentPokemonEditor/ViewLocator. Do not rename public properties to make the visual migration easier.

## File owners

| Owner | Exclusive mutation scope |
|---|---|
| A | Styles/Theme.axaml; Styles/ControlSystem.axaml; Services/UiDensityService.cs; ThemeTests.cs; UiDensityTests.cs; new CompactThemeTests.cs; only the two color-policy methods in ResponsiveShellTests.cs during foundation |
| B | Views/MainWindow.axaml and .cs; MainWindowViewModel.Workspaces.cs; new MainWindowViewModel.Appearance.cs partial; ResponsiveShellTests.cs after A; new CompactShellTests.cs |
| C | Views/PokemonEditor.axaml and .cs; Controls/FilterableComboBox.cs only if required; existing FilterableComboBox tests; new CompactPokemonEditorTests.cs |
| D | Views/BoxViewer.axaml and .cs; Views/PartyViewer.axaml and .cs; new PartyStrip files; new CompactStorageTests.cs; existing slot routing/geometry tests only where changed visuals require |
| E | Views/SettingsView.axaml and .cs; Services/WindowService.cs; new CompactSettingsTests.cs |
| F | App.axaml(.cs) only if required; main VM constructor/DI if B requests it; all Localization/Strings/*.json; shared capture harness; root AGENTS.md; final cross-lane conflict resolution |

Paths above are relative to PKHeX.Avalonia, PKHeX.Presentation or Tests/PKHeX.Avalonia.Tests according to their names. Read the lane prompt for unambiguous full repo-relative paths. Unlisted production files are read-only to a lane until the integrator explicitly assigns ownership. The root AppTheme enum, ThemeService persistence and SettingsViewModel behavior should need no changes. Do not add Presentation → Infrastructure/Avalonia references.

F alone updates root Working notes, so four concurrent agents do not conflict there. Agents put their durable findings in their final handoff/PR description for F to consolidate.

## Localization coordination

F owns all nine JSON files. Before B–E start, F reserves/adds required new keys on the integration base, with real translations in de/en/es/fr/it/ja/ko/zh-Hans/zh-Hant. Reuse existing keys such as Settings_Theme and Settings_Appearance. Reserve `CompactUI_MoreSections` for overflow navigation if an equivalent does not exist; use existing Main/Stats/Met/Moves/OT/Contest/Memory/Ribbons labels. Correct the stale Settings_Theme_Desc (currently advertises Follow System although product choices are Light/Dark) in all nine languages.

If a lane discovers a new string, send the exact key, English meaning and usage to F. F supplies a separate shared-resource commit before that lane merges. No hardcoded temporary English, no duplicated JSON key, no new localization allowlist exemption.

## Existing tests that intentionally encode the superseded design

`ResponsiveShellTests.VisibleThemeTokensStayNeutral` and `AppearanceAccent_IsStaticAndAchromaticAcrossViewStyles` enforce the old achromatic palette. A replaces their obsolete visual assertions with meaningful checks for the approved accessible light/dark palette and resource coverage. B updates `MainWindow_DeclaresCompactResizableEditorAndDistinctWorkspaceTabs` and `MainWindow_At1024x720PreservesEditorAndWorkspaceWorkingWidths` to the new default AND keeps a larger-window regression case. Do not simply delete or skip these tests; retain unrelated workspace/capability/availability checks in the same file.

## PR handoff contract

Each lane reports: base SHA; changed files; screenshots in both themes; commands/results actually run; preserved binding/command inventory; out-of-scope discoveries; exact prerequisites; native checks performed vs pending. No fabricated pass counts or claims of macOS/Linux runtime testing from Windows headless tests. Do not edit UIVersion. Use conventional-commit PR titles.
