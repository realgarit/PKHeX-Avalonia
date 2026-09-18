# Runnable visual reference (not production code)

This is the exact compact C#-built Avalonia prototype the user approved, with only artwork paths made checkout-relative. It is intentionally outside PKHeX.sln. Do not reference this project from any production project, import its sample arrays, or copy its hand-built UI architecture into the application.

From repository root:

```powershell
dotnet run --project docs/ui/compact-redesign/reference-prototype/Atelier.csproj -c Release
```

The main client area is 900×600; Preferences opens shortly after launch. Top-right switches Light/Dark. Main, Stats, Moves, Met and OT select simplified sample panes. Dropdowns are real native controls but do not edit a PKM. Theme is live but not persisted; other preferences are illustrative; Save/Apply do not write data.

To regenerate client-area reference captures:

```powershell
dotnet run --project docs/ui/compact-redesign/reference-prototype/Atelier.csproj -c Release -- --capture
```

Captures are written next to the executable under bin/Release/net10.0. This capture mode intentionally cycles themes at startup. Normal launch does not. Committed reference/ PNGs are the previously reviewed images, not a claim that the future production implementation has passed.

Uses the repository's existing 24 sprite files via csproj Content links; no machine-specific paths or network artwork. Fonts/packages match Avalonia 11.3.18. Prototype limitations include simplified selection semantics, some tiny text/contrast needing production correction, hardcoded English/sample stats, and placeholder preference controls. These are deliberately documented rather than silently elevated into requirements. Production must follow DESIGN.md and preserve real bindings/services.
