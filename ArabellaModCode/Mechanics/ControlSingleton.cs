using ArabellaMod.Characters;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace ArabellaMod.Mechanics;

/// <summary>
/// 在一张牌完成结算并离开结算区后，按该牌实际支付的能量＋欲火触发掌控。
/// </summary>
[RegisterSingleton]
public sealed class ControlSingleton : HookedSingletonModel, ISecondaryResourceHookListener
{
    private readonly Dictionary<CardModel, int> _pendingPayments = [];
    private readonly Dictionary<CardModel, int> _controlProgress = [];
    private readonly Dictionary<CardModel, int> _grantedControlThresholds = [];
    private readonly Dictionary<CardModel, int> _indulgedCostReductions = [];
    private readonly Dictionary<Player, int> _lustSpentThisTurn = [];
    private readonly Dictionary<Player, int> _lustSpentThisCombat = [];
    private readonly HashSet<Player> _drawOnNextLustSpend = [];
    private readonly HashSet<CardModel> _controlReturnedCards = [];
    private static WeakReference<ControlSingleton>? _activeInstance;

    public ControlSingleton() : base(HookType.Combat)
    {
        _activeInstance = new WeakReference<ControlSingleton>(this);
    }

    public override Task BeforeCombatStart()
    {
        // Hooked combat singletons are reused by RitsuLib. A lethal card can end combat
        // before it leaves the play pile, so its payment transaction must not leak into the
        // next fight. Every collection below is combat-scoped; temporary granted Control is
        // explicitly described and costed as "this combat" by its source card.
        _pendingPayments.Clear();
        _controlProgress.Clear();
        _grantedControlThresholds.Clear();
        _indulgedCostReductions.Clear();
        _lustSpentThisTurn.Clear();
        _lustSpentThisCombat.Clear();
        _drawOnNextLustSpend.Clear();
        _controlReturnedCards.Clear();
        return Task.CompletedTask;
    }

    public static bool WasReturnedByControl(CardModel card) =>
        card.Pile?.Type == PileType.Hand &&
        _activeInstance is not null &&
        _activeInstance.TryGetTarget(out ControlSingleton? active) &&
        active._controlReturnedCards.Contains(card);

    public static int GetProgress(CardModel card)
    {
        if (card.Pile?.Type != PileType.Exhaust ||
            _activeInstance is null ||
            !_activeInstance.TryGetTarget(out ControlSingleton? active))
        {
            return 0;
        }

        return active._controlProgress.GetValueOrDefault(card);
    }

    public static int GetThreshold(CardModel card)
    {
        if (card is IControlCard { ControlThreshold: > 0 } controlCard)
            return controlCard.ControlThreshold;

        return _activeInstance is not null &&
               _activeInstance.TryGetTarget(out ControlSingleton? active)
            ? active._grantedControlThresholds.GetValueOrDefault(card)
            : 0;
    }

    public static void GrantControl(CardModel card, int threshold)
    {
        if (threshold <= 0 || _activeInstance is null ||
            !_activeInstance.TryGetTarget(out ControlSingleton? active))
        {
            return;
        }

        active._grantedControlThresholds[card] = threshold;
        card.AddKeyword(ArabellaKeywords.Control);
        card.AddKeyword(CardKeyword.Exhaust);
    }

    public static async Task TriggerControlNow(CardModel card)
    {
        if (_activeInstance is null ||
            !_activeInstance.TryGetTarget(out ControlSingleton? active) ||
            GetThreshold(card) <= 0)
        {
            return;
        }

        await active.ResolveControl(card, new BlockingPlayerChoiceContext());
    }

    public static int GetLustSpentThisTurn(Player player)
    {
        if (_activeInstance is null ||
            !_activeInstance.TryGetTarget(out ControlSingleton? active))
        {
            return 0;
        }

        return active._lustSpentThisTurn.GetValueOrDefault(player);
    }

    public static int GetLustSpentThisCombat(Player player)
    {
        if (_activeInstance is null ||
            !_activeInstance.TryGetTarget(out ControlSingleton? active))
        {
            return 0;
        }

        return active._lustSpentThisCombat.GetValueOrDefault(player);
    }

    public static void DrawOnNextLustSpend(Player player)
    {
        if (_activeInstance is not null &&
            _activeInstance.TryGetTarget(out ControlSingleton? active))
        {
            active._drawOnNextLustSpend.Add(player);
        }
    }

    public async Task AfterSecondaryResourceSpent(SecondaryResourceSpendContext context)
    {
        if (context.Definition.Id != ArabellaResources.LustId ||
            context.Player.Character is not ArabellaModCharacter ||
            context.Amount <= 0)
        {
            return;
        }

        _lustSpentThisTurn[context.Player] =
            _lustSpentThisTurn.GetValueOrDefault(context.Player) + context.Amount;
        _lustSpentThisCombat[context.Player] =
            _lustSpentThisCombat.GetValueOrDefault(context.Player) + context.Amount;
        await ArabellaExcitement.Gain(context.Player, context.Amount, this);

        if (_drawOnNextLustSpend.Remove(context.Player))
        {
            await CardPileCmd.Draw(
                new ThrowingPlayerChoiceContext(),
                context.Amount,
                context.Player);
        }

        if (context.Player.Creature.GetPower<ArabellaDesireDominationPower>() is { } domination)
            await domination.OnLustSpent(new BlockingPlayerChoiceContext());
    }

    public decimal ModifySecondaryResourceCost(
        SecondaryResourceCostContext context,
        decimal currentCost)
    {
        if (context.Definition.Id != ArabellaResources.LustId)
            return currentCost;

        if (_grantedControlThresholds.ContainsKey(context.Card) && currentCost == 1m)
            currentCost = 2m;

        return currentCost;
    }

    public Task AfterSecondaryResourceChanged(SecondaryResourceChangeContext context)
    {
        if (context.Definition.Id == ArabellaResources.ExcitementId &&
            ArabellaExcitement.Get(context.Player) < ArabellaExcitement.IndulgedThreshold)
        {
            // Forget the reductions, rather than merely disabling them: reaching 6
            // again must start a new streak. Clear cards in every pile for this owner.
            CardModel[] cards = _indulgedCostReductions.Keys
                .Where(card => card.Owner == context.Player)
                .ToArray();
            foreach (CardModel card in cards)
                _indulgedCostReductions.Remove(card);
            foreach (CardModel card in cards)
                card.InvokeEnergyCostChanged();
        }

        return Task.CompletedTask;
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.EnergyCost.CostsX || originalCost < 0 ||
            !_indulgedCostReductions.TryGetValue(card, out int reduction) ||
            ArabellaExcitement.Get(card.Owner) < ArabellaExcitement.IndulgedThreshold)
        {
            return false;
        }

        // Runs before Excitement's late Lust-for-Energy substitution. Keeping this
        // separate from local cost modifiers preserves other discounts on removal.
        modifiedCost = Math.Max(0m, originalCost - reduction);
        return modifiedCost != originalCost;
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
                _lustSpentThisTurn.Remove(player);
                _drawOnNextLustSpend.Remove(player);
            }
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Character is not ArabellaModCharacter ||
            !cardPlay.IsLastInSeries)
        {
            return Task.CompletedTask;
        }

        int amountSpent = cardPlay.Resources.EnergySpent;
        if (cardPlay.TryGetSecondaryResources(out SecondaryResourcePlayLedger ledger))
            amountSpent += ledger.Spent(ArabellaResources.LustId);

        if (amountSpent > 0)
            _pendingPayments[cardPlay.Card] = amountSpent;

        return Task.CompletedTask;
    }

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        if (oldPileType == PileType.Hand && card.Pile?.Type != PileType.Hand)
            _controlReturnedCards.Remove(card);

        ResetProgressForPileChange(card, oldPileType);

        if (oldPileType != PileType.Play ||
            !_pendingPayments.Remove(card, out int amountSpent))
        {
            return;
        }

        await AccumulateControl(card.Owner, amountSpent, card);
    }

    private void ResetProgressForPileChange(CardModel card, PileType oldPileType)
    {
        if (GetThreshold(card) <= 0 ||
            !card.Keywords.Contains(ArabellaKeywords.Control))
        {
            return;
        }

        if (card.Pile?.Type == PileType.Exhaust && oldPileType != PileType.Exhaust)
        {
            // A newly exhausted Control card starts its own fresh progress track. Its own
            // payment is excluded below; only later payments made by other cards count.
            _controlProgress[card] = 0;
            return;
        }

        if (oldPileType == PileType.Exhaust && card.Pile?.Type != PileType.Exhaust)
        {
            // Once Control retrieves the card, it cannot keep gaining progress in Hand,
            // Draw or Discard. Re-entering Exhaust will create a new zeroed track.
            _controlProgress.Remove(card);
        }
    }

    private async Task AccumulateControl(
        Player player,
        int amountSpent,
        CardModel playedCard)
    {
        CardModel[] cards = PileType.Exhaust
            .GetPile(player)
            .Cards
            .Where(card =>
                card.Owner == player &&
                card != playedCard &&
                card.Keywords.Contains(ArabellaKeywords.Control) &&
                GetThreshold(card) > 0)
            .Distinct()
            .ToArray();

        foreach (CardModel card in cards)
        {
            int threshold = GetThreshold(card);
            int accumulated = Math.Min(
                threshold,
                _controlProgress.GetValueOrDefault(card) + amountSpent);
            _controlProgress[card] = accumulated;

            if (accumulated < threshold)
            {
                continue;
            }

            await ResolveControl(card, new BlockingPlayerChoiceContext());
        }
    }

    private async Task ResolveControl(CardModel card, PlayerChoiceContext choiceContext)
    {
        if (card is IControlCard controlCard)
            await controlCard.OnControl(choiceContext);

        card.Owner.Creature
            .GetPower<ArabellaHighPressureTamingPower>()
            ?.OnControl(card);
        card.Owner.Creature
            .GetPower<ArabellaApproachingLimitPower>()
            ?.OnControl(card);
        if (card.Owner.Creature.GetPower<ArabellaTamingExhibitionPower>() is { } exhibition)
            await exhibition.OnControl(choiceContext);
        if (card.Owner.Creature.GetPower<ArabellaGlobalControlPower>() is { } globalControl)
            await globalControl.OnControl(choiceContext);

        card.EnergyCost.AddThisTurn(-1, reduceOnly: true);

        if (card.Pile?.Type != PileType.Hand)
        {
            // Set before moving so the hand holder sees the glow as it is created.
            _controlReturnedCards.Add(card);
            await CardPileCmd.Add(card, PileType.Hand);
            if (card.Pile?.Type != PileType.Hand)
                _controlReturnedCards.Remove(card);

            if (card.Pile?.Type == PileType.Hand &&
                !card.EnergyCost.CostsX &&
                ArabellaExcitement.Get(card.Owner) >= ArabellaExcitement.IndulgedThreshold)
            {
                _indulgedCostReductions[card] =
                    _indulgedCostReductions.GetValueOrDefault(card) + 1;
            }
        }

        card.InvokeEnergyCostChanged();
    }
}
