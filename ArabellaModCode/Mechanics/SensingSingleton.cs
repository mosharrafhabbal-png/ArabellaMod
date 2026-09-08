using ArabellaMod.Characters;
using ArabellaMod.Cards;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace ArabellaMod.Mechanics;

/// <summary>
/// Handles hand-entry Sensing (感应) and the pile movement that generates Lust.
/// Keeping both rules here ensures there are exactly three global mechanic singletons.
/// </summary>
[RegisterSingleton]
public sealed class SensingSingleton : HookedSingletonModel
{
    private readonly Dictionary<Player, int> _triggersThisTurn = [];
    private static WeakReference<SensingSingleton>? _activeInstance;

    public SensingSingleton() : base(HookType.Combat)
    {
        _activeInstance = new WeakReference<SensingSingleton>(this);
    }

    public static int GetTriggersThisTurn(Player player)
    {
        if (_activeInstance is null ||
            !_activeInstance.TryGetTarget(out SensingSingleton? active))
        {
            return 0;
        }

        return active._triggersThisTurn.GetValueOrDefault(player);
    }

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        CardPile? newPile = card.Pile;
        if (newPile is null || card.Owner.Character is not ArabellaModCharacter)
            return;

        // Moving a card out of Exhaust and back into active circulation generates 1 Lust.
        if (oldPileType == PileType.Exhaust &&
            newPile.Type is PileType.Hand or PileType.Draw)
        {
            await SecondaryResourceCmd.Gain(card.Owner, ArabellaResources.LustId, 1, this);
        }

        if (newPile.Type != PileType.Hand ||
            oldPileType == PileType.Hand ||
            !card.Keywords.Contains(ArabellaKeywords.Sensing) ||
            card is not ISensingCard sensingCard)
        {
            return;
        }

        // Make it immediately clear which card caused Sensing to trigger.
        CardCmd.Preview(card);

        _triggersThisTurn[card.Owner] =
            _triggersThisTurn.GetValueOrDefault(card.Owner) + 1;

        await sensingCard.OnSensing();

        if (card is IArabellaSensingDerivedCard)
            await CardPileCmd.Draw(new ThrowingPlayerChoiceContext(), 1, card.Owner);

        ArabellaSensoryHeatingPower? sensoryHeating =
            card.Owner.Creature.GetPower<ArabellaSensoryHeatingPower>();
        if (sensoryHeating != null)
        {
            await sensoryHeating.OnSensing(new ThrowingPlayerChoiceContext());
        }

        if (ArabellaExcitement.Get(card.Owner) >= ArabellaExcitement.RestlessThreshold)
        {
            await CreatureCmd.GainBlock(
                card.Owner.Creature,
                2m,
                ValueProp.Unpowered,
                cardPlay: null);
        }
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
        {
            foreach (Player player in participants
                         .Select(creature => creature.Player)
                         .Where(player => player != null)
                         .Cast<Player>())
            {
                _triggersThisTurn.Remove(player);
            }
        }

        return Task.CompletedTask;
    }

}
