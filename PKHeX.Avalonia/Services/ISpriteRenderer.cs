using Avalonia.Media.Imaging;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

public interface ISpriteRenderer
{
    Bitmap? GetSprite(PKM pk, bool isEgg = false);
    Bitmap? GetEmptySlot();
    void Initialize(SaveFile sav);
}
