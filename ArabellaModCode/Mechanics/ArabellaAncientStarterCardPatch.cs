using ArabellaMod.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace ArabellaMod.Mechanics;

/// <summary>
/// Extend the native table so previews, upgrade/enchantment transfer and Dusty Tome's
/// exclusion of ancient starter cards all use the same mapping.
/// </summary>
[HarmonyPatch(typeof(ArchaicTooth), "TranscendenceUpgrades", MethodType.Getter)]
internal static class ArabellaAncientStarterCardPatch
{
    private static void Postfix(Dictionary<ModelId, CardModel> __result)
    {
        __result[ModelDb.Card<ArabellaSerpentineNature>().Id] =
            ModelDb.Card<ArabellaIndulgentNature>();
    }
}
