using ArabellaMod.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Powers;

/// <summary>
/// Reduces only the next player-turn hand draw, then removes itself.
/// </summary>
[RegisterPower]
public sealed class ArabellaDrawDebtPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => ArabellaPowerIcons.Create(
        "icon_1602271_chain_scale.png");

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner.Player || AmountOnTurnStart <= 0)
            return count;

        return Math.Max(0m, count - AmountOnTurnStart);
    }

    public override Task AfterModifyingHandDraw()
    {
        if (AmountOnTurnStart > 0)
            Flash();

        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (AmountOnTurnStart > 0 && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

/// <summary>
/// Increases the cost of the first formal card played next turn. Multiple
/// Withdrawals stack onto that one card, and unused debt expires at turn end.
/// </summary>
[RegisterPower]
public sealed class ArabellaWithdrawalDebtPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => ArabellaPowerIcons.Create(
        "icon_1601121_zhuangbei_scale.png");

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (AmountOnTurnStart <= 0 ||
            card.Owner.Creature != Owner ||
            !IsFormalCard(card))
        {
            return false;
        }

        modifiedCost = originalCost + AmountOnTurnStart;
        return true;
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (AmountOnTurnStart > 0 &&
            cardPlay.IsLastInSeries &&
            cardPlay.Card.Owner.Creature == Owner &&
            IsFormalCard(cardPlay.Card))
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (AmountOnTurnStart > 0 && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    private static bool IsFormalCard(CardModel card)
    {
        return card is not IArabellaDerivedCard &&
               card.Type is CardType.Attack or CardType.Skill or CardType.Power;
    }
}
