using PKHeX.Application.Models;
using PKHeX.Core;
using PKHeX.Presentation.Localization;

namespace PKHeX.Presentation.Models;

public static class SpriteStateDescription
{
    public static string Describe(PKM pk, SpriteSlotState state)
    {
        var labels = new List<string>();
        var strings = LocalizedStrings.Instance;
        if (pk.Species != 0)
        {
            if (pk.IsEgg) labels.Add(strings["SlotState_Egg"]);
            if (pk.IsShiny) labels.Add(strings["SlotState_Shiny"]);
            if (pk.HeldItem > 0) labels.Add(StringResourceLookup.HeldItem(pk));
            if (pk is IAlphaReadOnly { IsAlpha: true }) labels.Add(strings["SlotState_Alpha"]);
            if (pk is IGigantamaxReadOnly { CanGigantamax: true }) labels.Add(strings["SlotState_Gigantamax"]);
            if (state.Illegal) labels.Add(strings["SlotState_Illegal"]);
            if (state.MoveHint) labels.Add(strings["SlotState_MoveHint"]);
        }
        if (state.Storage.IsBattleTeam() >= 0) labels.Add(strings["SlotState_Team"]);
        if (state.Storage.HasFlag(StorageSlotSource.Locked)) labels.Add(strings["SlotState_Locked"]);
        if (state.Storage.IsParty() is var party && party >= 0) labels.Add(strings.Format("SlotState_Party", party + 1));
        if (state.Storage.HasFlag(StorageSlotSource.Starter)) labels.Add(strings["SlotState_Starter"]);
        return string.Join(", ", labels);
    }
}
