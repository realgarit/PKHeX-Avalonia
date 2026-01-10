# PKHeX Avalonia

![License](https://img.shields.io/badge/License-GPLv3-blue.svg)

PKHeX Avalonia is a cross-platform port of [PKHeX](https://github.com/kwsch/PKHeX). By using the Avalonia UI framework, we're bringing the classic Pokémon save editor to macOS and Linux with a native look and feel.

### ⚠️ Project Status: Early Development
This is currently in a pre-alpha state. Most features are still being moved over from the original Windows version. Expect bugs and missing functionality.

---

## Features
We're working on full support for everything the original PKHeX offers:
* **Save Editing:** Core series save files (.sav, .dsv, .dat, .gci, .bin).
* **Entity Files:** Import and export .pk\*, .ck3, .xk3, .pb7, and more.
* **Mystery Gifts:** Support for .pgt, .pcd, .pgf, and .wc\* files.
* **Transferring:** Move Pokémon between generations while converting formats automatically.

## Requirements
To build and run this, you'll need:
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting Started
You can run the project directly from your terminal. Navigate to the root folder and run:

```bash
dotnet run --project PKHeX.Avalonia/PKHeX.Avalonia.csproj
```

## Screenshots
*Work in progress — the UI is changing fast.*

## Credits
This fork is built on the incredible work of the [PKHeX team](https://github.com/kwsch/PKHeX).

* **Logic & Research:** [PKHeX](https://github.com/kwsch/PKHeX)
* **QR Codes:** [QRCoder](https://github.com/codebude/QRCoder) (MIT)
* **Sprites:** [pokesprite](https://github.com/msikma/pokesprite) (MIT)
* **Arceus Sprites:** National Pokédex - Icon Dex project and contributors.
