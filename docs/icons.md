# Icon system

PKHeX-Avalonia draws its action and state icons from one shared vector set,
`PKHeX.Avalonia/Styles/Icons.axaml`. Views use `PathIcon` with a `StaticResource Icon.*` key. Do
not use emoji or raw arrow and shape characters (for example ◀ ▶ ▲ ▼ → ✕ ★ 🔍) in views: their look
depends on the platform font, and a colour emoji does not follow the app theme.
`Tests/PKHeX.Avalonia.Tests/IconSystemTests.cs` fails on such characters in `.axaml` files.

## Provenance and licence

Every geometry in the set is original work drawn for PKHeX-Avalonia from basic shapes (lines,
arcs and polygons) on a 16 x 16 grid. No icon is copied, traced or derived from upstream PKHeX
artwork, an icon font or a third-party icon library. The icons are part of this repository and use
its licence. Pokémon sprites and item artwork are separate assets with their own provenance in
`PKHeX.Avalonia/Assets/ThirdParty/`.

## Grid and states

- 16 x 16 DIP design box with a 1 DIP safe margin. Bars are 1.5 to 2 DIP wide so every icon has the
  same optical weight. Geometry is resolution independent, so it stays sharp at 100, 125, 150 and
  200 % display scaling (`Tests/PKHeX.Avalonia.Render.Tests/IconRenderTests.cs`).
- Colour is never part of the geometry. The central styles in `Icons.axaml` set the foreground for
  the normal, hover, pressed and disabled states from semantic theme tokens. The HighContrast,
  Light and Dark dictionaries in `Styles/Theme.axaml` supply those tokens, so high contrast needs no
  per-icon rule.
- An icon-only button uses the `icon-action` (or `icon-button`) class and always has
  `ToolTip.Tip` and `AutomationProperties.Name` from a localized `{loc:Loc ...}` key.

## Inventory

| Key | Meaning |
| --- | --- |
| `Icon.ChevronLeft` | Previous, collapse to the left |
| `Icon.ChevronRight` | Next, expand to the right |
| `Icon.ChevronUp` | Move up |
| `Icon.ChevronDown` | Move down |
| `Icon.ArrowRight` | Move into the right-hand list |
| `Icon.ArrowLeft` | Move into the left-hand list |
| `Icon.Close` | Close, dismiss, clear |
| `Icon.Check` | Confirmed, legal |
| `Icon.Search` | Search |
| `Icon.Reset` | Reset to default |
| `Icon.Filter` | Filters |
| `Icon.Folder` | File or folder |
| `Icon.Tool` | Advanced settings |
| `Icon.Star` | Shiny, star marking |
| `Icon.Heart` | Heart marking, favorite |
| `Icon.Circle` | Circle marking |
| `Icon.Triangle` | Triangle marking |
| `Icon.Square` | Square marking |
| `Icon.Diamond` | Diamond marking |
| `Icon.Pouch.Items` | Items pouch (also the fallback pouch) |
| `Icon.Pouch.KeyItems` | Key Items pouch |
| `Icon.Pouch.TMHMs` | TMs and HMs pouch |
| `Icon.Pouch.Medicine` | Medicine pouch |
| `Icon.Pouch.Berries` | Berries pouch |
| `Icon.Pouch.Balls` | Poké Balls pouch |
| `Icon.Pouch.BattleItems` | Battle Items pouch |
| `Icon.Pouch.MailItems` | Mail pouch |
| `Icon.Pouch.PCItems` | PC Items |
| `Icon.Pouch.FreeSpace` | Free Space pouch |
| `Icon.Pouch.Gem` | Mega Stones, Z-Crystals and Treasures pouches |
| `Icon.Pouch.Candy` | Candies pouch |
| `Icon.Pouch.Ingredients` | Ingredients pouch |

To add an icon, draw it on the grid, add it to `Icons.axaml`, this table and the inventory list in
`IconSystemTests`.
