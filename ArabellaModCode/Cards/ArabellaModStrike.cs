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
/// 斜切打击：艾拉贝拉的基础群体打击。
/// </summary>
[RegisterCard(typeof(ArabellaModCardPool))]
[RegisterCharacterStarterCard(typeof(ArabellaModCharacter), 4)]
public sealed class ArabellaModStrike : ModCardTemplate, IArabellaExcitementCostCard
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ArabellaCardArt.Attack);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4, ValueProp.Move)
    ];

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    public ArabellaModStrike()
        : base(1, CardType.Attack, CardRarity.Basic, TargetType.AllEnemies, true)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
    }
}
