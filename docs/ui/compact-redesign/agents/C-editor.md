# Agent C — compact Pokémon editor with feature parity

## Objective

Make the real PokemonEditor legible in an approximately 306px column using the reference's identity/form hierarchy. Preserve every existing field and operation across generations.

## Owned files

- PKHeX.Avalonia/Views/PokemonEditor.axaml and PokemonEditor.axaml.cs
- PKHeX.Avalonia/Controls/FilterableComboBox.cs only if styling cannot achieve required behavior
- Tests/PKHeX.Avalonia.Tests/FilterableComboBoxTests.cs and FilterableComboBoxGeometryTests.cs only for related changes
- New Tests/PKHeX.Avalonia.Tests/CompactPokemonEditorTests.cs

Read the PokemonEditorViewModel partial files, but do not modify entity calculations or persistence in them. Request any necessary narrowly scoped presentation API from F.

## Implementation sequence

1. Extract an inventory of all current binding paths, commands, conditional visibility and editor sections into your handoff. Approximately 900 lines of existing XAML contain features absent from the mockup: do not replace them with its five-field form.
2. Build a 64–76px sprite + title/level/legality/shiny identity row. Keep ShowLegalityCommand, ToggleShinyCommand and actual validation state.
3. Keep Main/Stats/Met/Moves/OT-Misc as compact primary navigation. Provide an explicit localized More menu for supported Contest/Memory/Ribbons, with keyboard access and selection indication. Do not clip the tab strip or silently change index-based tests/consumers without updating them.
4. Reflow common Main fields at 306px using bounded label columns and stretch inputs. Preserve species/form/gender/nickname flag behavior, held item, ball, egg etc. Put extra fields in local scroll or a labeled disclosure; do not use a whole-editor Viewbox.
5. Reflow Stats to keep IV/EV totals, hypertraining and HaX stats available; Moves retains current/relearn/PP/PP-Up and technical record actions. Met and OT preserve dates, IDs, memories and handler ownership behavior.
6. Keep real FilterableComboBox sources/SelectedValue bindings for large lists. Style with A's resources; fix popup geometry only if needed. Numeric controls keep their actual limits.
7. Test real load/edit paths for multi-form Pokémon, gender-linked forms, nickname flags, traded Gen 6+ data, moves/PP and conditional tabs, reusing existing fixtures/tests. Focus new coverage on changed layout/interaction failure modes.

## Done when

No production binding or command was dropped without an equivalent documented reachable location. Core fields are usable at target width; advanced sections remain accessible; native dropdown typing/selection and keyboard focus work in both themes. Provide real Main/Stats/More-section renders, binding inventory and focused test results. Do not introduce mockup Apply/Reset semantics.

## Mandatory working rules

Read root AGENTS.md and ../README.md, ../DESIGN.md, ../CONTRACTS.md, ../VERIFICATION.md before changing code. Inspect reference/compact-light.png and compact-dark.png; inspect preferences images where relevant. Use the supplied isolated worktree and foundation SHA. You are not alone in the codebase: do not revert others' edits. Own only the files below; request an explicit ownership transfer for anything else. Core, AutoMod, UIVersion and all unrelated behavior are immutable for this task. Preserve compiled bindings, localization, accessibility and native Fluent control behavior. Reference-prototype is visual evidence, not production architecture.

Send new resource/string requirements to F; do not independently edit shared styles/localization/root AGENTS.md during the parallel wave. Do not add hardcoded English or test allowlist exemptions. Use dynamic theme resources. Before your PR, run the repository pr-checklist skill, appropriate reviewer agents, a zero-warning Release build and focused meaningful tests. Deliver actual production renders in both themes, commands/results, preserved behavior inventory, and native-test limitations. An unavailable prerequisite is a handoff issue, not permission to implement another lane's files.
