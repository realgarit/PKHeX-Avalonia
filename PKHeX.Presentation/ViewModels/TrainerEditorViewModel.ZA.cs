using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.ViewModels;

public partial class TrainerEditorViewModel
{
    private readonly ZaTrainerDataSession? _zaSession;
    private readonly IDialogService? _zaDialogs;
    private readonly IImageCodec? _zaImageCodec;
    private SAV9ZA? _zaBaseline;
    private bool _zaHasMoney;
    private uint _zaOriginalMoney;
    private int _zaEpoch;
    private bool _zaClosed;
    private bool _zaHadValidLastSaved;
    public bool IsZA => _zaSession is not null;
    public bool CanUndoZaCollection => !_zaClosed && _zaSession?.CanUndo == true;
    public ObservableCollection<ZaTrainerImageViewModel> ZaImages { get; } = [];
    [ObservableProperty] private string _zaMap = string.Empty;
    [ObservableProperty] private double _zaRotation;
    [ObservableProperty] private DateTimeOffset? _zaLastSavedDate;
    [ObservableProperty] private TimeSpan? _zaLastSavedTime;
    [ObservableProperty] private int _zaLastSavedSecond;
    public bool HasZaLastSavedSeconds => _sav is SAV9ZA za && za.LastSaved.HasSeconds;
    [ObservableProperty] private string _zaError = string.Empty;
    public bool HasZaError => ZaError.Length != 0;
    public bool IsZaInputValid =>
        ZaMap.Length <= 32 && ZaMap.All(character => character is >= ' ' and <= '~') &&
        StreetName.Length <= 18 &&
        ValidCoordinate(X, _zaBaseline?.Coordinates.X) && ValidCoordinate(Y, _zaBaseline?.Coordinates.Y) && ValidCoordinate(Z, _zaBaseline?.Coordinates.Z) &&
        (ZaRotation.Equals(_zaBaseline?.Coordinates.Rotation) || double.IsFinite(ZaRotation)) &&
        (ZaLastSavedDate is null || ZaLastSavedDate.Value.Year >= 1900) &&
        (ZaLastSavedDate.HasValue == ZaLastSavedTime.HasValue) && (!_zaHadValidLastSaved || ZaLastSavedDate.HasValue) &&
        (ZaLastSavedTime is null || ZaLastSavedTime.Value >= TimeSpan.Zero && ZaLastSavedTime.Value < TimeSpan.FromDays(1)) &&
        ZaLastSavedSecond is >= 0 and <= 59;

    private static bool ValidCoordinate(double value, float? original) =>
        (original is { } stored && value.Equals((double)stored)) || (double.IsFinite(value) && Math.Abs(value) <= float.MaxValue);

    private void LoadZaFields()
    {
        if (_sav is not SAV9ZA za) return;
        _zaBaseline = (SAV9ZA)za.Clone();
        _zaHasMoney = false;
        try { _zaOriginalMoney = za.Money; _zaHasMoney = true; }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        HasCoordinates = true; X = za.Coordinates.X; Y = za.Coordinates.Y; Z = za.Coordinates.Z;
        ZaMap = za.Coordinates.Map; ZaRotation = za.Coordinates.Rotation;
        ZaLastSavedDate = null; ZaLastSavedTime = null;
        ZaLastSavedSecond = 0;
        _zaHadValidLastSaved = false;
        try { var saved = za.LastSaved.Timestamp; ZaLastSavedDate = new DateTimeOffset(saved); ZaLastSavedTime = saved.TimeOfDay; ZaLastSavedSecond = saved.Second; _zaHadValidLastSaved = true; }
        catch (ArgumentOutOfRangeException) { }
        HasHyperspacePoints = false;
        try
        {
            if (za.Blocks.TryGetBlock(SaveBlockAccessor9ZA.KHyperspaceSurveyPoints, out var survey) && survey.Type == SCTypeCode.UInt32)
            { HyperspacePoints = za.Blocks.GetBlockValue<uint>(survey.Key); HasHyperspacePoints = true; }
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        AnyCurrencyVisible |= HasHyperspacePoints;
        ZaError = string.Empty;
        LoadZaImages(za);
        OnPropertyChanged(nameof(CanUndoZaCollection)); SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnZaErrorChanged(string value) => OnPropertyChanged(nameof(HasZaError));
    partial void OnZaMapChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnZaRotationChanged(double value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnZaLastSavedDateChanged(DateTimeOffset? value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnZaLastSavedTimeChanged(TimeSpan? value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnZaLastSavedSecondChanged(int value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnStreetNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnXChanged(double value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnYChanged(double value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnZChanged(double value) => SaveCommand.NotifyCanExecuteChanged();

    private void SaveZaFields()
    {
        if (_sav is not SAV9ZA || _zaBaseline is not { } baseline || _zaSession is null || !CanSave()) return;
        bool committed = _zaSession.TryCommit(za =>
        {
            if (TrainerName != baseline.OT) za.OT = TrainerName;
            if (Gender != baseline.Gender) za.Gender = (byte)Gender;
            if (DisplayTid != baseline.DisplayTID || DisplaySid != baseline.DisplaySID) za.SetDisplayID(DisplayTid, DisplaySid);
            if (Language != baseline.Language) za.Language = Language;
            if (PlayedHours != baseline.PlayedHours) za.PlayedHours = PlayedHours;
            if (PlayedMinutes != baseline.PlayedMinutes) za.PlayedMinutes = PlayedMinutes;
            if (PlayedSeconds != baseline.PlayedSeconds) za.PlayedSeconds = PlayedSeconds;
            if (_zaHasMoney && Money != _zaOriginalMoney) za.Money = Money;
            if (HasRoyalePoints && RoyalePoints != baseline.TicketPointsRoyale) za.TicketPointsRoyale = RoyalePoints;
            if (HasRoyalePointsInfinite && RoyalePointsInfinite != baseline.TicketPointsRoyaleInfinite) za.TicketPointsRoyaleInfinite = RoyalePointsInfinite;
            if (HasHyperspacePoints && HyperspacePoints != baseline.GetValue<uint>(SaveBlockAccessor9ZA.KHyperspaceSurveyPoints))
                za.SetValue(SaveBlockAccessor9ZA.KHyperspaceSurveyPoints, HyperspacePoints);
            if (HasStreetName && StreetName != baseline.GetString(baseline.Blocks.GetBlock(SaveBlockAccessor9ZA.KStreetName).Data))
                za.SetString(za.Blocks.GetBlock(SaveBlockAccessor9ZA.KStreetName).Data, StreetName, 18, StringConverterOption.ClearZero);
            if (ZaMap != baseline.Coordinates.Map) za.Coordinates.Map = ZaMap;
            if (!X.Equals((double)baseline.Coordinates.X)) za.Coordinates.X = (float)X;
            if (!Y.Equals((double)baseline.Coordinates.Y)) za.Coordinates.Y = (float)Y;
            if (!Z.Equals((double)baseline.Coordinates.Z)) za.Coordinates.Z = (float)Z;
            if (Math.Abs(ZaRotation - baseline.Coordinates.Rotation) > 0.00000001)
            {
                // Core's double overload puts sine in RY, but Rotation reads RZ.
                // Use the public quaternion overload with the documented yaw axis.
                double angle = ZaRotation * Math.PI / 360;
                za.Coordinates.SetPlayerRotation(0, 0, (float)Math.Sin(angle), (float)Math.Cos(angle));
            }
            if (ZaLastSavedDate is { } date && ZaLastSavedTime is { } time)
            {
                var desired = date.Date + new TimeSpan(time.Hours, time.Minutes, HasZaLastSavedSeconds ? ZaLastSavedSecond : 0);
                DateTime? original = null;
                try { original = baseline.LastSaved.Timestamp; } catch (ArgumentOutOfRangeException) { }
                if (desired != original) za.LastSaved.Timestamp = desired;
            }
        });
        if (!committed) { _sav = _zaSession.Staged; ZaError = LocalizedStrings.Instance["TrainerZA_Conflict"]; return; }
        _sav = _zaSession.Staged; LoadFromSave();
    }

    [RelayCommand]
    private async Task CollectZaAsync(string kind)
    {
        if (_zaClosed || _zaSession is null || _zaDialogs is null || kind is not ("Screws" or "TMs")) return;
        int epoch = _zaEpoch;
        if (!await _zaDialogs.ShowConfirmationAsync(LocalizedStrings.Instance["TrainerZA_Collect" + kind],
                LocalizedStrings.Instance["TrainerZA_Confirm" + kind], LocalizedStrings.Instance["TrainerZA_Collect"], LocalizedStrings.Instance["Common_Cancel"])) return;
        if (_zaClosed || epoch != _zaEpoch) return;
        try { _zaSession.ApplyCollection(kind == "TMs"); ZaError = string.Empty; }
        catch (ArgumentException) { ZaError = LocalizedStrings.Instance["TrainerZA_CollectionFailed"]; }
        catch (InvalidOperationException) { ZaError = LocalizedStrings.Instance["TrainerZA_CollectionFailed"]; }
        _sav = _zaSession.Staged;
        OnPropertyChanged(nameof(CanUndoZaCollection));
    }

    [RelayCommand]
    private void UndoZaCollection()
    {
        if (_zaClosed || _zaSession is null) return;
        _zaSession.Undo(); _sav = _zaSession.Staged;
        LoadZaImages((SAV9ZA)_sav);
        _zaEpoch++;
        OnPropertyChanged(nameof(CanUndoZaCollection));
    }

    [RelayCommand]
    private async Task ImportZaImageAsync(ZaTrainerImageViewModel? image)
    {
        if (_zaClosed || _zaSession is null || _zaDialogs is null || _zaImageCodec is null || image?.CanImport is not true) return;
        int epoch = _zaEpoch;
        try
        {
            var path = await _zaDialogs.OpenFileAsync(LocalizedStrings.Instance["TrainerZA_ImportImage"], ["png"]);
            if (path is null || _zaClosed || epoch != _zaEpoch) return;
            // Bound the read; the codec additionally checks actual decoded dimensions.
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            long length = input.Length;
            if (length > 32 * 1024 * 1024) throw new ArgumentException();
            var bytes = new byte[checked((int)length)];
            await input.ReadExactlyAsync(bytes);
            if (_zaClosed || epoch != _zaEpoch) return;
            var prepared = await Task.Run(() => PrepareZaImage(bytes, image));
            if (_zaClosed || epoch != _zaEpoch) return;
            if (!prepared.Valid)
            { ZaError = LocalizedStrings.Instance.Format("TrainerZA_ImageImportFailed", image.Width, image.Height); return; }
            if (prepared.Encoded is null) { ZaError = string.Empty; return; }
            _zaSession.ImportCompressedImage(image.DataKey, image.WidthKey, image.HeightKey, image.Width, image.Height, prepared.Encoded);
            _sav = _zaSession.Staged; _zaEpoch++;
            int index = ZaImages.ToList().FindIndex(item => item.DataKey == image.DataKey);
            if (index >= 0) ZaImages[index] = ZaImages[index] with { Png = prepared.Png };
            ZaError = string.Empty;
            OnPropertyChanged(nameof(CanUndoZaCollection));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { if (!_zaClosed && epoch == _zaEpoch) ZaError = LocalizedStrings.Instance.Format("TrainerZA_ImageImportFailed", image.Width, image.Height); }
    }

    private (bool Valid, byte[]? Encoded, byte[]? Png) PrepareZaImage(byte[] bytes, ZaTrainerImageViewModel image)
    {
        var decoded = _zaImageCodec!.DecodePng(bytes, image.Width, image.Height);
        if (decoded.Image is null || decoded.Error != ImageDecodeError.None) return (false, null, null);
        if (image.Png is { } original && _zaImageCodec.DecodePng(original, image.Width, image.Height).Image is { } pixels &&
            pixels.Bgra.AsSpan().SequenceEqual(decoded.Image.Bgra)) return (true, null, null);
        var encoded = Dxt1ImageEncoder.Encode(decoded.Image);
        var preview = _zaImageCodec.EncodePng(new PixelImage(image.Width, image.Height, DXT1.Decompress(encoded, image.Width, image.Height)));
        return (true, encoded, preview);
    }

    public void Dispose()
    {
        BdspEditor?.Dispose();
        PlaEditor?.Dispose();
        LgpeEditor?.Dispose();
        Gen7Editor?.Dispose();
        SwshEditor?.Dispose();
        DisposeBattleBox();
        _zaClosed = true; _zaEpoch++;
        OnPropertyChanged(nameof(CanUndoZaCollection));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task ExportZaImageAsync(ZaTrainerImageViewModel? image)
    {
        if (image?.Png is not { } png || _zaDialogs is null) return;
        var path = await _zaDialogs.SaveFileAsync(LocalizedStrings.Instance["TrainerZA_ExportImage"], "trainer.png", ["png"]);
        if (path is null) return;
        try { await File.WriteAllBytesAsync(path, png); }
        catch (IOException) { ZaError = LocalizedStrings.Instance["TrainerZA_ImageExportFailed"]; }
        catch (UnauthorizedAccessException) { ZaError = LocalizedStrings.Instance["TrainerZA_ImageExportFailed"]; }
    }

    private void LoadZaImages(SAV9ZA za)
    {
        ZaImages.Clear();
        foreach (var (name, data, width, height) in new[]
        {
            ("TrainerZA_ImageCurrent", SaveBlockAccessor9ZA.KPictureCurrentData, SaveBlockAccessor9ZA.KPictureCurrentWidth, SaveBlockAccessor9ZA.KPictureCurrentHeight),
            ("TrainerZA_ImagePortrait", SaveBlockAccessor9ZA.KPictureSBCData, SaveBlockAccessor9ZA.KPictureSBCWidth, SaveBlockAccessor9ZA.KPictureSBCHeight),
            ("TrainerZA_ImageInitial", SaveBlockAccessor9ZA.KPictureInitialData, SaveBlockAccessor9ZA.KPictureInitialWidth, SaveBlockAccessor9ZA.KPictureInitialHeight),
        })
        {
            byte[]? png = null;
            int imageWidth = 0, imageHeight = 0;
            try
            {
                if (_zaImageCodec is not null &&
                    za.Blocks.TryGetBlock(width, out var widthBlock) && widthBlock.Type == SCTypeCode.UInt32 &&
                    za.Blocks.TryGetBlock(height, out var heightBlock) && heightBlock.Type == SCTypeCode.UInt32 &&
                    za.Blocks.TryGetBlock(data, out var imageBlock))
                {
                    uint w = (uint)widthBlock.GetValue(), h = (uint)heightBlock.GetValue();
                    var bytes = imageBlock.Data;
                    if (imageBlock.Type == SCTypeCode.Object && w is > 0 and <= 2048 && h is > 0 and <= 2048 && w % 4 == 0 && h % 4 == 0 &&
                        bytes.Length >= (w / 4) * (h / 4) * 8)
                    {
                        imageWidth = (int)w; imageHeight = (int)h;
                        png = _zaImageCodec.EncodePng(new PixelImage((int)w, (int)h, DXT1.Decompress(bytes, (int)w, (int)h)));
                    }
                }
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            ZaImages.Add(new(LocalizedStrings.Instance[name], png, data, width, height, imageWidth, imageHeight));
        }
    }
}

public sealed record ZaTrainerImageViewModel(string Name, byte[]? Png, uint DataKey = 0, uint WidthKey = 0, uint HeightKey = 0, int Width = 0, int Height = 0)
{
    public bool HasImage => Png is not null;
    public bool CanImport => Width > 0 && Height > 0;
    public string ImportHelp => CanImport ? LocalizedStrings.Instance.Format("TrainerZA_ImageDimensions", Width, Height) : LocalizedStrings.Instance["TrainerZA_ImageUnavailable"];
}
