using ArabellaMod.Cards;
using ArabellaMod.Mechanics;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Powers;

[RegisterPower]
public sealed class ArabellaIndulgentNaturePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => ArabellaPowerIcons.Create(
        "icon_1601031_atkult_scale.png");

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner.Creature != Owner || !ArabellaCardLogic.CanGainLustFromExhaust(card))
            return;

        Flash();
        await SecondaryResourceCmd.Gain(Owner.Player!, ArabellaResources.LustId, Amount, this);
    }
}
