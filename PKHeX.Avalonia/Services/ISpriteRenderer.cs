using Avalonia.Media.Imaging;
using PKHeX.Core;

namespace PKHeX.Avalonia.Services;

public interface ISpriteRenderer
{
    global::Avalonia.Media.Imaging.Bitmap? GetSprite(PKM pk, bool isEgg = false);
    global::Avalonia.Media.Imaging.Bitmap? GetSprite(ushort species, byte form, byte gender, uint formarg, bool shiny, PKHeX.Core.EntityContext context);
    Bitmap? GetEmptySlot();
    void Initialize(SaveFile sav);
}
