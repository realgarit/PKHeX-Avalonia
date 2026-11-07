# Visual regression baselines

Functional and layout tests can pass while spacing, clipping, icons, hierarchy or slot states
regress. The visual acceptance matrix renders the main desktop surfaces with real Skia drawing and
compares each frame with a reviewed PNG baseline that is committed to the repository.

- Tests: `Tests/PKHeX.Avalonia.Render.Tests/VisualRegression/`
- Baselines: `Tests/PKHeX.Avalonia.Render.Tests/VisualRegression/Baselines/*.png` (24 frames, about 3 MB)

## Acceptance matrix

Every frame is rendered in **Dark**, **Light** and **HighContrast**. HighContrast is no longer a
user choice (settings normalize it to Dark), but its palette is still in `Styles/Theme.axaml` for
legacy accessibility settings, so it stays covered.

| Frame | Size | What it shows |
| --- | --- | --- |
| `shell-box-editor-main` | 900x600 | Compact shell, Box workspace, Pokémon editor **Main** section |
| `shell-box-editor-stats` | 900x600 | Same shell, editor **Stats** section |
| `shell-box-editor-met` | 900x600 | Same shell, editor **Met** section |
| `shell-box-editor-moves` | 900x600 | Same shell, editor **Moves** section |
| `shell-box-editor-ot` | 900x600 | Same shell, editor **OT/Misc** section |
| `shell-party` | 900x600 | Party workspace six-slot grid with the shell around it |
| `dialog-settings` | 420x520 | Settings dialog (Linux-only section pinned off) |
| `dialog-box-report` | 900x600 | Box Report tool window over the populated save |

The save is the public `Tests/savefiles/gen9a_legendsza.main` fixture, changed in memory only:

| State | Slot in box 1 |
| --- | --- |
| Legal, **shiny** and **Alpha**, **selected**, loaded in the editor (legal pill) | the fixture's Alpha Machamp, made shiny |
| **Illegal** (warning marker, `False` in Box Report) | first other Pokémon, level 1 below met level 50 |
| **Held item** (Oran Berry overlay) | second other Pokémon |
| **Hovered** (`:pointerover` set on the slot button) | third other Pokémon |
| **Keyboard focused** (`Focus(NavigationMethod.Tab)`, focus-visible ring) | fourth other Pokémon |
| **Empty** | last occupied slot, cleared |

The test asserts that each state is really present (shiny, Alpha, legal/illegal, held item,
empty, selected) before it compares pixels, so a fixture change cannot silently remove a state
from the baseline.

## How frames are compared

A pixel counts as different only when its largest RGBA channel delta is above **24** *and* no pixel
in the 3x3 neighbourhood of the other image is within that tolerance (checked in both directions).
This absorbs one-pixel antialiasing and glyph-edge differences, but not moved, resized, recoloured,
clipped or missing content. A frame fails when more than **0.2 %** of its pixels differ (1,080 pixels
at 900x600), or when its size changes. `VisualBaselineMetricTests` guards these numbers with
synthetic frames: a one-pixel text shift plus colour noise passes; a four-pixel row move, a clipped
button and a recoloured surface fail. Locally, repeated Windows runs are pixel-identical.

### Cross-platform choice

Inter is bundled (`WithInterFont`), so layout, glyph advances and line heights match on every
operating system. Skia still rasterizes glyph coverage with the platform scaler (DirectWrite on
Windows, CoreText on macOS, FreeType on Linux), so glyph edges differ slightly between operating
systems (for example, macOS draws "1" one pixel before the line start). Keeping three sets of
baselines would triple the repository size and need three machines to regenerate.

So the baselines are **generated on Windows**, and the diff **fails the build on Windows only**.
On Linux and macOS CI the same frames are rendered, compared and published, and the measured
difference is written to the job summary, but a mismatch does not fail the run. Set
`PKHEX_VISUAL_ENFORCE=1` to enforce on any OS (or `0` to turn enforcement off).

## When a frame does not match

The test does not stop at the first frame: it checks the whole matrix and then fails with one line
per mismatched frame. For every frame it writes `<name>.actual.png`; for a mismatch it also writes
`<name>.expected.png` (the baseline, "before") and `<name>.diff.png` (faded baseline with every
counted pixel in magenta). They go to `bin/<config>/net10.0/TestResults/visual-regression/`
(override with `PKHEX_VISUAL_ARTIFACT_DIR`) together with `visual-summary.txt`.

CI uploads these files in the `test-results-<os>` artifact of every run, and the
"Report visual regression summary" step prints the per-frame table in the job summary. A PR that
changes the UI therefore publishes before/after screenshots: the expected/actual/diff PNGs when the
baselines are stale, and the changed baseline PNGs in the PR diff when they were regenerated.

## Regenerate the baselines

Regenerate on **Windows** after an intended UI change, review every changed PNG, and commit them
with the change:

```powershell
$env:PKHEX_VISUAL_UPDATE = '1'; dotnet test Tests/PKHeX.Avalonia.Render.Tests -c Release --filter "FullyQualifiedName~VisualRegression"; Remove-Item Env:PKHEX_VISUAL_UPDATE
```

```bash
PKHEX_VISUAL_UPDATE=1 dotnet test Tests/PKHeX.Avalonia.Render.Tests -c Release --filter "FullyQualifiedName~VisualRegression"
```

In update mode every frame is written to the source `Baselines` folder instead of being compared
(blank frames are still rejected). Then run the same command without the variable; every frame
should report `match` in `visual-summary.txt`. `git diff --stat` shows which frames changed, and
GitHub shows a before/after image view for each changed PNG in the PR.

## Automated versus human review

| Area | Automated here | Still needs human review |
| --- | --- | --- |
| Layout, spacing, clipping, hierarchy at 900x600 | Yes, pixel diff (Windows) | Other window sizes and resizing |
| Theme colours (Dark, Light, HighContrast) | Yes | OS high-contrast modes and accent colours |
| Slot states: legal/illegal, shiny, Alpha, held item, selected, empty | Yes, asserted and diffed | – |
| Hovered and keyboard-focused slot appearance | Yes, simulated (`:pointerover`, Tab focus) | Real pointer hover, hover timing, focus order |
| Editor sections Main/Stats/Met/Moves/OT, Party, Settings, Box Report | Yes | Other editors and dialogs (see the other render tests) |
| Icons and sprites rendered in place | Yes | – |
| Font rendering (hinting, subpixel antialiasing, system font fallback) | No: headless Skia with bundled Inter | Native check on Windows, macOS and Linux |
| Window placement, title bars, multi-monitor, OS scaling (125–200 %, Retina) | No | Native check on Windows, macOS and Linux |
| Screen readers and native menus | No (names are checked by `AccessibilityAuditTests`) | Native check |
| Animations and transitions | No (frames are taken after they settle) | Native check |

Native checklist before a release with UI changes, on Windows, macOS and Linux (X11, and Wayland
when enabled): open a save, check font rendering in the shell and editor, hover and focus box and
party slots, open and move dialogs, and check the shell at 100 % and at a high OS scale.
