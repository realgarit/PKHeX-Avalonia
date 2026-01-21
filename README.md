# PKHeX Avalonia

![License](https://img.shields.io/badge/License-GPLv3-blue.svg)

PKHeX Avalonia is now the main development branch of the cross-platform [PKHeX](https://github.com/kwsch/PKHeX) port. By using the Avalonia UI framework, we bring the classic Pokémon save editor to macOS and Linux with a native look and feel.

### ⚠️ Project Status: Beta (Migration Complete)
All core features and editors have been migrated from the original WinForms version to Avalonia. However, **nothing is thoroughly tested**.

**Expect a lot of bugs.** We are currently in a stabilization phase.

---

## Project Structure
* **PKHeX.Avalonia**: The main application (cross-platform).
* **Legacy/PKHeX.WinForms**: The original Windows Forms application, kept as a reference archive.
* **PKHeX.Core**: Shared logic library.

## Features
* **Save Editing:** Core series save files (.sav, .dsv, .dat, .gci, .bin).
* **Entity Files:** Import and export .pk*, .ck3, .xk3, .pb7, and more.
* **Mystery Gifts:** Support for .pgt, .pcd, .pgf, and .wc* files.
* **Transferring:** Move Pokémon between generations while converting formats automatically.

## Requirements
To build and run this, you'll need:
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting Started
You can run the project directly from your terminal. Navigate to the root folder and run:

```bash
dotnet run --project PKHeX.Avalonia
```

## Screenshots
*Work in progress — the UI is changing fast.*
<img width="1566" height="934" alt="image" src="https://github.com/user-attachments/assets/29135e2b-a95f-42ce-81fa-2fad679b660e" />
<img width="1566" height="934" alt="image" src="https://github.com/user-attachments/assets/fd494c90-58ac-4ea1-b607-367128fc34be" />

## Credits
This fork is built on the incredible work of the [PKHeX team](https://github.com/kwsch/PKHeX).

* **Logic & Research:** [PKHeX](https://github.com/kwsch/PKHeX)
* **QR Codes:** [QRCoder](https://github.com/codebude/QRCoder) (MIT)
* **Sprites:** [pokesprite](https://github.com/msikma/pokesprite) (MIT)
* **Arceus Sprites:** National Pokédex - Icon Dex project and contributors.
