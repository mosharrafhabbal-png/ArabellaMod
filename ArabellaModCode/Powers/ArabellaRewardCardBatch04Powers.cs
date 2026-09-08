using ArabellaMod.Cards;
using ArabellaMod.Mechanics;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ArabellaMod.Powers;

public abstract class ArabellaUpgradeableRewardPower : ArabellaRewardPower
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Upgraded", 0m)];

    protected bool HasUpgradedStack => DynamicVars["Upgraded"].BaseValue > 0m;

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this && amount > 0m && cardSource?.IsUpgraded == true)
            DynamicVars["Upgraded"].BaseValue = 1m;

        return Task.CompletedTask;
    }
}

[RegisterPower]
public sealed class ArabellaPainTamingPower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1601111_atkult_scale.png";

    public bool PreventsExcitementLoss => true;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (HasUpgradedStack && side == CombatSide.Player && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!HasUpgradedStack && side == CombatSide.Player && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class ArabellaSpectatorsDelightPower : ArabellaRewardPower
{
    protected override string FileName => "icon_1600641_atkult_scale.png";

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (target?.IsMonster != true || dealer != Owner ||
            props.HasFlag(ValueProp.Unpowered))
        {
            return amount;
        }

        return amount + ArabellaCardLogic.HarmfulEffectCount(target) * Amount;
    }

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class ArabellaTamingExhibitionPower : ArabellaRewardPower
{
    protected override string FileName => "icon_1502351_chain_scale.png";

    public async Task OnControl(PlayerChoiceContext choiceContext)
    {
        if (CombatState.HittableEnemies.Count == 0)
            return;

        Creature? target = Owner.Player!.RunState.Rng.CombatTargets.NextItem(
            CombatState.HittableEnemies);
        if (target == null)
            return;

        Flash();
        await CreatureCmd.Damage(
            choiceContext,
            target,
            Amount,
            ValueProp.Unpowered,
            Owner);
    }
}

[RegisterPower]
public sealed class ArabellaFilthyPleasurePower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1601431_chain_scale.png";

    private sealed class Data
    {
        public bool TriggeredThisTurn;
    }

    protected override object InitInternalData() => new Data();

    public override Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player == Owner.Player)
            GetInternalData<Data>().TriggeredThisTurn = false;

        return Task.CompletedTask;
    }

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        Data data = GetInternalData<Data>();
        if (card.Owner.Creature != Owner ||
            card is IArabellaDerivedCard ||
            data.TriggeredThisTurn)
            return;

        data.TriggeredThisTurn = true;
        if (PileType.Hand.GetPile(card.Owner).Cards.Count == 0)
            return;

        CardSelectorPrefs prefs = new(
            ModelDb.Card<ArabellaFilthyPleasure>().SelectionScreenPrompt,
            0,
            1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        CardModel? selected = (await CardSelectCmd.FromHand(
            choiceContext,
            card.Owner,
            prefs,
            filter: null,
            source: this)).FirstOrDefault();
        if (selected == null)
            return;

        Flash();
        await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Bottom);
        await CardPileCmd.Draw(choiceContext, HasUpgradedStack ? 3 : 2, card.Owner);
    }
}

[RegisterPower]
public sealed class ArabellaEndlessDancePower : ArabellaRewardPower
{
    protected override string FileName => "icon_1400811_zhuangbei_scale.png";
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<VigorPower>(0m)];

    public override Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext,
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (power == this && amount > 0m && cardSource is ArabellaEndlessDance)
            DynamicVars["VigorPower"].BaseValue += amount * cardSource.DynamicVars["VigorPower"].BaseValue;

        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner.Player)
            return;

        Flash();
        ArabellaDeadlyFlexibilityPower? flexibility = Owner.GetPower<ArabellaDeadlyFlexibilityPower>();
        for (int i = 0; i < Amount; i++)
        {
            CardModel serpent = flexibility == null
                ? combatState.CreateCard<ArabellaSlitheringSerpent>(player)
                : combatState.CreateCard<ArabellaVipersKiss>(player);
            if (flexibility?.UpgradesVipersKiss == true)
            {
                serpent.UpgradeInternal();
                serpent.FinalizeUpgradeInternal();
            }

            await CardPileCmd.AddGeneratedCardToCombat(serpent, PileType.Draw, player, CardPilePosition.Top);
        }
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature != Owner ||
            !cardPlay.IsLastInSeries ||
            !cardPlay.IsAutoPlay ||
            cardPlay.Card is not IArabellaDerivedCard)
        {
            return;
        }

        Flash();
        await PowerCmd.Apply<VigorPower>(
            choiceContext,
            Owner,
            DynamicVars["VigorPower"].BaseValue,
            Owner,
            cardPlay.Card);
    }
}

[RegisterPower]
public sealed class ArabellaIndulgenceQueenPower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1601811_zhuangbei_scale.png";

    public async Task OnIndulgence(PlayerChoiceContext choiceContext)
    {
        CardModel? drawn = await CardPileCmd.Draw(choiceContext, Owner.Player!);
        if (drawn != null && drawn.Pile?.Type == PileType.Hand)
            drawn.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);

        if (HasUpgradedStack)
        {
            await CreatureCmd.GainBlock(
                Owner,
                2m,
                ValueProp.Unpowered,
                cardPlay: null);
        }
    }
}

[RegisterPower]
public sealed class ArabellaGlobalControlPower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1601031_zhuangbei_scale.png";

    public async Task OnControl(PlayerChoiceContext choiceContext)
    {
        List<CardModel> options = PileType.Discard.GetPile(Owner.Player!).Cards.ToList();
        if (HasUpgradedStack)
            options.AddRange(PileType.Draw.GetPile(Owner.Player!).Cards);
        options = options.Distinct().ToList();
        if (options.Count == 0)
            return;

        CardModel? selected;
        if (HasUpgradedStack)
        {
            selected = (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                options,
                Owner.Player!,
                new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, 1)))
                .FirstOrDefault();
        }
        else
        {
            selected = options
                .Where(card => card.Pile?.Type == PileType.Discard)
                .OrderByDescending(card => card.EnergyCost.GetWithModifiers(CostModifiers.All))
                .FirstOrDefault();
        }

        if (selected == null)
            return;

        Flash();
        await CardPileCmd.Add(selected, PileType.Hand);
        selected.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);
    }
}

[RegisterPower]
public sealed class ArabellaDesireDominationPower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1601031_chain_scale.png";

    public async Task OnLustSpent(PlayerChoiceContext choiceContext)
    {
        List<CardModel> options = PileType.Hand.GetPile(Owner.Player!).Cards.ToList();
        if (options.Count == 0)
            return;

        CardModel? selected;
        if (HasUpgradedStack)
        {
            selected = (await CardSelectCmd.FromHand(
                choiceContext,
                Owner.Player!,
                new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, 1),
                filter: null,
                source: this)).FirstOrDefault();
        }
        else
        {
            selected = Owner.Player!.RunState.Rng.Niche.NextItem(options);
        }

        if (selected == null)
            return;

        selected.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);
        if (selected.EnergyCost.GetWithModifiers(CostModifiers.All) == 0)
            await CardPileCmd.Draw(choiceContext, 1, Owner.Player!);
    }
}

[RegisterPower]
public sealed class ArabellaDeadlyFlexibilityPower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1501711_zhuangbei_scale.png";

    public bool UpgradesVipersKiss => HasUpgradedStack;
}

[RegisterPower]
public sealed class ArabellaDiscardPleasurePower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1601111_zhuangbei_scale.png";

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card.Owner.Creature != Owner)
            return;

        Flash();
        CardModel? drawn = await CardPileCmd.Draw(choiceContext, card.Owner);
        if (HasUpgradedStack)
        {
            if (drawn != null)
                await CardPileCmd.Draw(choiceContext, 1, card.Owner);
            return;
        }

        if (PileType.Hand.GetPile(card.Owner).Cards.Count == 0)
            return;

        IEnumerable<CardModel> discard = await CardSelectCmd.FromHandForDiscard(
            choiceContext,
            card.Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
            filter: null,
            source: this);
        await CardCmd.Discard(choiceContext, discard);
    }
}

[RegisterPower]
public sealed class ArabellaCleanPlaythingsPower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1501831_zhuangbei_scale.png";

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner.Player)
            return;

        IEnumerable<CardModel> candidates = PileType.Hand.GetPile(player).Cards
            .Where(IsIntrinsicallyUnplayable);
        CardModel[] cards = (HasUpgradedStack ? candidates : candidates.Take(3)).ToArray();
        foreach (CardModel card in cards)
            await CardCmd.Exhaust(choiceContext, card);
    }

    private static bool IsIntrinsicallyUnplayable(CardModel card)
    {
        card.CanPlay(out UnplayableReason reason, out _);
        return reason.HasFlag(UnplayableReason.HasUnplayableKeyword) ||
               reason.HasFlag(UnplayableReason.BlockedByCardLogic);
    }
}

[RegisterPower]
public sealed class ArabellaPatheticEndPower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1602381_zhuangbei_scale.png";

    private sealed class Data
    {
        public int EnemyDamage;
        public int LacerationDamage;
        public bool Resolving;
    }

    protected override object InitInternalData() => new Data();

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        Data data = GetInternalData<Data>();
        if (data.Resolving || result.UnblockedDamage <= 0)
            return Task.CompletedTask;

        if (target == Owner && dealer?.IsMonster == true)
        {
            data.EnemyDamage += result.UnblockedDamage;
        }
        else if (HasUpgradedStack && target.IsMonster && dealer == Owner &&
                 props.HasFlag(ValueProp.Unblockable) &&
                 props.HasFlag(ValueProp.Unpowered) &&
                 target.GetPower<ArabellaLacerationPower>() != null)
        {
            data.LacerationDamage += result.UnblockedDamage;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner))
            return;

        Data data = GetInternalData<Data>();
        int damage = (data.EnemyDamage + data.LacerationDamage) * Amount;
        data.EnemyDamage = 0;
        data.LacerationDamage = 0;
        if (damage <= 0)
            return;

        data.Resolving = true;
        try
        {
            foreach (Creature enemy in CombatState.HittableEnemies.ToArray())
            {
                await CreatureCmd.Damage(
                    choiceContext,
                    enemy,
                    damage,
                    ValueProp.Unblockable | ValueProp.Unpowered,
                    Owner);
                if (enemy.IsAlive)
                    await ArabellaCardLogic.TriggerLaceration(choiceContext, enemy, null!);
            }
        }
        finally
        {
            data.Resolving = false;
        }
    }
}

[RegisterPower]
public sealed class ArabellaOuroborosPower : ArabellaUpgradeableRewardPower
{
    protected override string FileName => "icon_1601811_lingge_scale.png";

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card.Owner.Creature != Owner)
            return;

        CardModel? drawn = await CardPileCmd.Draw(choiceContext, card.Owner);
        if (drawn == null)
            return;

        if (HasUpgradedStack && drawn.Pile?.Type == PileType.Hand)
            drawn.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);
    }

    public override async Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        if (card.Owner.Creature != Owner ||
            oldPileType != PileType.Exhaust ||
            card.Pile?.Type == PileType.Exhaust)
        {
            return;
        }

        await ArabellaExcitement.Gain(card.Owner, 1, this);
    }
}
