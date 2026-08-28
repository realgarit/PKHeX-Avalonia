using System;
using System.Text.Json;
using Moq;
using PKHeX.Application.Abstractions;
using PKHeX.Application.Services;
using PKHeX.Avalonia;
using PKHeX.Core;
using PKHeX.Presentation.ViewModels;

namespace PKHeX.Avalonia.Tests;

/// <summary>
/// Covers issue #271: <see cref="AppSettings.Converter"/> was persisted but never applied to
/// <see cref="EntityConverter"/>'s statics, so preferences like <c>AllowIncompatibleConversion</c>
/// never actually took effect - including on the <see cref="EntityConverter.TryMakePKMCompatible"/>
/// path that <c>PokemonEditorViewModel.LoadPKM</c> added for issue #234.
/// <para>
/// <see cref="EntityConverter"/>'s settings are process-wide mutable statics, so every test here
/// captures the pre-test values in the constructor and restores them in <see cref="Dispose"/>. That
/// protects <em>sequential</em> test ordering within this class, but on its own would do nothing for
/// a test in another class mutating the same statics <em>concurrently</em>. That risk is already
/// closed at the assembly level: <c>xunit.runner.json</c> sets
/// <c>"parallelizeTestCollections": false</c> for this whole project, so no two test classes here
/// ever run at the same time (the same convention <see cref="HaXModeTests"/> already relies on for
/// its own <c>GameInfo.FilteredSources</c> static mutation). No additional <c>[Collection]</c>
/// grouping is needed on top of that existing, assembly-wide setting.
/// </para>
/// </summary>
public sealed class EntityConverterSettingsTests : IDisposable
{
    private readonly EntityCompatibilitySetting _originalAllowIncompatibleConversion = EntityConverter.AllowIncompatibleConversion;
    private readonly EntityRejuvenationSetting _originalRejuvenateHOME = EntityConverter.RejuvenateHOME;
    private readonly GameVersion _originalVirtualConsoleSourceGen1 = EntityConverter.VirtualConsoleSourceGen1;
    private readonly GameVersion _originalVirtualConsoleSourceGen2 = EntityConverter.VirtualConsoleSourceGen2;
    private readonly bool _originalRetainMetDateTransfer45 = EntityConverter.RetainMetDateTransfer45;

    public void Dispose()
    {
        EntityConverter.AllowIncompatibleConversion = _originalAllowIncompatibleConversion;
        EntityConverter.RejuvenateHOME = _originalRejuvenateHOME;
        EntityConverter.VirtualConsoleSourceGen1 = _originalVirtualConsoleSourceGen1;
        EntityConverter.VirtualConsoleSourceGen2 = _originalVirtualConsoleSourceGen2;
        EntityConverter.RetainMetDateTransfer45 = _originalRetainMetDateTransfer45;
    }

    /// <summary>A full set of non-default values, one per <see cref="EntityConverterSettings"/> property.</summary>
    private static EntityConverterSettings NonDefaultConverterSettings() => new()
    {
        AllowIncompatibleConversion = EntityCompatibilitySetting.AllowIncompatibleAll,
        AllowGuessRejuvenateHOME = EntityRejuvenationSetting.None,
        VirtualConsoleSourceGen1 = GameVersion.YW,
        VirtualConsoleSourceGen2 = GameVersion.GD,
        RetainMetDateTransfer45 = true,
    };

    private static void AssertCoreStaticsMatchNonDefault()
    {
        Assert.Equal(EntityCompatibilitySetting.AllowIncompatibleAll, EntityConverter.AllowIncompatibleConversion);
        Assert.Equal(EntityRejuvenationSetting.None, EntityConverter.RejuvenateHOME);
        Assert.Equal(GameVersion.YW, EntityConverter.VirtualConsoleSourceGen1);
        Assert.Equal(GameVersion.GD, EntityConverter.VirtualConsoleSourceGen2);
        Assert.True(EntityConverter.RetainMetDateTransfer45);
    }

    [Fact]
    public void InitializeCore_AppliesNonDefaultConverterSettings_ToCoreStatics()
    {
        var settings = new AppSettings { Converter = NonDefaultConverterSettings() };

        settings.InitializeCore();

        AssertCoreStaticsMatchNonDefault();
    }

    [Fact]
    public void InitializeCore_WithDefaultSettings_ResetsCoreStaticsToDefaults()
    {
        // Dirty the statics first, as if a previous load (or a live Settings-screen save) already
        // applied a non-default Converter group.
        new AppSettings { Converter = NonDefaultConverterSettings() }.InitializeCore();
        AssertCoreStaticsMatchNonDefault();

        new AppSettings().InitializeCore();

        Assert.Equal(EntityCompatibilitySetting.DisallowIncompatible, EntityConverter.AllowIncompatibleConversion);
        Assert.Equal(EntityRejuvenationSetting.MissingDataHOME, EntityConverter.RejuvenateHOME);
        Assert.Equal(GameVersion.RD, EntityConverter.VirtualConsoleSourceGen1);
        Assert.Equal(GameVersion.SI, EntityConverter.VirtualConsoleSourceGen2);
        Assert.False(EntityConverter.RetainMetDateTransfer45);
    }

    [Fact]
    public void BuildServiceProvider_AppliesPersistedConverterSettings_AtStartup()
    {
        // The real composition root: a settings store whose persisted file already holds a
        // non-default Converter group, exactly like a returning user's config.json.
        var store = new FakeSettingsStore { ToLoad = new AppSettings { Converter = NonDefaultConverterSettings() } };

        // App.axaml.cs's ConfigureServices calls config.InitializeCore() synchronously while
        // building the container, before anything is resolved from it, so the effect is observable
        // without pulling any service out of the provider.
        var provider = App.BuildServiceProvider(paths: new FakeAppPaths(), settingsStore: store);
        try
        {
            AssertCoreStaticsMatchNonDefault();
        }
        finally
        {
            // Same best-effort teardown HeadlessAppFixture uses for its own BuildServiceProvider
            // result: release the singletons instead of letting a production object graph leak.
            if (provider is IDisposable disposable)
                disposable.Dispose();
        }
    }

    [Fact]
    public void SettingsViewModel_Save_ReappliesConverterSettingsToCoreStatics()
    {
        var settings = new AppSettings { Converter = NonDefaultConverterSettings() };
        var vm = new SettingsViewModel(
            settings,
            new FakeSettingsStore(),
            new Mock<IThemeService>().Object,
            new LanguageService(),
            UpdateTestDoubles.Coordinator());

        // The Settings screen doesn't (yet) bind Converter to any editable control, so Save() just
        // re-applies whatever is already sitting on settings.Converter - the same as startup does.
        vm.SaveCommand.Execute(null);

        AssertCoreStaticsMatchNonDefault();
    }

    [Fact]
    public void Converter_JsonRoundTrip_PreservesNonDefaultValues()
    {
        var settings = new AppSettings { Converter = NonDefaultConverterSettings() };

        var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings));

        Assert.NotNull(restored);
        Assert.Equal(EntityCompatibilitySetting.AllowIncompatibleAll, restored!.Converter.AllowIncompatibleConversion);
        Assert.Equal(EntityRejuvenationSetting.None, restored.Converter.AllowGuessRejuvenateHOME);
        Assert.Equal(GameVersion.YW, restored.Converter.VirtualConsoleSourceGen1);
        Assert.Equal(GameVersion.GD, restored.Converter.VirtualConsoleSourceGen2);
        Assert.True(restored.Converter.RetainMetDateTransfer45);
    }

    [Fact]
    public void AllowIncompatibleConversion_GatesTryMakePKMCompatible_ForBackwardsPK7ToPK6()
    {
        // PK7 (format 7) -> PK6 (format 6) is a backwards conversion with no official transfer
        // route (EntityConverter.IsConvertibleToFormat rejects any format 3+ -> lower-than-8 hop),
        // so it is exactly the kind of conversion PokemonEditorViewModel.LoadPKM relies on this
        // setting to gate on the editor's "load a foreign-format entity" path added for issue #234.
        var pk7 = new PK7 { Species = (ushort)Species.Bulbasaur };

        new AppSettings { Converter = { AllowIncompatibleConversion = EntityCompatibilitySetting.DisallowIncompatible } }.InitializeCore();

        var blocked = EntityConverter.TryMakePKMCompatible(pk7, new PK6(), out var blockedResult, out var blockedEntity);

        Assert.False(blocked);
        Assert.Equal(EntityConverterResult.NoTransferRoute, blockedResult);
        Assert.IsType<PK6>(blockedEntity); // the untouched target handed back as-is

        new AppSettings { Converter = { AllowIncompatibleConversion = EntityCompatibilitySetting.AllowIncompatibleAll } }.InitializeCore();

        var allowed = EntityConverter.TryMakePKMCompatible(pk7, new PK6(), out var allowedResult, out var allowedEntity);

        Assert.True(allowed);
        Assert.Equal(EntityConverterResult.SuccessIncompatibleReflection, allowedResult);
        Assert.IsType<PK6>(allowedEntity);
        Assert.Equal((ushort)Species.Bulbasaur, allowedEntity.Species);
    }
}
