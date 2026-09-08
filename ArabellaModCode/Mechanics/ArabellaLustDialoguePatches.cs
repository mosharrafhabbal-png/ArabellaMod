using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ArabellaMod.Mechanics;

/// <summary>
/// Replaces the vanilla generic unplayable thought with an Arabella-specific
/// message when the selected card is blocked by an unaffordable Lust cost.
/// </summary>
[HarmonyPatch(
    typeof(NThoughtBubbleVfx),
    nameof(NThoughtBubbleVfx.Create),
    [typeof(string), typeof(Creature), typeof(double?)])]
internal static class ArabellaLustInsufficientThoughtPatch
{
    private const string NotEnoughLustThought =
        "还没有足够的[color=#ff3048]欲火[/color]呢……";

    private static void Prefix(ref string text, Creature speaker)
    {
        NCardPlay? cardPlay = NPlayerHand.Instance?._currentCardPlay;
        CardModel? card = cardPlay?.Holder?.CardModel;

        if (card?.Owner?.Character is not Characters.ArabellaModCharacter ||
            !ReferenceEquals(card.Owner.Creature, speaker) ||
            !card.TryGetSecondaryCosts(out SecondaryResourceCostSet costs) ||
            !costs.ResourceIds.Contains(ArabellaResources.LustId) ||
            SecondaryResourcePaymentResolver.CanPay(card))
        {
            return;
        }

        text = NotEnoughLustThought;
    }
}
