using ArabellaMod.Characters;
using ArabellaMod.Mechanics;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Cards;

/// <summary>
/// Marker shared by all of Arabella's derived cards, including Withdrawal.
/// Derived cards never generate Lust from being exhausted.
/// </summary>
internal interface IArabellaDerivedCard;

/// <summary>
/// Marker for T01-T05, which are derived cards that trigger Sensing when drawn.
/// Recovery effects redirect only this group to the draw pile; Withdrawal keeps
/// its separate status-card pile rules.
/// </summary>
internal interface IArabellaSensingDerivedCard : IArabellaDerivedCard, ISensingCard;

internal static class ArabellaCardLogic
{
    public static bool PlayedThisTurn(CardModel source, CardType type)
    {
        return CombatManager.Instance.History.CardPlaysFinished.Any(entry =>
            entry.HappenedThisTurn(source.CombatState) &&
            entry.Actor == source.Owner.Creature &&
            entry.CardPlay.Card.Type == type);
    }

    public static CardType? PreviousCardType(CardModel source)
    {
        return CombatManager.Instance.History.CardPlaysFinished
            .LastOrDefault(entry =>
                entry.HappenedThisTurn(source.CombatState) &&
                entry.Actor == source.Owner.Creature)
            ?.CardPlay.Card.Type;
    }

    public static CardModel? PreviousFormalCard(CardModel source, bool excludePowers = false)
    {
        return CombatManager.Instance.History.CardPlaysFinished
            .LastOrDefault(entry =>
                entry.HappenedThisTurn(source.CombatState) &&
                entry.Actor == source.Owner.Creature &&
                entry.CardPlay.Card is not IArabellaDerivedCard &&
                (!excludePowers || entry.CardPlay.Card.Type != CardType.Power) &&
                entry.CardPlay.Card.Type is CardType.Attack or CardType.Skill or CardType.Power)
            ?.CardPlay.Card;
    }

    public static bool ExhaustedThisTurn(CardModel source)
    {
        return CombatManager.Instance.History.Entries
            .OfType<CardExhaustedEntry>()
            .Any(entry =>
                entry.HappenedThisTurn(source.CombatState) &&
                entry.Card.Owner == source.Owner);
    }

    public static int DistinctTypesPlayedThisTurn(CardModel source)
    {
        return CombatManager.Instance.History.CardPlaysFinished
            .Where(entry =>
                entry.HappenedThisTurn(source.CombatState) &&
                entry.Actor == source.Owner.Creature)
            .Select(entry => entry.CardPlay.Card.Type)
            .Distinct()
            .Count();
    }

    public static int DerivedCardsAutoPlayedThisCombat(CardModel source)
    {
        return CombatManager.Instance.History.CardPlaysFinished.Count(entry =>
            entry.Actor == source.Owner.Creature &&
            entry.CardPlay.IsAutoPlay &&
            entry.CardPlay.Card is IArabellaDerivedCard);
    }

    public static int ExhaustedCardCount(CardModel source, bool thisTurn)
    {
        return CombatManager.Instance.History.Entries
            .OfType<CardExhaustedEntry>()
            .Count(entry =>
                 entry.Card.Owner == source.Owner &&
                 (!thisTurn || entry.HappenedThisTurn(source.CombatState)));
    }

    public static bool CanGainLustFromExhaust(CardModel card) =>
        card is not IArabellaDerivedCard;

    public static int HarmfulEffectCount(Creature target)
    {
        return target.Powers
            .Where(power =>
                power.Amount != 0 &&
                power.TypeForCurrentAmount == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff)
            .Select(power => power.Id)
            .Distinct()
            .Count();
    }

    public static async Task TriggerLaceration(
        PlayerChoiceContext choiceContext,
        Creature target,
        CardModel source,
        int times = 1)
    {
        for (int i = 0; i < times && target.IsAlive; i++)
        {
            ArabellaLacerationPower? laceration = target.GetPower<ArabellaLacerationPower>();
            if (laceration == null)
                return;

            await laceration.Trigger(choiceContext, source);
        }
    }

    public static Creature? RandomEnemy(CardModel source)
    {
        return source.Owner.RunState.Rng.CombatTargets.NextItem(
            source.CombatState?.HittableEnemies ?? []);
    }

    public static Task ApplyLaceration(
        PlayerChoiceContext choiceContext,
        CardModel source,
        Creature target,
        int amount)
    {
        return PowerCmd.Apply<ArabellaLacerationPower>(
            choiceContext,
            target,
            amount,
            source.Owner.Creature,
            source);
    }

    public static async Task GenerateSerpents(CardModel source, int amount)
    {
        ArabellaDeadlyFlexibilityPower? flexibility =
            source.Owner.Creature.GetPower<ArabellaDeadlyFlexibilityPower>();

        for (int i = 0; i < amount; i++)
        {
            CardModel serpent = flexibility == null
                ? source.CombatState!.CreateCard<ArabellaSlitheringSerpent>(source.Owner)
                : source.CombatState!.CreateCard<ArabellaVipersKiss>(source.Owner);
            bool upgradeDerived = flexibility?.UpgradesVipersKiss == true ||
                                  flexibility == null && source.IsUpgraded;
            if (upgradeDerived)
            {
                serpent.UpgradeInternal();
                serpent.FinalizeUpgradeInternal();
            }
            await CardPileCmd.AddGeneratedCardToCombat(
                serpent,
                PileType.Draw,
                source.Owner);
        }
    }

    public static async Task GenerateWithdrawals(
        CardModel source,
        int amount,
        PileType pile,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        if (pile is not PileType.Draw and not PileType.Discard)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pile),
                pile,
                "Generated cards must enter the Draw or Discard Pile.");
        }

        for (int i = 0; i < amount; i++)
        {
            CardModel withdrawal = source.CombatState!.CreateCard<ArabellaWithdrawal>(source.Owner);
            await CardPileCmd.AddGeneratedCardToCombat(
                withdrawal,
                pile,
                source.Owner,
                position);
        }
    }
}

public abstract class ArabellaRewardCard : ModCardTemplate, IArabellaExcitementCostCard
{
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: ArabellaCardArt.For(GetType(), Type));

    protected ArabellaRewardCard(
        int energyCost,
        CardType type,
        CardRarity rarity,
        TargetType targetType)
        : base(energyCost, type, rarity, targetType, true)
    {
    }
}

[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSlitheringSerpent : ArabellaRewardCard, IArabellaSensingDerivedCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Sensing];

    protected override bool IsPlayable => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(5m, ValueProp.Move), new DamageVar("FollowupDamage", 4m, ValueProp.Move)];

    public ArabellaSlitheringSerpent()
        : base(0, CardType.Attack, CardRarity.Token, TargetType.RandomEnemy)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? target = cardPlay.Target ?? ArabellaCardLogic.RandomEnemy(this);
        if (target == null)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);

        if (target.IsAlive &&
            ArabellaCardLogic.PreviousFormalCard(this)?.Type == CardType.Attack)
        {
            await DamageCmd.Attack(DynamicVars["FollowupDamage"].BaseValue)
                .FromCard(this, cardPlay)
                .Targeting(target)
                .Execute(choiceContext);
        }
    }

    public Task OnSensing()
    {
        return CardCmd.AutoPlay(
            new ThrowingPlayerChoiceContext(),
            this,
            null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["FollowupDamage"].UpgradeValueBy(2m);
    }
}

[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaWithdrawal : ArabellaRewardCard, IArabellaDerivedCard
{
    public override int MaxUpgradeLevel => 0;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Ethereal, CardKeyword.Unplayable];

    public override bool HasTurnEndInHandEffect => true;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaWithdrawalDebtPower>()];

    public ArabellaWithdrawal()
        : base(-1, CardType.Status, CardRarity.Status, TargetType.None)
    {
    }

    protected override async Task OnTurnEndInHand(PlayerChoiceContext choiceContext)
    {
        await SecondaryResourceCmd.Lose(
            Owner,
            ArabellaResources.LustId,
            2,
            this);
        await ArabellaExcitement.Lose(Owner, 2, this);
        await PowerCmd.Apply<ArabellaWithdrawalDebtPower>(
            choiceContext,
            Owner.Creature,
            1,
            Owner.Creature,
            this);
    }
}

// 006 蛇腹突刺
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSerpentBellyThrust : ArabellaRewardCard, ISensingCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Sensing];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move), new PowerVar<VigorPower>(3m)];

    public ArabellaSerpentBellyThrust()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public Task OnSensing() => PowerCmd.Apply<VigorPower>(
        new ThrowingPlayerChoiceContext(), Owner.Creature,
        DynamicVars["VigorPower"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["VigorPower"].UpgradeValueBy(1m);
    }
}

// 007 缠身横斩
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaEntanglingSlash : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move), new BlockVar(4m, ValueProp.Move)];

    public ArabellaEntanglingSlash()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        if (ArabellaCardLogic.PlayedThisTurn(this, CardType.Skill))
            await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}

// 008 骄纵鞭击
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaPamperedLash : ArabellaRewardCard, IIndulgenceCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(10m, ValueProp.Move), new DamageVar("IndulgenceDamage", 7m, ValueProp.Move)];

    public ArabellaPamperedLash()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        Creature? target = ArabellaCardLogic.RandomEnemy(this);
        if (target == null) return;
        await DamageCmd.Attack(DynamicVars["IndulgenceDamage"].BaseValue)
            .FromCard(this, null).Targeting(target).Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["IndulgenceDamage"].UpgradeValueBy(2m);
    }
}

// 009 冷眼割裂
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaColdEyeLaceration : ArabellaRewardCard, ISensingCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Sensing];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(5m, ValueProp.Move), new DynamicVar("Laceration", 2m)];

    public ArabellaColdEyeLaceration()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public async Task OnSensing()
    {
        Creature? target = ArabellaCardLogic.RandomEnemy(this);
        if (target != null)
            await ArabellaCardLogic.ApplyLaceration(new ThrowingPlayerChoiceContext(), this, target, DynamicVars["Laceration"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Laceration"].UpgradeValueBy(1m);
    }
}

// 010 回卷割裂
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaRewindingLaceration : ArabellaRewardCard, IIndulgenceCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(10m, ValueProp.Move),
        new DamageVar("BonusDamage", 6m, ValueProp.Move),
        new DynamicVar("Increase", 3m)
    ];

    public ArabellaRewindingLaceration()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        if (ArabellaCardLogic.ExhaustedThisTurn(this) && cardPlay.Target.IsAlive)
            await DamageCmd.Attack(DynamicVars["BonusDamage"].BaseValue).FromCard(this, cardPlay)
                .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        decimal increase = DynamicVars["Increase"].BaseValue;
        DynamicVars.Damage.BaseValue += increase;
        DynamicVars["BonusDamage"].BaseValue += increase;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["BonusDamage"].UpgradeValueBy(2m);
        DynamicVars["Increase"].UpgradeValueBy(2m);
    }
}

// 011 欲痕穿刺
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaLustScarPierce : ArabellaRewardCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(10m, ValueProp.Move), new DynamicVar("Laceration", 2m)];

    public ArabellaLustScarPierce()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        if (cardPlay.Target.IsAlive)
            await ArabellaCardLogic.ApplyLaceration(choiceContext, this, cardPlay.Target, DynamicVars["Laceration"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["Laceration"].UpgradeValueBy(1m);
    }
}

// 012 蛇影扫掠
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSerpentShadowSweep : ArabellaRewardCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move), new CardsVar(1)];

    public ArabellaSerpentShadowSweep()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool followsAttack = ArabellaCardLogic.PreviousCardType(this) == CardType.Attack;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!).Execute(choiceContext);
        if (followsAttack)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

// 013 轻蔑一击
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaScornfulStrike : ArabellaRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Restless];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(4m, ValueProp.Move), new DamageVar("BonusDamage", 4m, ValueProp.Move)];

    public ArabellaScornfulStrike()
        : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        if (ArabellaExcitement.Get(Owner) >= ArabellaExcitement.RestlessThreshold && cardPlay.Target.IsAlive)
            await DamageCmd.Attack(DynamicVars["BonusDamage"].BaseValue).FromCard(this, cardPlay)
                .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["BonusDamage"].UpgradeValueBy(2m);
    }
}

// 028 猩红乱舞
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaCrimsonDance : ArabellaRewardCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(5m, ValueProp.Move)];

    public ArabellaCrimsonDance()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = 1 + ArabellaCardLogic.DistinctTypesPlayedThisTurn(this);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(hits)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

// 029 蛇吻连环
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSerpentKissChain : ArabellaRewardCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaSlitheringSerpent>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(3m, ValueProp.Move), new DynamicVar("Hits", 2m), new CardsVar(3)];

    public ArabellaSerpentKissChain()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(DynamicVars["Hits"].IntValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);

        // Snapshot and use the seeded combat RNG: no duplicates, generation or immediate sensing.
        CardModel[] selected = PileType.Draw.GetPile(Owner).Cards
            .Where(card => card is IArabellaSensingDerivedCard)
            .ToList().StableShuffle(Owner.RunState.Rng.CombatCardSelection)
            .Take(DynamicVars.Cards.IntValue).ToArray();
        await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

// 030 不驯之刃
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaUntamedBlade : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 7;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaWithdrawal>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(24m, ValueProp.Move), new DynamicVar("Increase", 6m)];

    public ArabellaUntamedBlade()
        : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        await ArabellaCardLogic.GenerateWithdrawals(this, 1, PileType.Discard);
    }

    public Task OnControl(PlayerChoiceContext choiceContext)
    {
        DynamicVars.Damage.BaseValue += DynamicVars["Increase"].BaseValue;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
        DynamicVars["Increase"].UpgradeValueBy(1m);
    }
}

// 031 支配鞭挞
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaDominatingLash : ArabellaRewardCard, IControlCard, ISensingCard
{
    public int ControlThreshold => 6;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control, ArabellaKeywords.Sensing];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(17m, ValueProp.Move), new DamageVar("SensingDamage", 7m, ValueProp.Move)];

    public ArabellaDominatingLash()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public async Task OnSensing()
    {
        Creature? target = ArabellaCardLogic.RandomEnemy(this);
        if (target == null) return;
        await DamageCmd.Attack(DynamicVars["SensingDamage"].BaseValue)
            .FromCard(this, null).Targeting(target)
            .Execute(new ThrowingPlayerChoiceContext());
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
        DynamicVars["SensingDamage"].UpgradeValueBy(2m);
    }
}

// 032 纵欲突袭
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaIndulgentAssault : ArabellaRewardCard, IIndulgenceCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaSlitheringSerpent>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(7m, ValueProp.Move), new DynamicVar("Serpents", 1m), new DynamicVar("IndulgenceSerpents", 3m)];

    public ArabellaIndulgentAssault()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        await ArabellaCardLogic.GenerateSerpents(this, DynamicVars["Serpents"].IntValue);
    }

    public Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal) =>
        ArabellaCardLogic.GenerateSerpents(this, DynamicVars["IndulgenceSerpents"].IntValue);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["IndulgenceSerpents"].UpgradeValueBy(1m);
    }
}

// 033 反复品尝
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaRepeatedTaste : ArabellaRewardCard, IIndulgenceCard, ISensingCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [ArabellaKeywords.Indulgence, ArabellaKeywords.Sensing];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move), new DynamicVar("Lust", 1m), new BlockVar(4m, ValueProp.Move)];

    public ArabellaRepeatedTaste()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public Task OnSensing() => SecondaryResourceCmd.Gain(
        Owner, ArabellaResources.LustId, DynamicVars["Lust"].IntValue, this);

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        await CardPileCmd.Add(this, PileType.Draw, CardPilePosition.Top);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue, DynamicVars.Block.Props, cardPlay: null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}

// 065 绝对服从
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaAbsoluteObedience : ArabellaRewardCard, IControlCard, ISensingCard
{
    public int ControlThreshold => 9;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control, ArabellaKeywords.Sensing];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(27m, ValueProp.Move)];

    public ArabellaAbsoluteObedience()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public async Task OnSensing()
    {
        CardModel? controlCard = PileType.Exhaust.GetPile(Owner).Cards
            .Where(card => card != this && card is IControlCard)
            .OrderByDescending(card => card.EnergyCost.GetAmountToSpend())
            .FirstOrDefault();
        if (controlCard == null) return;
        await CardPileCmd.Add(controlCard, PileType.Hand);
        if (IsUpgraded)
            controlCard.EnergyCost.AddThisTurn(-1, reduceOnly: true);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(7m);
}

// 066 盛宴终幕
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaFeastFinale : ArabellaRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaWithdrawal>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(18m, ValueProp.Move), new DynamicVar("PerLust", 3m), new DynamicVar("Withdrawals", 2m)];

    public ArabellaFeastFinale()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 3);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal damage = DynamicVars.Damage.BaseValue +
                         ControlSingleton.GetLustSpentThisTurn(Owner) * DynamicVars["PerLust"].BaseValue;
        await DamageCmd.Attack(damage).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!).Execute(choiceContext);
        await ArabellaCardLogic.GenerateWithdrawals(
            this, DynamicVars["Withdrawals"].IntValue, PileType.Discard);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
        DynamicVars["PerLust"].UpgradeValueBy(1m);
    }
}

// 067 女王之舞
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaQueensDance : ArabellaRewardCard, ISensingCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Sensing];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaSlitheringSerpent>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(4m, ValueProp.Move), new DynamicVar("Serpents", 1m)];

    public ArabellaQueensDance()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = 1 + SensingSingleton.GetTriggersThisTurn(Owner);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(hits)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public Task OnSensing() => ArabellaCardLogic.GenerateSerpents(this, DynamicVars["Serpents"].IntValue);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Serpents"].UpgradeValueBy(1m);
    }
}

// 068 猩红处刑
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaCrimsonExecution : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 8;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(10m, ValueProp.Move), new DynamicVar("Hits", 3m), new DynamicVar("DrawDebt", 2m)];

    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaDrawDebtPower>()];

    public ArabellaCrimsonExecution()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .WithHitCount(DynamicVars["Hits"].IntValue)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);
        await PowerCmd.Apply<ArabellaDrawDebtPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["DrawDebt"].BaseValue,
            Owner.Creature,
            this);
    }

    public Task OnControl(PlayerChoiceContext choiceContext)
    {
        DynamicVars["Hits"].BaseValue += 1m;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}
