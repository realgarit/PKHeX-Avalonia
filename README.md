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
<img width="1212" height="790" alt="Screenshot 2026-01-21 at 20 46 16" src="https://github.com/user-attachments/assets/430b2ca2-a011-4d8d-aaa6-f07287e30d6c" />
<img width="1212" height="790" alt="Screenshot 2026-01-21 at 20 46 36" src="https://github.com/user-attachments/assets/1d2d3950-ac98-46bd-853b-c51c1e2e74c3" />
<img width="1212" height="790" alt="Screenshot 2026-01-21 at 20 46 48" src="https://github.com/user-attachments/assets/40d58fc3-86c7-4d3b-bccd-b6c82fecd14a" />
<img width="1212" height="790" alt="Screenshot 2026-01-21 at 20 47 06" src="https://github.com/user-attachments/assets/8d0a1b76-ded5-4119-a079-33a2b08ebf7c" />
<img width="1100" height="677" alt="Screenshot 2026-01-21 at 20 47 32" src="https://github.com/user-attachments/assets/0b9a811c-5fb5-44cc-9f06-5a4dadf6e043" />

## Credits
This fork is built on the incredible work of the [PKHeX team](https://github.com/kwsch/PKHeX).

* **Logic & Research:** [PKHeX](https://github.com/kwsch/PKHeX)
* **QR Codes:** [QRCoder](https://github.com/codebude/QRCoder) (MIT)
* **Sprites:** [pokesprite](https://github.com/msikma/pokesprite) (MIT)
* **Arceus Sprites:** National Pokédex - Icon Dex project and contributors.
