# README screenshots

The README's dark and light lead screenshots were rendered on 2026-09-26 from
real 900×600 Avalonia views, including the direct top-row workspace tabs. They use
`CompactUiCaptureTests.ReadmeHero_UsesLegalSaveFixture` with the repository test
save `Tests/savefiles/gen9a_legendsza.main`, Box 1 and its existing party.
Charizard (Box 1, slot 6) is selected. The capture asserts that every displayed
box/party slot and the selected Pokémon pass legality checks; warnings remain
enabled. The source save file and Pokémon data are not rewritten.

## Regenerate without using the desktop

From the repository root in PowerShell:

```powershell
dotnet build Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release
$env:PKHEX_HEADLESS_CAPTURE = '1'
$env:PKHEX_HEADLESS_CAPTURE_DIR = Join-Path (Get-Location) 'tmp/readme-captures'
dotnet test Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~ReadmeHero_UsesLegalSaveFixture'
Remove-Item Env:PKHEX_HEADLESS_CAPTURE, Env:PKHEX_HEADLESS_CAPTURE_DIR
```

The capture flag selects the Skia-backed headless renderer. The test creates no
native desktop window and does not move the mouse or keyboard focus. The two
theme variants are written to `tmp/`; only the reviewed lead frames are published in this directory.

Inspect both production frames before copying them into the documentation:

```powershell
Copy-Item tmp/readme-captures/readme-legal-dark.png docs/screenshots/pokemon-editor-dark.png
Copy-Item tmp/readme-captures/readme-legal-light.png docs/screenshots/pokemon-editor-light.png
```

Keep the full frame, readable text, and actual legality indicators. Do not retouch
the UI to imply behavior that the application does not have. After regeneration,
update the capture provenance above and verify both README image links.

## Expandable gallery

The root README keeps a centered 600px preview and nine images inside five
GitHub-native `details`/`summary` sections, collapsed by default.

Seven additional images were captured on 2026-09-20 from the same application
source using `HeadlessFeatureCaptureTests`:

| Capture method | Generated file | Published file |
|---|---|---|
| `CaptureTaskAwareShellStates_WhenEnabled_WritesPng` | `shell-save.png` | `gallery-trainer.png` |
| Same | `shell-inventory.png` | `gallery-inventory.png` |
| Same | `shell-reports.png` | `gallery-reports.png` |
| Same | `shell-launcher.png` | `gallery-launcher.png` |
| `CaptureIssueSweepNextEditors_WhenEnabled_WritesPng` | `next-pokedex8b-editor.png` | `gallery-pokedex.png` |
| Same | `next-seal-stickers-editor.png` | `gallery-stickers.png` |
| Same | `next-tech-record-editor.png` | `gallery-records.png` |

Use the capture environment variables above and filter for these methods to
regenerate the gallery. The shell images use the checked-in
`Tests/savefiles/gen9a_legendsza.main` fixture. The three auxiliary editors use
blank/synthetic save and Pokémon objects. None use private user saves.

For this capture session, the shell fixture explicitly applied `AppTheme.Light`
and called `RefreshThemeSelection()` immediately after constructing
`HeadlessAppFixture`, before loading the save. This temporary test-only setup
kept the theme picker consistent with the rendered palette; it was removed
after capture. Repeat that setup before building if regenerating these exact
light-theme frames. Production application code was unchanged.

Other PNGs in this directory are earlier documentation assets retained for
existing consumers.
