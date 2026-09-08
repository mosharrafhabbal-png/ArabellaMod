using ArabellaMod.Characters;
using ArabellaMod.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Cards;

/// <summary>
/// 初次驯服：同时教授欲火费用、掌控与感应的基础攻击。
/// </summary>
[RegisterCard(typeof(ArabellaModCardPool))]
[RegisterCharacterStarterCard(typeof(ArabellaModCharacter), 1)]
public sealed class ArabellaFirstTaming : ModCardTemplate, IControlCard, ISensingCard, IArabellaExcitementCostCard
{
    public int ControlThreshold => 5;

    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ArabellaCardArt.FirstTaming);

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust,
        ArabellaKeywords.Control,
        ArabellaKeywords.Sensing
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(14m, ValueProp.Move),
        new BlockVar(4m, ValueProp.Unpowered)
    ];

    public ArabellaFirstTaming()
        : base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, true)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute(choiceContext);
    }

    public Task OnSensing()
    {
        return CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block.BaseValue,
            DynamicVars.Block.Props,
            cardPlay: null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}
