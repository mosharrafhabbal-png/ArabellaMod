using ArabellaMod.Characters;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace ArabellaMod.Mechanics;

/// <summary>
/// 每个造成未格挡伤害的实例使艾拉贝拉失去 2 兴奋。
/// </summary>
[RegisterSingleton]
public sealed class ExcitementSingleton : HookedSingletonModel
{
    public ExcitementSingleton() : base(HookType.Combat)
    {
    }

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target.Player is not { Character: ArabellaModCharacter } player ||
            result.UnblockedDamage <= 0 ||
            props.HasFlag(ValueProp.Unpowered) ||
            target.GetPower<ArabellaPainTamingPower>()?.PreventsExcitementLoss == true)
        {
            return Task.CompletedTask;
        }

        return ArabellaExcitement.Lose(player, 2, this);
    }

    public override bool TryModifyEnergyCostInCombatLate(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        PlayerCombatState? playerCombatState = card.Owner.PlayerCombatState;
        if (playerCombatState == null)
        {
            ArabellaExcitementPayment.Clear(card);
            return false;
        }

        int energyShortfall = ArabellaExcitementPayment.Refresh(
            card,
            originalCost,
            playerCombatState.Energy);
        if (energyShortfall <= 0)
            return false;

        modifiedCost = playerCombatState.Energy;
        return true;
    }
}

/// <summary>
/// Builds a required Lust payment equal to the Energy that Arabella is missing.
/// The matching late Energy hook lowers only that missing portion, so the two
/// payment lines always describe the same substitution.
/// </summary>
internal static class ArabellaExcitementPayment
{
    private const string UseId = "excitement_energy_replacement";

    /// <summary>
    /// Rebuilds the Lust substitution from the same Energy cost currently being
    /// evaluated by the game. This must happen on every cost query: both current
    /// Energy and temporary card-cost modifiers can change after a card enters combat.
    /// </summary>
    public static int Refresh(CardModel card, decimal energyCost, int availableEnergy)
    {
        int shortfall = CanReplaceEnergy(card)
            ? Math.Max(0, decimal.ToInt32(energyCost) - availableEnergy)
            : 0;

        if (shortfall <= 0)
        {
            Clear(card);
            return 0;
        }

        SecondaryResourceCost desiredCost = new(shortfall, false, 0);
        SecondaryResourcePlayUse? existingUse = null;
        if (card.TryGetSecondaryResourceUses(out SecondaryResourcePlayUseSet useSet))
            existingUse = useSet.Snapshot().FirstOrDefault(use => use.Id == UseId);

        if (existingUse == null ||
            existingUse.ResourceId != ArabellaResources.LustId ||
            existingUse.Kind != SecondaryResourceUseKind.RequiredCost ||
            existingUse.Duration != SecondaryResourceCostDuration.ThisCombat ||
            existingUse.Cost != desiredCost)
        {
            card.SecondaryResourceUses().Set(
                UseId,
                ArabellaResources.LustId,
                desiredCost,
                SecondaryResourceUseKind.RequiredCost,
                SecondaryResourceCostDuration.ThisCombat);
        }

        return shortfall;
    }

    public static bool CanReplaceEnergy(CardModel card)
    {
        return card.Owner.Character is ArabellaModCharacter &&
               card.CombatState != null &&
               !card.EnergyCost.CostsX &&
               ArabellaExcitement.Get(card.Owner) >= ArabellaExcitement.ExcitedThreshold;
    }

    public static void Clear(CardModel card)
    {
        if (card.TryGetSecondaryResourceUses(out SecondaryResourcePlayUseSet useSet) &&
            useSet.UseIds.Contains(UseId))
            useSet.Clear(UseId);
    }
}
