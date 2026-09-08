using ArabellaMod.Characters;
using ArabellaMod.Mechanics;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Cards;

/// <summary>
/// 纵欲本相：蛇蝎天性的先古版本，只额外结算卡牌自身的放纵效果。
/// </summary>
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaIndulgentNature : ModCardTemplate, IArabellaExcitementCostCard
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"{Entry.ResPath}/images/cards/005shengji.png");

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Lust", 2m), new PowerVar<ArabellaIndulgentNaturePower>(1m)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaIndulgentNaturePower>()];

    public ArabellaIndulgentNature()
        : base(1, CardType.Power, CardRarity.Ancient, TargetType.Self, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SecondaryResourceCmd.Gain(Owner, ArabellaResources.LustId,
            DynamicVars["Lust"].IntValue, this);
        await PowerCmd.Apply<ArabellaIndulgentNaturePower>(choiceContext, Owner.Creature,
            DynamicVars["ArabellaIndulgentNaturePower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
