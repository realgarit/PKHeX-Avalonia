using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Avalonia.Services;
using PKHeX.Core;

namespace PKHeX.Avalonia.ViewModels;

public partial class MysteryGiftEditorViewModel : ViewModelBase
{
    private readonly SaveFile _sav;
    private readonly IMysteryGiftStorage? _storage;
    private readonly IDialogService _dialogService;

    public MysteryGiftEditorViewModel(SaveFile sav, IDialogService dialogService)
    {
        _sav = sav;
        _dialogService = dialogService;
        _storage = GetStorage(sav);

        if (_storage is not null)
        {
            GiftCount = _storage.GiftCountMax;
            IsSupported = true;
            LoadGifts();
        }
        else
        {
            GiftCount = 0;
            IsSupported = false;
        }
    }

    private static IMysteryGiftStorage? GetStorage(SaveFile sav)
    {
        if (sav is IMysteryGiftStorageProvider provider)
            return provider.MysteryGiftStorage;
        return null;
    }

    public int GiftCount { get; }
    public bool IsSupported { get; }

    [ObservableProperty]
    private ObservableCollection<MysteryGiftSlotViewModel> _gifts = [];

    [ObservableProperty]
    private MysteryGiftSlotViewModel? _selectedGift;

    private void LoadGifts()
    {
        if (_storage is null) return;

        Gifts.Clear();
        for (int i = 0; i < GiftCount; i++)
        {
            var gift = _storage.GetMysteryGift(i);
            Gifts.Add(new MysteryGiftSlotViewModel(i, gift));
        }

        if (Gifts.Count > 0)
            SelectedGift = Gifts[0];
    }

    [RelayCommand]
    private void SelectGift(MysteryGiftSlotViewModel? gift)
    {
        if (gift is not null)
            SelectedGift = gift;
    }

    [RelayCommand]
    private void Save()
    {
        if (_storage is null) return;

        foreach (var slot in Gifts)
        {
            if (slot.Gift is not null)
                _storage.SetMysteryGift(slot.Index, slot.Gift);
        }
    }

    [RelayCommand]
    private void Reset()
    {
        LoadGifts();
    }

    [RelayCommand]
    private async Task ImportGiftAsync()
    {
        if (_storage is null || SelectedGift is null) return;

        var path = await _dialogService.OpenFileAsync(
            "Import Mystery Gift",
            ["*.wc9", "*.wa9", "*.wc8", "*.wa8", "*.wb8", "*.wc7", "*.wc6", "*.pgf", "*.pgt", "*.pcd", "*"]);

        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var data = await System.IO.File.ReadAllBytesAsync(path);
            var ext = System.IO.Path.GetExtension(path);
            var gift = MysteryGift.GetMysteryGift(data, ext);

            if (gift is null)
            {
                await _dialogService.ShowErrorAsync("Import Error", "Could not parse the mystery gift file.");
                return;
            }

            // Check compatibility
            if (gift.Generation != _sav.Generation)
            {
                await _dialogService.ShowErrorAsync("Import Error",
                    $"Gift generation ({gift.Generation}) does not match save generation ({_sav.Generation}).");
                return;
            }

            SelectedGift.Gift = gift;
            SelectedGift.UpdateFromGift();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Import Error", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportGiftAsync()
    {
        if (SelectedGift?.Gift is null || SelectedGift.IsEmpty) return;

        var gift = SelectedGift.Gift;
        var defaultName = gift.FileName;

        var path = await _dialogService.SaveFileAsync("Export Mystery Gift", defaultName);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var data = gift.Write().ToArray();
            await System.IO.File.WriteAllBytesAsync(path, data);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Export Error", ex.Message);
        }
    }

    [RelayCommand]
    private void DeleteGift()
    {
        if (SelectedGift?.Gift is null) return;

        SelectedGift.Gift.Clear();
        SelectedGift.UpdateFromGift();
    }
}

public partial class MysteryGiftSlotViewModel : ViewModelBase
{
    public MysteryGiftSlotViewModel(int index, DataMysteryGift? gift)
    {
        Index = index;
        Gift = gift;
        UpdateFromGift();
    }

    public int Index { get; }
    public DataMysteryGift? Gift { get; set; }

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _species = string.Empty;

    [ObservableProperty]
    private string _summary = string.Empty;

    public void UpdateFromGift()
    {
        if (Gift is null || Gift.IsEmpty)
        {
            IsEmpty = true;
            Title = $"Slot {Index + 1}: (Empty)";
            Species = string.Empty;
            Summary = string.Empty;
        }
        else
        {
            IsEmpty = false;
            Title = $"Slot {Index + 1}: {Gift.CardTitle}";

            if (Gift.IsEntity)
            {
                var speciesId = Gift.Species;
                var speciesNames = GameInfo.Strings.Species;
                Species = speciesId < speciesNames.Count ? speciesNames[speciesId] : $"Species #{speciesId}";
                Summary = $"Lv. {Gift.Level} | {Gift.OriginalTrainerName}";
            }
            else
            {
                Species = Gift.Type.ToString();
                Summary = string.Empty;
            }
        }
    }
}
