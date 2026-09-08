using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ArabellaMod.Powers;

[RegisterPower]
public sealed class ArabellaLustfireVeilPower : ArabellaRewardPower
{
    protected override string FileName => "icon_1601111_chain_scale.png";

    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // Same attack-instance boundary as Flame Barrier, including fully blocked hits.
        if (target != Owner || dealer == null || !dealer.IsAlive ||
            dealer.Side == Owner.Side || !props.IsPoweredAttack())
            return;

        Flash();
        await PowerCmd.Apply<ArabellaLacerationPower>(
            choiceContext, dealer, Amount, Owner, null);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side)
            await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class ArabellaCoiledBladeRecoveryPower : ArabellaRewardPower
{
    protected override string FileName => "icon_1601211_chain_scale.png";

    private sealed class Data
    {
        public readonly Dictionary<CardModel, int> Prepared = [];
    }

    protected override object InitInternalData() => new Data();

    public bool HasPreparedRepeat(CardModel card) =>
        card.Owner.Creature == Owner &&
        GetInternalData<Data>().Prepared.GetValueOrDefault(card) > 0;

    // Track physical card instances: another copy with the same ID gets no repeat.
    public void Prepare(CardModel card)
    {
        Dictionary<CardModel, int> prepared = GetInternalData<Data>().Prepared;
        prepared[card] = prepared.GetValueOrDefault(card) + 1;
        card.InvokeEnergyCostChanged();
    }

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Owner.Creature != Owner || card.Type != CardType.Attack)
            return playCount;
        return playCount + GetInternalData<Data>().Prepared.GetValueOrDefault(card);
    }

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (!GetInternalData<Data>().Prepared.Remove(card, out int repeats))
            return;

        card.InvokeEnergyCostChanged();

        // Consume before the series starts. Native repetition pays once and moves the
        // physical card to its result pile once, so it cannot recursively repeat itself.
        Flash();
        await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -repeats, Owner, null);
    }
}
