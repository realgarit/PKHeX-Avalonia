# Approved design specification

## 1. Priority order

Preserve save correctness and feature access → preserve keyboard/accessibility/localization → match compact layout → match cosmetic details. Do not solve overflow by deleting fields, reducing all text to 9px, scaling the whole app with a Viewbox, or increasing the default window to the rejected 1480px layout.

## 2. Geometry (Avalonia DIPs, not physical screen pixels)

| Region | Default target |
|---|---|
| Main client area | 900 wide × 600 high; resizable |
| Minimum client area | 900 × 600 for first production pass |
| Native title bar | Retained, outside the client-area target |
| Menu row | Approximately 35 high, Auto if localization requires |
| Save context/action row | Approximately 49 high |
| Bottom status row | Approximately 27 high |
| Body | Remaining height, two columns, no persistent left rail |
| Editor column | 306 wide target; 300 minimum, 360 resize maximum |
| Storage column | Remaining width, minimum 500 at default density |
| Divider | 1px visual; optional splitter consumes at most 4 DIPs |
| Editor horizontal inset | 16–17 each side |
| Box horizontal inset | 16 each side |
| Editor identity area | 80–94 high; sprite 64–76 |
| Editor navigation | 30–36 high |
| Form controls | 29–30 high in Compact; 5–6 radius |
| Box header | 34–39 high |
| Standard box grid | 6 columns × 5 rows; all slots visible at default size |
| Slot gaps / radius | 5–6 / 6–7 |
| Box sprite | About 40–47 high at default size; maintain aspect ratio |
| Party strip | 6 columns; about 70–76 total including label |
| Settings window | 390 × 492 initial client area; minimum 390 × 420; resizable |

The mockup's 840 minimum is not a production requirement. Verify actual client bounds with native decorations at 100%, 125%, 150%, and 200% scaling; logical 900px can occupy 1800 physical pixels. If the work area cannot contain the target, clamp to the work area and provide accessible local scrolling; do not position buttons below the screen. At ordinary 900 × 600 Compact there must be no whole-shell or box-grid scrollbar. Comfortable density remains supported and may scroll editor content locally.

## 3. Surface and color contract

Seed colors copied from the approved screenshots:

| Semantic role | Light | Dark |
|---|---|---|
| Canvas/background | #F7F7F9 | #191B22 |
| Pane/card | #FFFFFF | #22252E |
| Input | #FBFBFC | #292D37 |
| Divider | #E4E5E9 | #343743 |
| Primary text | #30333C | #E2E4EB |
| Muted reference text | #83868F | #A0A5B4 |
| Rose reference accent | #AD4D53 | #C4717B |
| Selected slot fill | #F5E8EA | #403039 |
| Selected slot border | #B66A73 | #C4717B |
| Success reference | #52836C | #8EC4A7 |

These are source design values, not a claim of WCAG compliance. A must measure actual text/background combinations. Light #83868F is too weak for some small-text uses; dark rose with white button text also requires adjustment. Preserve the visual family while achieving at least 4.5:1 for normal text, 3:1 for large text and necessary control/focus boundaries. Distinguish accent background, on-accent text, and accent text; do not use one color indiscriminately. Retain semantic illegal/warning/shiny states independently of rose selection. Capture the final accessible colors and write the actual values in the foundation PR.

Use DynamicResource references and the existing ThemeDictionaries/ThemeService. No per-view literal palettes, manual color-tree walks, prototype mutable-brush dictionary, gradients, glow, or OS-dependent emoji icons. Outline vector icons should be approximately 14–16px with recognizable strokes. Native Fluent templates remain; target their supported states consistently.

## 4. Typography and controls

Use the existing app font configuration; the reference uses Inter. Entity title approximately 20–22, workspace title 13–14, controls/ordinary labels 11–12, ancillary labels at least 10 where readable. The reference's 9px captions are not mandatory. Do not reduce below existing accessible requirements to achieve a screenshot match. Long names ellipsize with a full tooltip; editable values do not disappear behind chevrons.

Every TextBox/ComboBox/FilterableComboBox/NumericUpDown must have coherent normal, hover, focus, disabled, invalid, selected, and popup states in both themes. Match dropdown popup width to its input unless content needs more room; cap popup height and scroll its list. Up/Down, typing search, Enter, Escape, Tab, and clicking outside must work. Do not replace searchable species/item inputs with a five-item ComboBox from the prototype. Theme open popups, context menus, numeric spinner buttons, checks, selection, and focus indicators as well as closed controls.

## 5. Shell behavior

Use real native Menu/MenuItem controls and the existing command/capability registry. Preserve File/Edit/Save/Tools/Window/Help access and all existing shortcuts, even if menu labels differ from the simplified reference. Remove the 176px workspace rail. Keep Pokémon/Save/Reports switching in Window menu and Ctrl+1/2/3; Ctrl+K keeps the searchable launcher. The compact save row shows actual loaded-save context and a genuine Save/Export command. Empty state must show Open and useful help, never sample Pokémon. Preserve update notifications, busy states, HaX warnings, unsaved-change protection, and error messages without overlaying interactive content.

Expose Light/Dark through a small top-level dropdown using the existing persisted IThemeService. Read current theme when opening/synchronizing the picker; no local-only boolean. Keep the full Settings entry. Do not add System/HighContrast choices: current source intentionally normalizes those legacy enum values to Dark.

Do not infer that the mockup Apply/Reset pair defines a production transaction. Keep existing editor-to-slot operations, prompts and undo semantics. A footer action may call an existing exact operation only with a truthful localized label; otherwise omit the mockup action rather than inventing one.

## 6. Pokémon editor

Keep CurrentPokemonEditor and every existing generation-sensitive field and command. Primary compact navigation: Main, Stats, Met, Moves, OT/Misc. Contest, Memory and Ribbons must remain reachable when supported via a clearly labeled More menu (and keyboard), never a clipped invisible tab. Local scrolling inside an active editor section is allowed; identity and navigation stay visible. Document the old-to-new binding inventory before changing the XAML.

Use a restrained identity row: real sprite, actual entity title/level, legality access and shiny state. Main should prioritize species, nickname, level/nature, ability, held item, form/gender and ball. Additional production fields stay in the same tab with local scroll or a labeled expander. Form/gender, PID, nickname flags, OT/HT identities, PP, IV/EV, hypertraining and HaX stats keep their existing semantics.

The portrait and slots use ISpriteRenderer PNG bytes with the host converter; no Bitmap/file IO in Presentation. Do not enlarge low-resolution sprites beyond sensible bounds or add downloaded art.

## 7. Storage and party

Bind to actual Slots and supported capacity; 30 slots is the ordinary-box reference, not a constant to impose on every save format. Keep slot numbers, selected state, legality/shiny markers and full nickname tooltip. Empty slots remain identifiable and focusable. Selection: subtle rose fill + border, never solely color as the indicator.

Keep click, double-click, Ctrl/Shift/Alt/Option, keyboard, context menu, drag/drop, undo, overwrite prompts and move-to-party/box behavior. The mini party strip MUST be interactive and use the same routing rules as the full PartyViewer; the existing passive decorative strip is not sufficient. Do not hardwire a click into View if current resolver specifies selection/set/delete behavior. Preserve LGPE no-party behavior and party compaction.

## 8. Secondary windows

Restyle the real SettingsView rather than adding a fake Preferences VM. Preserve every current setting. Put Appearance first; use section navigation or scrolling for Backup, Editing, Sprites and Startup so the window remains small. Keep current live persistence of theme/density and Save semantics for other settings. The prototype Done button is not permission to silently discard production edits. Do not implement its decorative Accent choice.

Preserve each production window's modal/modeless lifecycle. The reference Preferences is modeless only for visual exploration; do not change production settings ownership just to imitate it. WindowService sizing changes must be scoped to Settings, not shrink every database/raid/editor to 390px. Existing detached Box/Party tools remain singleton-per-VM, owned, and closed on save changes. All already-open windows and popups must update with theme changes.
