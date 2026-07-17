# UI Density Design

## Goal

Make the Avalonia UI feel denser by default on smaller laptop displays, addressing GitHub issue #166 without adding a user preference or changing platform display scaling.

## Scope

Adopt option 2: compact shared theme defaults and the primary editing experience.

- Reduce shared theme padding and compact component sizing where it affects common controls.
- Reduce explicit spacing in `MainWindow.axaml` and `PokemonEditor.axaml`, the main daily-use screens.
- Preserve existing readable body text and all current accessibility automation names.
- Leave specialized editor views out of scope for this issue.

## Approach

`Theme.axaml` remains the source of global visual defaults. Its card, header, section, and view-container spacing will be made denser, as will the shared compact numeric-input styles where needed. The main window will use a narrower Pokémon-editor column and a shorter status-bar treatment. The Pokémon editor will reduce its repeated section/card paddings, field-label gaps, and inter-section spacing while preserving control min-heights and explicit usability affordances.

No density setting, persistence model, localization key, or new service is required. Avalonia continues to honor the operating system's scale factor.

## Verification

Add a focused resource/layout contract test that reads the three XAML files and asserts the compact default values selected for this feature. Run that test first and verify it fails before making visual changes. Then run the Avalonia test project, the full Release build, and the complete Release test suite. The existing accessibility and localization audit tests must remain green.

## Constraints

- Do not modify `PKHeX.Core` or vendored `PKHeX.AutoMod` source.
- Bump `UIVersion` by one patch for this `fix` PR.
- No direct push to `main`; changes remain on `realgar/feat/ui-density-166` for a PR.
