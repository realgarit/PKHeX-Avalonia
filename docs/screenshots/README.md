# README screenshots

All nine images referenced by the root README were refreshed on 2026-10-03 from the real Avalonia views. The main shell captures use the default 900x600 compact layout, with the theme picker matching the rendered palette.

The dark and light lead frames use `CompactUiCaptureTests.ReadmeMarkers_UsesPublicSaveClone`. This loads `Tests/savefiles/gen9a_legendsza.main`, selects the existing Alpha Machamp in Box 1, and makes that Pokemon Shiny in the in-memory copy. Both original PKHeX markers appear together in the box and editor preview. Core legality checks stay enabled and the selected Pokemon passes. The fixture file is never written, and the demonstration is not a claim of an in-game Shiny capture.

`ReadmeHero_UsesLegalSaveFixture` also verifies the unmodified source box, party, and selected Charizard against Core legality checks. Its original-fixture frames remain available for comparison. No private save data is used.

## Regenerate without using the desktop

From the repository root in PowerShell:

```powershell
dotnet build Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release
$env:PKHEX_HEADLESS_CAPTURE = '1'
$env:PKHEX_HEADLESS_CAPTURE_DIR = Join-Path (Get-Location) 'tmp/readme-captures'

dotnet test Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ReadmeMarkers_UsesPublicSaveClone|FullyQualifiedName~ReadmeHero_UsesLegalSaveFixture|FullyQualifiedName~CaptureTaskAwareShellStates_WhenEnabled_WritesPng|FullyQualifiedName~CaptureIssueSweepNextEditors_WhenEnabled_WritesPng'
Remove-Item Env:PKHEX_HEADLESS_CAPTURE, Env:PKHEX_HEADLESS_CAPTURE_DIR
```

The flag selects Skia-backed headless rendering. These captures create no native desktop windows and do not move the user's mouse or keyboard focus.

Inspect every frame before publishing it. Keep the full frame and actual legality indicators; do not retouch the UI or add markers to the images.

| Capture method | Generated file | Published file |
|---|---|---|
| `ReadmeMarkers_UsesPublicSaveClone` | `readme-markers-dark.png` | `pokemon-editor-dark.png` |
| Same | `readme-markers-light.png` | `pokemon-editor-light.png` |
| `CaptureTaskAwareShellStates_WhenEnabled_WritesPng` | `shell-save.png` | `gallery-trainer.png` |
| Same | `shell-inventory.png` | `gallery-inventory.png` |
| Same | `shell-reports.png` | `gallery-reports.png` |
| Same | `shell-launcher.png` | `gallery-launcher.png` |
| `CaptureIssueSweepNextEditors_WhenEnabled_WritesPng` | `next-pokedex8b-editor.png` | `gallery-pokedex.png` |
| Same | `next-seal-stickers-editor.png` | `gallery-stickers.png` |
| Same | `next-tech-record-editor.png` | `gallery-records.png` |

For example:

```powershell
Copy-Item tmp/readme-captures/readme-markers-dark.png docs/screenshots/pokemon-editor-dark.png
Copy-Item tmp/readme-captures/readme-markers-light.png docs/screenshots/pokemon-editor-light.png
```

Verify copied files against their capture hashes and check every README image link. Update this provenance when regenerating the gallery.

## Gallery scope

The root README retains its centered lead image and five collapsible gallery sections. Trainer, Inventory, Reports, and the launcher use the checked-in Z-A fixture. Inventory selects Key Items to show the restored individual artwork. The shell capture applies the Light theme explicitly and refreshes the picker; no temporary production changes are needed.

The BDSP Pokedex, Seal Stickers, and Technical Records frames use blank/synthetic save and Pokemon objects. Their displayed state and controls are the actual production views, including staged actions where supported.

Other PNGs in this directory are earlier assets retained for existing consumers.

## Flathub screenshots

Flathub requires real Linux window captures with the native title bar, rounded corners and shadow, at most 1000x700. The three `flathub-*.png` files, referenced by the AppStream MetaInfo (`packaging/linux/io.pkhex.avalonia.metainfo.xml`), are GNOME Shell 50 window screenshots of the v1.98.0 Linux build, captured on 2026-10-28 in a headless GNOME session in WSL (Ubuntu 26.04). They are unedited 928x668 RGBA images: the 900x600 window plus GNOME's shadow. The app runs with default settings on its default X11 backend, and the third frame switches to the Light theme. The public `Tests/savefiles/gen9a_legendsza.main` fixture is opened through the real File > Open dialog.

| File | Content |
|---|---|
| `flathub-01-pokemon-editor.png` | Charizard on the Main tab with Box 1 and the party (dark theme, default) |
| `flathub-02-inventory.png` | Inventory, Medicine pouch (dark theme) |
| `flathub-03-stats-light.png` | Venusaur on the Stats tab (light theme) |

To regenerate, run as root in a disposable Ubuntu/WSL environment with `gnome-shell`, `xwayland`, `gjs` and `xdg-desktop-portal-gtk` installed. Publish a self-contained `linux-x64` build first, then keep `run-session.sh` running while `capture.sh` works:

```bash
dotnet publish PKHeX.Avalonia/PKHeX.Avalonia.csproj -c Release -r linux-x64 --self-contained -o tmp/flathub-publish
bash Scripts/flathub-screenshots/run-session.sh &
bash Scripts/flathub-screenshots/capture.sh tmp/flathub-publish tmp/flathub-screenshots
```

The session enables a local helper extension that turns on GNOME Shell's unsafe mode, which `capture.sh` needs for input and window screenshots; use it only in a throwaway session. Inspect every frame before copying it here. A dropped click shows up as the wrong tab or an empty editor; rerun the capture instead of editing images.
