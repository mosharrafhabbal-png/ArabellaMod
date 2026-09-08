using ArabellaMod.Powers;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace ArabellaMod.Mechanics;

[HarmonyPatch]
internal static class ArabellaCardGlow
{
    // These effects track card instances, including vanilla and other mods' cards.
    // RitsuLib's type registry rejects the abstract CardModel base class.
    // Add to the native results instead, preserving all existing glow conditions.
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.ShouldGlowGold), MethodType.Getter)]
    [HarmonyPostfix]
    private static void AddPreparedRepeatGlow(CardModel __instance, ref bool __result) =>
        __result |= HasPreparedRepeat(__instance);

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.ShouldGlowRed), MethodType.Getter)]
    [HarmonyPostfix]
    private static void AddControlReturnGlow(CardModel __instance, ref bool __result) =>
        __result |= ControlSingleton.WasReturnedByControl(__instance) && !HasPreparedRepeat(__instance);

    private static bool HasPreparedRepeat(CardModel card) =>
        card.Pile?.Type == PileType.Hand &&
        card.Owner?.Creature.GetPower<ArabellaCoiledBladeRecoveryPower>()?.HasPreparedRepeat(card) == true;
}
