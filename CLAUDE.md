# PKHeX Avalonia Migration - Project Rules

## Core Preservation (Non-Negotiable)

- **NEVER modify PKHeX.Core** - This is the canonical save file research library. All changes must be in the UI layer only.
- **NEVER modify PKHeX.Drawing** projects - These use System.Drawing/GDI+ which is Windows-only. Create Avalonia-native alternatives instead.
- **Wrap, don't fork** - If PKHeX.Core lacks functionality you need, create adapter/wrapper services in the Avalonia project rather than modifying Core.

## Architecture

- **MVVM with pragmatism** - Use CommunityToolkit.Mvvm for data binding. Code-behind is acceptable for complex UI-only logic (drag-drop, animations) that would be awkward in a ViewModel.
- **Dependency Injection** - Register services in App.axaml.cs. ViewModels must not instantiate services directly.
- **Interface abstractions** - ViewModels must not reference Views or Avalonia types directly. Use IDialogService, ISpriteRenderer, etc.
- **No singletons/statics for state** - Avoid patterns like `SpriteUtil.Spriter` in new code. Pass dependencies explicitly.

## Rendering

- **No System.Drawing** - Use SkiaSharp or Avalonia's DrawingContext for all graphics operations.
- **ISpriteRenderer abstraction** - All sprite generation goes through this interface, making it testable and swappable.
- **Lazy loading for sprites** - Don't load all 1000+ species sprites at startup. Load on-demand and cache.

## Compatibility

- **Target .NET 10 Preview** - Test cutting-edge compatibility, but keep fallback to .NET 9 possible.
- **Cross-platform first** - Test on macOS/Linux, not just Windows. Avoid platform-specific APIs.
- **Preserve save file compatibility** - Use PKHeX.Core's SaveUtil for all file operations. Never implement custom save parsing.

## UI/UX

- **Fluent theme** - Use Avalonia's FluentTheme. Don't force WinForms styling.
- **Keyboard accessible** - All interactive elements must be keyboard navigable.
- **Familiar layout** - Keep the general PKHeX layout users expect (box grid, party, tabs) but modernize the styling.

## File Organization

```
PKHeX.Avalonia/
├── Models/        # Data transfer objects, view models data
├── Services/      # Business logic, PKHeX.Core wrappers
├── ViewModels/    # MVVM view models
├── Views/         # AXAML files and code-behind
├── Controls/      # Reusable custom controls
└── Resources/     # Assets, styles, localization
```

## Testing

- **Unit test ViewModels** - They should be testable without Avalonia runtime.
- **Mock PKHeX.Core types** - Use interfaces where possible to enable testing.
- **Integration tests for save operations** - Verify round-trip save/load preserves data.