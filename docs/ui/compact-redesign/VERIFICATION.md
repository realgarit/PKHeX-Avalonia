# Acceptance and verification

## Automated baseline

Run from each owning worktree. A full Release build must have zero warnings/errors. Run relevant focused tests while editing; after the final changes run the full solution gates. Record actual totals and pre-existing skips instead of copying historical AGENTS.md counts.

```powershell
dotnet build PKHeX.sln -c Release
dotnet test PKHeX.sln -c Release --no-build
```

Required suites include Architecture, AccessibilityAudit, LocalizationAudit, Theme, UiDensity, ResponsiveShell, FilterableComboBox/Geometry, BoxViewer, PartyViewer, DetachedWorkspace, SlotModifierClickRouting, SlotClickHitTest, SlotDragTransfer, SlotDragDropWiring, SlotOperations, MainWindowFileCommand, PokemonEditorReports. Keep generation-specific and legality tests passing.

## Rendered production evidence

F adds `Tests/PKHeX.Avalonia.Tests/Harness/CompactUiCaptureTests.cs`, using the real composition fixture and the existing Skia capture machinery, not a screenshot of the reference project. Each lane may add its own captures in its uniquely owned tests. Do not concurrently edit HeadlessFeatureCaptureTests.cs.

```powershell
$env:PKHEX_HEADLESS_CAPTURE='1'
$env:PKHEX_HEADLESS_CAPTURE_DIR=Join-Path (Get-Location) 'tmp/compact-ui-captures'
dotnet test Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release --filter 'FullyQualifiedName~CompactUiCaptureTests'
Remove-Item Env:PKHEX_HEADLESS_CAPTURE
Remove-Item Env:PKHEX_HEADLESS_CAPTURE_DIR
```

Capture tests must assert a nonzero real frame was produced when the opt-in flag is set. A skipped/no-frame capture is not passing visual evidence. Inspect every image; ensure text, dividers and real Pokémon are visible, not a blank headless bitmap. Include the actual client size and theme in filenames. Store bulk artifacts under worktree tmp or CI artifacts, not committed generated binaries.

## Matrix (both Light and Dark)

| Scenario | What must be visible/working |
|---|---|
| 900×600 Compact, loaded supported save | Identity, primary editor controls, all ordinary box slots, six party positions, menu/save/status; no shell horizontal scrollbar |
| 1024×720 and larger | Sensible growth; no giant sprites, stretched tiny input columns or reappearing left rail |
| Comfortable density | All controls reachable; local editor/settings scroll; no clipped actions |
| No save loaded | Truthful Open state; no demo values |
| Main/Stats/Met/Moves/OT | Real bindings and editable controls; all conditional sections reachable |
| Contest/Memory/Ribbons overflow | Discoverable when supported, absent/disabled correctly when not |
| Gen 2, Gen 6/7, SWSH/SV, LGPE, PA9/ZA | Existing supported fixture behavior; storage capacity/party support and generation fields correct |
| Long names / German / Japanese | Layout remains usable; full names via tooltip; no tofu glyphs |
| Settings 390×492 | Appearance and navigation visible; remaining settings scroll; save/close action visible |
| Existing open Settings + detached Box/Party | Theme changes update every surface live without reopening |
| Trainer, Inventory, database grid, one generation-specific editor | Shared theme changes do not corrupt legacy surfaces or overflow |
| Busy / error / HaX / illegal / update notification | Truthful states, accessible messages and unclipped actions |

## Native interaction gate

Run the actual application using disposable repository fixtures through File > Open, not private saves and not an assumed CLI save-path argument. At least Windows native runtime evidence is required in this environment; macOS/Linux must be explicitly marked pending unless actually checked.

- Open species/item/nature dropdowns; type/filter, arrow, Enter, Escape, Tab, click outside. Check the popup's background, border, item selection, clipping, placement and scroll in both themes. Popup roots may be outside a simple window RenderTargetBitmap; use native screenshots for this evidence.
- Open menus and context menus, hover/focus/disabled controls, invalid numeric fields, and toggle/check controls. Verify focus outline contrast without relying on pointer hover.
- Switch theme with a secondary window and a popup open, close/reopen Settings, restart application; verify persistence and theme picker synchronization.
- Click/select/open/set/delete/move Pokémon via existing modifier resolver and keyboard. Drag/drop between Box and Party; verify undo, overwrite confirmation, party compaction, and LGPE guards.
- Reinvoke detached tool: same window focuses. Change/close save: old tools close. Closing a secondary window does not close the application.
- Move to 125/150/200% scale and a constrained display; verify native titlebar/client bounds, work-area placement, resizing and actions reachable by keyboard.
- Exercise real Save As against a temporary copy and reload it; visual redesign must not bypass unsaved-change/backup behavior. Never use the prototype to claim this passes.

## Test policy

Replace superseded cosmetic assertions with new meaningful layout/theme/accessibility assertions. Never remove functional assertions or add skips to force green. Prefer measured arranged bounds, viewmodel outcomes and hit-testing over brittle source-string checks. No standalone mock tests that only assert the implementation's constants equal themselves.

## Final acceptance

F signs off only when the integrated app matches the reference hierarchy at 900×600, feature inventory is accounted for, all gates pass, and real window/dropdown interactions are observed. Run architecture-boundary-reviewer for cross-project changes and xaml-mvvm-reviewer for view/viewmodel changes, as applicable. Run the repository pr-checklist skill before opening implementation PRs. Disclose untested platforms. Keep Core/AutoMod and UIVersion untouched and use conventional PR titles. Release/Discord communication follows repository policy only after verified merge and release.
