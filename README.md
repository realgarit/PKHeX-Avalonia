# PKHeX Avalonia

![License](https://img.shields.io/badge/License-GPLv3-blue.svg)
![CI](https://github.com/realgarit/PKHeX-Avalonia/actions/workflows/ci.yml/badge.svg)
![Release](https://img.shields.io/github/v/release/realgarit/PKHeX-Avalonia?label=Latest%20Release)

PKHeX Avalonia is a cross-platform port of [PKHeX](https://github.com/kwsch/PKHeX). It's the classic Pokémon save editor, built with Avalonia so it runs on **Windows**, **macOS**, and **Linux**.


## Download

Get the latest build for your platform from the [Releases](https://github.com/realgarit/PKHeX-Avalonia/releases/latest) page:

| Platform | File |
|----------|------|
| Windows (x64) | `PKHeX-Avalonia-win-x64.zip` |
| Linux (x64) | `PKHeX-Avalonia-linux-x64.zip` |
| macOS Apple Silicon | `PKHeX-Avalonia-osx-arm64.zip` |
| macOS Intel | `PKHeX-Avalonia-osx-x64.zip` |

Every build is self-contained, so you don't need to install .NET.

**macOS note:** the app is ad-hoc signed but not notarized. So on first launch macOS warns about an "unidentified developer". To open it:
1. Right-click the app, pick **Open**, then click **Open** in the dialog.
2. Or run `xattr -d com.apple.quarantine ~/Downloads/PKHeX.Avalonia.app` in Terminal.


## Project Structure

The code is split into layers so the UI stays separate from the PKHeX logic:

| Project | What it does | Uses |
|---------|--------------|------|
| **PKHeX.Core** | Save, entity, and legality logic. Kept 1:1 with [upstream PKHeX](https://github.com/kwsch/PKHeX). We don't change it directly. | None |
| **PKHeX.Application** | Use-cases and service interfaces on top of Core. | Core |
| **PKHeX.Infrastructure** | File access and other OS bits. | Application, Core |
| **PKHeX.Presentation** | View-models. No UI framework here. | Application, Core |
| **PKHeX.Avalonia** | The Avalonia UI: views, styles, and the desktop app. | all of the above |

Tests live under `Tests/`: `PKHeX.Core.Tests`, `PKHeX.Avalonia.Tests`, and `PKHeX.Architecture.Tests` (which checks the layers above stay separate).

## Features

* Edit saves from Gen 1 to Gen 9, plus Let's Go, Legends: Arceus, BDSP, and Legends: Z-A.
* Edit any Pokémon: stats, moves, ribbons, memories, and more.
* Checks legality as you go and can fix illegal Pokémon for you.
* Import and export Pokémon files and Showdown sets.
* Move Pokémon between generations. It converts the format for you.
* Search your boxes with the PKM, Mystery Gift, and Encounter databases.
* Edit many Pokémon at once with the batch editor.
* Game specific editors under Tools, like Pokédex, Hall of Fame, and Secret Base.


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

Pokémon editor and box view. The full editor next to the sprite box grid.
![Pokémon editor and box view](docs/screenshots/pokemon-editor.png)
Inventory editor. Edit items by pouch (Medicine, Balls, Berries, Mega Stones, and so on).
![Inventory editor](docs/screenshots/inventory-editor.png)
PKM Database. Search your boxes with a filter rail you can resize or hide.
![PKM Database](docs/screenshots/pkm-database.png)
Save editors. Gen 1 to 9 plus game specific tools under Tools, then Save Editors.
![Save editors menu](docs/screenshots/save-editors-menu.png)

## Credits
This fork is built on the work of the [PKHeX team](https://github.com/kwsch/PKHeX).

* **Logic & Research:** [PKHeX](https://github.com/kwsch/PKHeX)
* **QR Codes:** [QRCoder](https://github.com/codebude/QRCoder) (MIT)
* **Sprites:** [pokesprite](https://github.com/msikma/pokesprite) (MIT)
* **Arceus Sprites:** National Pokédex Icon Dex project and contributors.
