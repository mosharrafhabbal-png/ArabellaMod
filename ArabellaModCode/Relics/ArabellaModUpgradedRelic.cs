using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Relics;

/// <summary>
/// Arabella's upgraded starter relic. Power cards may exhaust a card from either
/// the hand or draw pile, and every ten Lust actually gained restores one HP.
/// </summary>
[RegisterRelic(typeof(Characters.ArabellaModRelicPool))]
public sealed class ArabellaModUpgradedRelic : ModRelicTemplate, ISecondaryResourceHookListener
{
    private const int LustPerHeal = 10;
    private int _lustTowardsHealing;

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override bool ShowCounter => true;

    public override int DisplayAmount => LustTowardsHealing;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Lust", LustPerHeal), new HealVar(1m)];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/ArabellaModRelic.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/ArabellaModRelic.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/ArabellaModRelic.png");

    [SavedProperty]
    public int LustTowardsHealing
    {
        get => _lustTowardsHealing;
        set
        {
            AssertMutable();
            _lustTowardsHealing = Math.Max(0, value);
            InvokeDisplayAmountChanged();
        }
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || cardPlay.Card.Type != CardType.Power)
            return;

        IReadOnlyList<CardModel> hand = PileType.Hand.GetPile(Owner).Cards;
        IReadOnlyList<CardModel> drawPile = PileType.Draw.GetPile(Owner).Cards;
        if (hand.Count == 0 && drawPile.Count == 0)
            return;

        // Draw-pile order is hidden information. Sort those cards before combining
        // both piles so this selection does not reveal their actual draw order.
        List<CardModel> options = hand
            .Concat(drawPile.OrderBy(card => card.Rarity).ThenBy(card => card.Id))
            .Distinct()
            .ToList();

        CardModel? selected = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            options,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, 1)))
            .FirstOrDefault();

        if (selected == null)
            return;

        Flash();
        await CardCmd.Exhaust(choiceContext, selected);
    }

    public async Task AfterSecondaryResourceChanged(
        SecondaryResourceChangeContext context)
    {
        if (context.Player != Owner ||
            context.Definition.Id != ArabellaMod.Mechanics.ArabellaResources.LustId ||
            context.Reason != SecondaryResourceChangeReason.Gain ||
            context.Delta <= 0)
        {
            return;
        }

        int total = LustTowardsHealing + context.Delta;
        int healing = total / LustPerHeal;
        LustTowardsHealing = total % LustPerHeal;
        if (healing <= 0)
            return;

        Flash();
        await CreatureCmd.Heal(Owner.Creature, healing);
    }
}

/// <summary>
/// Extends Touch of Orobas' built-in starter-relic upgrade table for Arabella.
/// </summary>
[HarmonyPatch(typeof(TouchOfOrobas), nameof(TouchOfOrobas.GetUpgradedStarterRelic))]
internal static class ArabellaStarterRelicUpgradePatch
{
    private static void Postfix(RelicModel starterRelic, ref RelicModel __result)
    {
        if (starterRelic is ArabellaModRelic)
            __result = ModelDb.Relic<ArabellaModUpgradedRelic>();
    }
}
