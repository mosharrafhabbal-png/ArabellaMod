using ArabellaMod.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Powers;

public abstract class ArabellaRewardPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected abstract string FileName { get; }

    public override PowerAssetProfile AssetProfile =>
        ArabellaPowerIcons.Create(FileName);
}

[RegisterPower]
public sealed class ArabellaMomentumRemainsPower : ArabellaRewardPower
{
    protected override string FileName => "icon_1601211_chain_scale.png";

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card.Owner.Creature != Owner || CombatState.HittableEnemies.Count == 0)
            return;

        Creature? target = Owner.Player!.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
        if (target == null)
            return;

        Flash();
        await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Unpowered, Owner);
    }
}

[RegisterPower]
public sealed class ArabellaEscalatingPower : ArabellaRewardPower
{
    protected override string FileName => "icon_1601211_atkult_scale.png";

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card.Owner.Creature != Owner)
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, cardPlay: null);
    }
}

[RegisterPower]
public sealed class ArabellaHighPressureTamingPower : ArabellaRewardPower
{
    protected override string FileName => "icon_1601701_atkult_scale.png";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("UpgradedStacks", 0m)];

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this && amount > 0m &&
            cardSource is ArabellaHighPressureTaming { IsUpgraded: true })
        {
            DynamicVars["UpgradedStacks"].BaseValue += amount;
        }

        return Task.CompletedTask;
    }

    public void OnControl(CardModel triggeringCard)
    {
        Flash();
        triggeringCard.EnergyCost.AddThisTurn(-Amount, reduceOnly: true);

        int upgradedBonus = DynamicVars["UpgradedStacks"].IntValue * 2;
        if (upgradedBonus <= 0)
            return;

        if (triggeringCard.DynamicVars.TryGetValue("Damage", out DynamicVar? damage))
            damage.BaseValue += upgradedBonus;
        if (triggeringCard.DynamicVars.TryGetValue("Block", out DynamicVar? block))
            block.BaseValue += upgradedBonus;
    }
}

[RegisterPower]
public sealed class ArabellaSerpentDanceRhythmPower : ArabellaRewardPower
{
    protected override string FileName => "icon_1400811_chain_scale.png";

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

        Data data = GetInternalData<Data>();
        CardType currentType = cardPlay.Card.Type;
        bool switched =
            (data.PreviousType == CardType.Attack && currentType == CardType.Skill) ||
            (data.PreviousType == CardType.Skill && currentType == CardType.Attack);
        data.PreviousType = currentType;

        if (!switched || CombatState.HittableEnemies.Count == 0)
            return;

        Creature? target = Owner.Player!.RunState.Rng.CombatTargets.NextItem(CombatState.HittableEnemies);
        if (target == null)
            return;

        Flash();
        await CreatureCmd.Damage(choiceContext, target, Amount, ValueProp.Unpowered, Owner);
    }
}

[RegisterPower]
public sealed class ArabellaSensoryHeatingPower : ArabellaRewardPower
{
    protected override string FileName => "icon_1601331_zhuangbei_scale.png";

    public async Task OnSensing(PlayerChoiceContext choiceContext)
    {
        Flash();
        await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, cardPlay: null);
    }
}
