# UI Density Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make PKHeX-Avalonia's default UI more compact on laptop displays, focused on the shared theme, main window, and Pokémon editor.

**Architecture:** Keep density behavior declarative in `Theme.axaml`; no settings, service, or persistence model is introduced. A static Avalonia test owns the selected XAML resource/layout contract so future visual changes do not silently restore the oversized defaults.

**Tech Stack:** Avalonia 11 XAML, .NET 10, xUnit.

## Global Constraints

- Do not modify `PKHeX.Core` or vendored `PKHeX.AutoMod` source.
- No density preference or localization changes; system display scaling remains authoritative.
- Preserve `AutomationProperties.Name` and existing minimum interactive control sizing.
- Bump `UIVersion` from `1.41.6` to `1.41.7`.

---

### Task 1: Lock the density contract with a failing test

**Files:**
- Create: `Tests/PKHeX.Avalonia.Tests/UiDensityTests.cs`

**Interfaces:**
- Consumes: source files under the repository root.
- Produces: an xUnit static-source regression test for the compact XAML values.

- [ ] **Step 1: Write the failing test**

Create `UiDensityTests` with three `[Fact]` methods. Resolve the repository root from `AppDomain.CurrentDomain.BaseDirectory`, read each XAML source file, and use `Assert.Contains` to require these compact values:

```csharp
Assert.Contains("<Setter Property=\"Padding\" Value=\"12,8\" />", theme);
Assert.Contains("<Setter Property=\"Padding\" Value=\"8\" />", theme);
Assert.Contains("ColumnDefinitions=\"520,*\"", mainWindow);
Assert.Contains("<StackPanel Spacing=\"12\" Margin=\"6,6,6,64\">", pokemonEditor);
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release --filter FullyQualifiedName~UiDensityTests`

Expected: FAIL because the current theme, main window, and Pokémon editor still contain the larger values.

- [ ] **Step 3: Commit the failing-test checkpoint**

```bash
git add Tests/PKHeX.Avalonia.Tests/UiDensityTests.cs
git commit -m "test: specify compact UI density defaults"
```

### Task 2: Compact shared theme defaults

**Files:**
- Modify: `PKHeX.Avalonia/Styles/Theme.axaml`

**Interfaces:**
- Consumes: `UiDensityTests` source assertions.
- Produces: reduced shared padding and compact form/data-grid defaults.

- [ ] **Step 1: Apply the minimal theme changes**

Update the shared style values only:

```xml
<Style Selector="Border.header-accent">
    <Setter Property="Padding" Value="12,8" />
</Style>
<Style Selector="Border.view-container">
    <Setter Property="Padding" Value="8" />
</Style>
<Style Selector="NumericUpDown.form-field">
    <Setter Property="MinHeight" Value="32" />
    <Setter Property="Padding" Value="6,3" />
</Style>
<Style Selector="DataGridRow">
    <Setter Property="MinHeight" Value="36" />
</Style>
<Style Selector="DataGridCell">
    <Setter Property="Padding" Value="6,3" />
</Style>
```

Also reduce `Border.card` and `Border.card-elevated` padding from `6` to `4`, `Border.section-card` from `8` to `6`, `Button.accent-gradient` padding from `20,10` to `16,8`, and badge padding from `8,4` to `6,3`.

- [ ] **Step 2: Run the density test to verify it remains red for targeted views**

Run: `dotnet test Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release --filter FullyQualifiedName~UiDensityTests`

Expected: FAIL only on the main-window and Pokémon-editor assertions.

### Task 3: Tighten the primary editor surfaces and release version

**Files:**
- Modify: `PKHeX.Avalonia/Views/MainWindow.axaml`
- Modify: `PKHeX.Avalonia/Views/PokemonEditor.axaml`
- Modify: `Directory.Build.props`

**Interfaces:**
- Consumes: compact shared theme styles from Task 2.
- Produces: the visible daily-use layout contract tested by `UiDensityTests` and an updated UI version.

- [ ] **Step 1: Apply the targeted layout changes**

Make the main Pokémon-editor column `520` pixels wide and reduce the status-bar padding to `8,4`. In `PokemonEditor.axaml`, use `Spacing="12" Margin="6,6,6,64"` for each tab's top-level content stack, reduce repeated section-card `Padding="12"` to `Padding="8"`, and reduce field-group top margins from `0,12,0,0` to `0,8,0,0`. Change `UIVersion` to `1.41.7`.

- [ ] **Step 2: Run the density test to verify it passes**

Run: `dotnet test Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release --filter FullyQualifiedName~UiDensityTests`

Expected: PASS, 3 tests with 0 failures.

- [ ] **Step 3: Run relevant guardrails and full verification**

Run:

```bash
dotnet test Tests/PKHeX.Avalonia.Tests/PKHeX.Avalonia.Tests.csproj -c Release
dotnet build PKHeX.sln -c Release
dotnet test PKHeX.sln -c Release
```

Expected: every command succeeds with no new warnings; accessibility and localization audits remain green.

- [ ] **Step 4: Commit the implementation**

```bash
git add PKHeX.Avalonia/Styles/Theme.axaml PKHeX.Avalonia/Views/MainWindow.axaml PKHeX.Avalonia/Views/PokemonEditor.axaml Directory.Build.props Tests/PKHeX.Avalonia.Tests/UiDensityTests.cs
git commit -m "fix: compact default UI density"
```
