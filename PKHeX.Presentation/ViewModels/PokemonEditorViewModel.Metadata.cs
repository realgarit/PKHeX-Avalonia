using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class PokemonEditorViewModel
{
    public bool HasPlusRecords => _pk is IPlusRecord && _pk.PersonalInfo is IPermitPlus;

    [RelayCommand]
    private async Task OpenPlusRecordsAsync()
    {
        if (!HasPlusRecords) return;
        PreparePKM();
        await _windowService.ShowDialogAsync(new PlusRecordEditorViewModel(_pk), LocalizedStrings.Instance["PlusRecords_Title"]);
        LoadFromPKM();
    }

    public bool HasAlpha => _pk is IAlpha;
    public bool HasSizeScalars => _pk is IScaledSize;
    public bool HasScale => _pk is IScaledSize3;
    public bool HasBattleVersion => _pk is IBattleVersion;
    public bool HasObedienceLevel => _pk is IObedienceLevel;
    public bool HasHandlerLanguage => _pk is IHandlerLanguage;
    public bool HasHomeTracker => _pk is IHomeTrack;

    [ObservableProperty] private bool _isAlpha;
    [ObservableProperty] private byte _heightScalar;
    [ObservableProperty] private byte _weightScalar;
    [ObservableProperty] private byte _scale;
    [ObservableProperty] private int _battleVersion;
    [ObservableProperty] private byte _obedienceLevel;
    [ObservableProperty] private int _handlingTrainerLanguage;
    [ObservableProperty] private string _homeTracker = "0000000000000000";

    public bool IsHomeTrackerValid => HomeTracker is { Length: 16 } &&
        ulong.TryParse(HomeTracker, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);

    // Include zero and preserve unknown stored values rather than silently normalizing metadata.
    public IReadOnlyList<ComboItem> BattleVersionList { get; private set; } = [];
    public IReadOnlyList<ComboItem> HandlerLanguageList { get; private set; } = [];

    private static IReadOnlyList<ComboItem> MetadataChoices(IEnumerable<ComboItem> choices, int stored)
    {
        var result = choices.ToList();
        if (result.All(z => z.Value != 0)) result.Insert(0, new ComboItem("—", 0));
        if (result.All(z => z.Value != stored)) result.Add(new ComboItem(stored.ToString(CultureInfo.InvariantCulture), stored));
        return result;
    }

    private void LoadMetadata()
    {
        var battleVersion = (int)((_pk as IBattleVersion)?.BattleVersion ?? 0);
        var handlerLanguage = (_pk as IHandlerLanguage)?.HandlingTrainerLanguage ?? 0;
        BattleVersionList = MetadataChoices(OriginGameList, battleVersion);
        HandlerLanguageList = MetadataChoices(LanguageList, handlerLanguage);
        OnPropertyChanged(nameof(BattleVersionList));
        OnPropertyChanged(nameof(HandlerLanguageList));
        IsAlpha = (_pk as IAlpha)?.IsAlpha ?? false;
        HeightScalar = (_pk as IScaledSize)?.HeightScalar ?? 0;
        WeightScalar = (_pk as IScaledSize)?.WeightScalar ?? 0;
        Scale = (_pk as IScaledSize3)?.Scale ?? 0;
        BattleVersion = battleVersion;
        ObedienceLevel = (_pk as IObedienceLevel)?.ObedienceLevel ?? 0;
        HandlingTrainerLanguage = handlerLanguage;
        HomeTracker = ((_pk as IHomeTrack)?.Tracker ?? 0).ToString("X16", CultureInfo.InvariantCulture);
        foreach (var name in new[] { nameof(HasPlusRecords), nameof(HasAlpha), nameof(HasSizeScalars), nameof(HasScale),
                     nameof(HasBattleVersion), nameof(HasObedienceLevel), nameof(HasHandlerLanguage),
                     nameof(HasHomeTracker), nameof(BattleVersion), nameof(HandlingTrainerLanguage) })
            OnPropertyChanged(name);
    }

    private void ApplyMetadata()
    {
        if (_pk is IAlpha alpha) alpha.IsAlpha = IsAlpha;
        if (_pk is IScaledSize size) { size.HeightScalar = HeightScalar; size.WeightScalar = WeightScalar; }
        if (_pk is IScaledSize3 scale) scale.Scale = Scale;
        if (_pk is IBattleVersion battle) battle.BattleVersion = (GameVersion)BattleVersion;
        if (_pk is IObedienceLevel obedience) obedience.ObedienceLevel = ObedienceLevel;
        if (_pk is IHandlerLanguage handler) handler.HandlingTrainerLanguage = (byte)HandlingTrainerLanguage;
        if (_pk is IHomeTrack home && IsHomeTrackerValid)
            home.Tracker = ulong.Parse(HomeTracker, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
    }

    partial void OnIsAlphaChanged(bool value) { if (!_isLoading) Validate(); }
    partial void OnHeightScalarChanged(byte value) { if (!_isLoading) Validate(); }
    partial void OnWeightScalarChanged(byte value) { if (!_isLoading) Validate(); }
    partial void OnScaleChanged(byte value) { if (!_isLoading) Validate(); }
    partial void OnBattleVersionChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnObedienceLevelChanged(byte value) { if (!_isLoading) Validate(); }
    partial void OnHandlingTrainerLanguageChanged(int value) { if (!_isLoading) Validate(); }
    partial void OnHomeTrackerChanged(string value)
    {
        OnPropertyChanged(nameof(IsHomeTrackerValid));
        if (!_isLoading) Validate();
    }
}
