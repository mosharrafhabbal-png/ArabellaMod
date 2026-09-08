using ArabellaMod.Characters;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ArabellaMod.Cards;

public abstract class ArabellaRewardPowerCard<TPower> : ArabellaRewardCard
    where TPower : PowerModel
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<TPower>()];

    protected ArabellaRewardPowerCard(int energyCost)
        : base(energyCost, CardType.Power, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected async Task ApplyPower(PlayerChoiceContext choiceContext, decimal amount)
    {
        await PowerCmd.Apply<TPower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
    }
}

[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaMomentumRemains : ArabellaRewardPowerCard<ArabellaMomentumRemainsPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaMomentumRemainsPower>(3m)];

    public ArabellaMomentumRemains() : base(1) { }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaMomentumRemainsPower"].BaseValue);

    protected override void OnUpgrade() =>
        DynamicVars["ArabellaMomentumRemainsPower"].UpgradeValueBy(1m);
}

[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaEscalating : ArabellaRewardPowerCard<ArabellaEscalatingPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaEscalatingPower>(2m)];

    public ArabellaEscalating() : base(1) { }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaEscalatingPower"].BaseValue);

    protected override void OnUpgrade() =>
        DynamicVars["ArabellaEscalatingPower"].UpgradeValueBy(1m);
}

[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaHighPressureTaming : ArabellaRewardPowerCard<ArabellaHighPressureTamingPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaHighPressureTamingPower>(1m)];

    public ArabellaHighPressureTaming() : base(2) { }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaHighPressureTamingPower"].BaseValue);

    protected override void OnUpgrade()
    {
        // The upgraded behavior is recorded by the Power from this card source when applied.
    }
}

[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSerpentDanceRhythm : ArabellaRewardPowerCard<ArabellaSerpentDanceRhythmPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaSerpentDanceRhythmPower>(2m)];

    public ArabellaSerpentDanceRhythm() : base(1) { }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaSerpentDanceRhythmPower"].BaseValue);

    protected override void OnUpgrade() =>
        DynamicVars["ArabellaSerpentDanceRhythmPower"].UpgradeValueBy(1m);
}

[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSensoryHeating : ArabellaRewardPowerCard<ArabellaSensoryHeatingPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaSensoryHeatingPower>(2m)];

    public ArabellaSensoryHeating() : base(1) { }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaSensoryHeatingPower"].BaseValue);

    protected override void OnUpgrade() =>
        DynamicVars["ArabellaSensoryHeatingPower"].UpgradeValueBy(1m);
}
