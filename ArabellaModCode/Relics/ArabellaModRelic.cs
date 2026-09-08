using ArabellaMod.Characters;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Relics;

/// <summary>
/// 艾拉贝拉的初始遗物。能力牌结算后，可以消耗至多一张其他手牌。
/// </summary>
[RegisterRelic(typeof(ArabellaModRelicPool))]
[RegisterCharacterStarterRelic(typeof(ArabellaModCharacter))]
public sealed class ArabellaModRelic : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"{Entry.ResPath}/images/relics/ArabellaModRelic.png",
        IconOutlinePath: $"{Entry.ResPath}/images/relics/ArabellaModRelic.png",
        BigIconPath: $"{Entry.ResPath}/images/relics/ArabellaModRelic.png");

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner || cardPlay.Card.Type != CardType.Power)
            return;

        if (PileType.Hand.GetPile(Owner).Cards.Count == 0)
            return;

        CardModel? selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, 1),
            filter: null,
            source: this)).FirstOrDefault();

        if (selected == null)
            return;

        Flash();
        await CardCmd.Exhaust(choiceContext, selected);
    }
}
