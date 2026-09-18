# Agent D — compact storage and reusable interactive party strip

## Objective

Match the compact box and party visuals without breaking slot operations. Produce the PartyStrip component API that B will consume; your PR integrates before B.

## Owned files

- PKHeX.Avalonia/Views/BoxViewer.axaml and BoxViewer.axaml.cs
- PKHeX.Avalonia/Views/PartyViewer.axaml and PartyViewer.axaml.cs
- New PKHeX.Avalonia/Views/PartyStrip.axaml and PartyStrip.axaml.cs
- New Tests/PKHeX.Avalonia.Tests/CompactStorageTests.cs
- Existing SlotModifierClickRoutingTests, SlotClickHitTestTests, SlotDragDropWiringTests, SlotDragTransferTests only for related new coverage
- If shared view-layer routing extraction is necessary, new PKHeX.Avalonia/Controls/PartySlotInteraction.cs (no VM/domain operations in it)

Read BoxViewerViewModel, PartyViewerViewModel, SlotClickActionResolver, SlotDragTransfer and MainWindowViewModel.SlotOperations. They are read-only unless F assigns a specific change. Do not edit MainWindow.axaml or its passive strip: B owns composition.

## Implementation sequence

1. Inventory exact click/modifier/double-click/keyboard/context-menu/drag-drop semantics and existing tests before changing the templates.
2. Flatten BoxViewer's oversized card/header padding. Keep real box navigation, occupancy, rename/pop-out/move operations and tooltips. Ordinary grid is 6×5 at 900px shell; account for actual per-save slot capacity.
3. Apply A's compact-slot class; subtle rose selection, 5–6px gaps, readable caption, slot number and tooltip. Keep actual sprites and legality/shiny state. Render empty/occupied/selected/drag-over/focus states.
4. Implement PartyStrip exactly as CONTRACTS.md: same PartyViewerViewModel DataContext, six positions when supported, real commands/routing, no duplicate data store. Share view-layer routing with full PartyViewer if necessary rather than copying behavior that can drift.
5. Preserve AddHandler(PointerPressedEvent, ..., RoutingStrategies.Tunnel, handledEventsToo:true). Button consumes normal pointer events; simply wiring a bubbling handler breaks Ctrl/Shift/Alt. Preserve slot Tag/DataContext and hit-test-transparent child artwork so drops resolve correctly.
6. Preserve safe copy/move/overwrite/undo behavior, party compaction after removal, and LGPE HasParty restrictions. Do not conflate “selected” with immediate editing if the resolver distinguishes them.
7. Test the inline component with a real fixture outside MainWindow so this PR is independent; check 0,1,6 occupied positions, unsupported party, routed modifier clicks, keyboard and drop targets. Keep detached full viewers functional.

## Done when

PartyStrip is compiled and independently tested on the accepted base, the existing main window still works, Box/Party operations retain meaning, and B can consume the component with only its specified DataContext. Provide light/dark storage captures, exact component availability SHA and focused interaction test results.

## Mandatory working rules

Read root AGENTS.md and ../README.md, ../DESIGN.md, ../CONTRACTS.md, ../VERIFICATION.md before changing code. Inspect reference/compact-light.png and compact-dark.png; inspect preferences images where relevant. Use the supplied isolated worktree and foundation SHA. You are not alone in the codebase: do not revert others' edits. Own only the files below; request an explicit ownership transfer for anything else. Core, AutoMod, UIVersion and all unrelated behavior are immutable for this task. Preserve compiled bindings, localization, accessibility and native Fluent control behavior. Reference-prototype is visual evidence, not production architecture.

Send new resource/string requirements to F; do not independently edit shared styles/localization/root AGENTS.md during the parallel wave. Do not add hardcoded English or test allowlist exemptions. Use dynamic theme resources. Before your PR, run the repository pr-checklist skill, appropriate reviewer agents, a zero-warning Release build and focused meaningful tests. Deliver actual production renders in both themes, commands/results, preserved behavior inventory, and native-test limitations. An unavailable prerequisite is a handoff issue, not permission to implement another lane's files.
