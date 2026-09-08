using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Powers;

public abstract class ArabellaTemporaryRewardPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected abstract string FileName { get; }

    public override PowerAssetProfile AssetProfile =>
        ArabellaPowerIcons.Create(FileName);

    protected async Task RemoveAtOwnerTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class ArabellaApproachingLimitPower : ArabellaTemporaryRewardPower
{
    protected override string FileName => "icon_1601111_chain_scale.png";

    public void OnControl(CardModel triggeringCard)
    {
        Flash();
        triggeringCard.EnergyCost.AddThisTurn(-Amount, reduceOnly: true);
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants) =>
        RemoveAtOwnerTurnEnd(side, participants);
}

[RegisterPower]
public sealed class ArabellaSnakeWalkPower : ArabellaTemporaryRewardPower
{
    protected override string FileName => "icon_1602211_zhuangbei_scale.png";

    private sealed class Data
    {
        public CardType? PreviousType;
    }

    protected override object InitInternalData() => new Data();

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner || !cardPlay.IsLastInSeries)
            return;

        CardType current = cardPlay.Card.Type;
        Data data = GetInternalData<Data>();
        bool switched =
            data.PreviousType == CardType.Attack && current == CardType.Skill ||
            data.PreviousType == CardType.Skill && current == CardType.Attack;
        data.PreviousType = current;

        if (!switched)
            return;

        Flash();
        await CreatureCmd.GainBlock(
            Owner,
            Amount,
            ValueProp.Unpowered,
            cardPlay: null);
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants) =>
        RemoveAtOwnerTurnEnd(side, participants);
}

[RegisterPower]
public sealed class ArabellaEnergyDebtPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => ArabellaPowerIcons.Create(
        "icon_1602271_atkult_scale.png");

    public override async Task AfterEnergyReset(Player player)
    {
        if (player != Owner.Player)
            return;

        await PlayerCmd.LoseEnergy(Amount, player);
        await PowerCmd.Remove(this);
    }
}
