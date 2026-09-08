using ArabellaMod.Characters;
using ArabellaMod.Mechanics;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ArabellaMod.Cards;

/// <summary>
/// 蛇蝎天性：把本场战斗中的每次消耗转化为欲火。
/// </summary>
[RegisterCard(typeof(ArabellaModCardPool))]
[RegisterCharacterStarterCard(typeof(ArabellaModCharacter), 1)]
public sealed class ArabellaSerpentineNature : ModCardTemplate, IArabellaExcitementCostCard
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ArabellaCardArt.Numbered(5));

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Innate];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ArabellaSerpentineNaturePower>(1m)
    ];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<ArabellaSerpentineNaturePower>()
    ];

    public ArabellaSerpentineNature()
        : base(1, CardType.Power, CardRarity.Basic, TargetType.Self, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SecondaryResourceCmd.Gain(
            Owner,
            ArabellaResources.LustId,
            1,
            this);
        await PowerCmd.Apply<ArabellaSerpentineNaturePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ArabellaSerpentineNaturePower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}
