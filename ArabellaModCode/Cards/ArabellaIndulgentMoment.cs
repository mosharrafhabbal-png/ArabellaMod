using ArabellaMod.Characters;
using ArabellaMod.Mechanics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Cards;

/// <summary>
/// 纵情一瞬：正常打出时过牌，被消耗时通过放纵获得格挡。
/// </summary>
[RegisterCard(typeof(ArabellaModCardPool))]
[RegisterCharacterStarterCard(typeof(ArabellaModCharacter), 1)]
public sealed class ArabellaIndulgentMoment : ModCardTemplate, IIndulgenceCard, IArabellaExcitementCostCard
{
    public override bool GainsBlock => true;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ArabellaCardArt.Numbered(3));

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        ArabellaKeywords.Indulgence
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(1),
        new BlockVar(7m, ValueProp.Unpowered)
    ];

    public ArabellaIndulgentMoment()
        : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, Owner);
    }

    public Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        return CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block.BaseValue,
            DynamicVars.Block.Props,
            cardPlay: null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
    }
}
