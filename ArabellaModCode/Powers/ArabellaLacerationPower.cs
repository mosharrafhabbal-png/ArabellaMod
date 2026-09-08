using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Powers;

/// <summary>
/// 裂创：敌方回合结束时失去生命，随后层数减半（向下取整）。
/// </summary>
[RegisterPower]
public sealed class ArabellaLacerationPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => ArabellaPowerIcons.Create(
        "icon_1501711_atkult_scale.png");

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants)
    {
        if (side == CombatSide.Enemy && Owner.IsMonster && participants.Contains(Owner))
            await Trigger(choiceContext, null);
    }

    public async Task Trigger(PlayerChoiceContext choiceContext, CardModel? cardSource)
    {
        int amount = Amount;
        if (amount <= 0 || !Owner.IsAlive)
            return;

        MegaCrit.Sts2.Core.Entities.Creatures.Creature? applier = Applier;
        int amplification = applier?
            .GetPower<ArabellaLacerationAmplificationPower>()?.Amount ?? 0;

        Flash();
        using (ArabellaLacerationDamageContext.Enter(Owner))
        {
            await CreatureCmd.Damage(
                choiceContext,
                Owner,
                amount,
                ValueProp.Unblockable | ValueProp.Unpowered,
                Applier,
                cardSource,
                null);
        }

        int remaining = amount / 2;
        if (Owner.GetPower<ArabellaLacerationPower>() == this)
        {
            await PowerCmd.ModifyAmount(
                choiceContext,
                this,
                remaining - amount,
                applier,
                cardSource);
        }

        if (amplification > 0 && Owner.IsAlive)
        {
            await PowerCmd.Apply<ArabellaLacerationPower>(
                choiceContext,
                Owner,
                amplification * 2,
                applier,
                cardSource);
        }
    }
}

/// <summary>
/// Carries the Laceration source through CreatureCmd's asynchronous damage pipeline so
/// the history hook can synchronize its dedicated impact feedback with the damage number.
/// </summary>
internal static class ArabellaLacerationDamageContext
{
    private static readonly AsyncLocal<MegaCrit.Sts2.Core.Entities.Creatures.Creature?>
        ActiveTarget = new();

    public static bool IsActiveFor(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target) =>
        ReferenceEquals(ActiveTarget.Value, target);

    public static IDisposable Enter(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature target)
    {
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? previous = ActiveTarget.Value;
        ActiveTarget.Value = target;
        return new Scope(previous);
    }

    private sealed class Scope(
        MegaCrit.Sts2.Core.Entities.Creatures.Creature? previous) : IDisposable
    {
        public void Dispose()
        {
            ActiveTarget.Value = previous;
        }
    }
}
