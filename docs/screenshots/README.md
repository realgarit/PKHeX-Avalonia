# README screenshots

The README's dark and light screenshots were rendered on 2026-09-20 from the real
900×600 Avalonia shell at v1.49.12 (`2b8632255`). They use the existing
`CompactUiCaptureTests.CompactShell_RealCompositionFitsAndCapturesBothThemes`
capture workflow, with a synthetic Scarlet save and demonstration Pokémon.
Legality warnings are expected for those synthetic entities. The trainer name
comes from the checked-in test fixture, not a private user save.

## Regenerate without using the desktop

From the repository root in PowerShell:

```powershell
dotnet build Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release
$env:PKHEX_HEADLESS_CAPTURE = '1'
$env:PKHEX_HEADLESS_CAPTURE_DIR = Join-Path (Get-Location) 'tmp/readme-captures'
dotnet test Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~CompactShell_RealCompositionFitsAndCapturesBothThemes'
Remove-Item Env:PKHEX_HEADLESS_CAPTURE, Env:PKHEX_HEADLESS_CAPTURE_DIR
```

The capture flag selects the Skia-backed headless renderer. The test creates no
native desktop window and does not move the mouse or keyboard focus. It also
produces intermediate editor-section, dropdown, and settings images in `tmp/`;
these are review artifacts, not automatically published screenshots.

Inspect both production frames before copying them into the documentation:

```powershell
Copy-Item tmp/readme-captures/compact-production-dark.png docs/screenshots/pokemon-editor-dark.png
Copy-Item tmp/readme-captures/compact-production-light.png docs/screenshots/pokemon-editor-light.png
```

Keep the full frame, readable text, and actual legality indicators. Do not retouch
the UI to imply behavior that the application does not have. After regeneration,
update the source version above and verify both README image links.

Other PNGs in this directory are earlier documentation assets. The current root
README uses only the two `pokemon-editor-dark/light.png` captures above.
