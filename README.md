# PKHeX Avalonia

![License](https://img.shields.io/badge/License-GPLv3-blue.svg)
![CI](https://github.com/realgarit/PKHeX-Avalonia/actions/workflows/ci.yml/badge.svg)
![Release](https://img.shields.io/github/v/release/realgarit/PKHeX-Avalonia?label=Latest%20Release)

PKHeX Avalonia is the cross-platform [PKHeX](https://github.com/kwsch/PKHeX) port built with the Avalonia UI framework, bringing the classic Pokémon save editor to **Windows**, **macOS**, and **Linux** with a native look and feel.

---

## Download

Grab the latest release for your platform from the [Releases](https://github.com/realgarit/PKHeX-Avalonia/releases/latest) page:

| Platform | File |
|----------|------|
| Windows (x64) | `PKHeX-Avalonia-win-x64.zip` |
| Linux (x64) | `PKHeX-Avalonia-linux-x64.zip` |
| macOS Apple Silicon | `PKHeX-Avalonia-osx-arm64.zip` |
| macOS Intel | `PKHeX-Avalonia-osx-x64.zip` |

All releases are self-contained — no .NET runtime installation required.

**macOS Note:** The app is ad-hoc signed but not notarized, so on first launch macOS will warn "unidentified developer". To open it:
1. Right-click the app → select **Open** → click **Open** in the dialog
2. Or in Terminal: `xattr -d com.apple.quarantine ~/Downloads/PKHeX.Avalonia.app`

---

## Project Structure

The solution follows a clean, layered architecture so the cross-platform UI stays decoupled from the upstream logic:

| Project | Role | References |
|---------|------|------------|
| **PKHeX.Core** | Shared save/entity/legality logic, kept 1:1 with [upstream PKHeX](https://github.com/kwsch/PKHeX). Never modified directly. | — |
| **PKHeX.Application** | Application layer — use-cases and service abstractions over Core. | Core |
| **PKHeX.Infrastructure** | Platform & I/O implementations (file access, persistence, OS integration). | Application, Core |
| **PKHeX.Presentation** | MVVM view-models and presentation logic, UI-framework agnostic. | Application, Core |
| **PKHeX.Avalonia** | The Avalonia UI — views, styles, and the cross-platform desktop app. | all of the above |

Tests live under `Tests/` (`PKHeX.Core.Tests`, `PKHeX.Avalonia.Tests`, and `PKHeX.Architecture.Tests`, which enforces the layer boundaries above).

## Features

### Save editing
* **Wide format support:** Core-series saves from **Gen 1 through Gen 9**, including Let's Go, Legends: Arceus, BDSP, and Legends: Z-A (`.sav`, `.dsv`, `.dat`, `.gci`, `.bin`, …).
* **Live legality:** every loaded Pokémon is checked against upstream legality logic, with a legality report and one-click legalization.
* **Entity files:** import and export `.pk*`, `.ck3`, `.xk3`, `.pb7`, and more.
* **Transferring:** move Pokémon between generations with automatic format conversion.

### Pokémon & box editing
* **Full entity editor:** Main, Stats, Met, Moves, OT/Misc, Contest, Memory, and Ribbons — all reflecting the active save's generation.
* **Visual box view:** sprite-based box grid with navigation, plus box manipulation and box-layout tools.
* **Trainer editor:** identity, money, play time, currencies, and adventure info.

### Tools & databases
* **PKM / Mystery Gift / Encounter databases:** searchable browsers backed by a shared, resizable & collapsible filter rail.
* **Batch editor:** bulk-edit Pokémon with searchable batch instructions.
* **Per-generation save editors:** Pokédex, Hall of Fame, Secret Base, Festival Plaza, Daycare, Records, Mailbox, event flags, and many more — surfaced per game under **Tools → Save Editors**.
* **Showdown import/export** and **Mystery Gift** (`.pgt`, `.pcd`, `.pgf`, `.wc*`) support.

### Experience
* Native look and feel on **Windows**, **macOS**, and **Linux** via Avalonia.
* Themed dark UI with modeless tool windows that can sit alongside the main editor.

## Building from Source

### Requirements
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Run
```bash
dotnet run --project PKHeX.Avalonia
```

### Build
```bash
dotnet build PKHeX.sln -c Release
```

### Test
```bash
dotnet test PKHeX.sln
```

### Publish (example: macOS ARM)
```bash
dotnet publish PKHeX.Avalonia -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

## Screenshots

**Pokémon editor & box view** — full entity editor alongside the sprite-based box grid.
![Pokémon editor and box view](docs/screenshots/pokemon-editor.png)

**Inventory editor** — per-pouch item editing (Medicine, Balls, Berries, Mega Stones, …).
![Inventory editor](docs/screenshots/inventory-editor.png)

**PKM Database** — searchable browser with the resizable / collapsible filter rail.
![PKM Database](docs/screenshots/pkm-database.png)

**Per-generation save editors** — Gen 1–9 plus game-specific tools under Tools → Save Editors.
![Save editors menu](docs/screenshots/save-editors-menu.png)

## Credits
This fork is built on the incredible work of the [PKHeX team](https://github.com/kwsch/PKHeX).

* **Logic & Research:** [PKHeX](https://github.com/kwsch/PKHeX)
* **QR Codes:** [QRCoder](https://github.com/codebude/QRCoder) (MIT)
* **Sprites:** [pokesprite](https://github.com/msikma/pokesprite) (MIT)
* **Arceus Sprites:** National Pokédex - Icon Dex project and contributors.
