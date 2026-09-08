using ArabellaMod.Characters;
using ArabellaMod.Mechanics;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
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

// 014 绞腕
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaWristBind : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(7m, ValueProp.Move), new BlockVar(3m, ValueProp.Move)];

    public ArabellaWristBind()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(1m);
    }
}

// 015 尾锋回刺
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaTailTipCounterstrike : ArabellaRewardCard, ISensingCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Sensing];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaSlitheringSerpent>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(4m, ValueProp.Move), new DynamicVar("Serpents", 1m)];

    public ArabellaTailTipCounterstrike()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public Task OnSensing() =>
        ArabellaCardLogic.GenerateSerpents(this, DynamicVars["Serpents"].IntValue);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Serpents"].UpgradeValueBy(1m);
    }
}

// 016 为我尖叫
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaMakeMeScream : ArabellaRewardCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Laceration", 1m),
        new DynamicVar("PriorityLaceration", 3m)
    ];

    public ArabellaMakeMeScream()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature[] enemies = CombatState!.HittableEnemies.ToArray();
        Creature? highestHp = enemies.MaxBy(enemy => enemy.CurrentHp);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState).Execute(choiceContext);

        foreach (Creature enemy in enemies.Where(enemy => enemy.IsAlive))
        {
            await ArabellaCardLogic.ApplyLaceration(
                choiceContext, this, enemy, DynamicVars["Laceration"].IntValue);
        }

        if (highestHp?.IsAlive == true)
        {
            await ArabellaCardLogic.ApplyLaceration(
                choiceContext, this, highestHp, DynamicVars["PriorityLaceration"].IntValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Laceration"].UpgradeValueBy(1m);
        DynamicVars["PriorityLaceration"].UpgradeValueBy(1m);
    }
}

// 017 趁隙抽打
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaOpportunityLash : ArabellaRewardCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(8m, ValueProp.Move)];

    public ArabellaOpportunityLash()
        : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        await ArabellaCardLogic.TriggerLaceration(choiceContext, cardPlay.Target, this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

// 018 蜿蜒步
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSinuousStep : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(7m, ValueProp.Move), new CardsVar(1)];

    public ArabellaSinuousStep()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool followsAttack = ArabellaCardLogic.PreviousCardType(this) == CardType.Attack;
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        if (followsAttack)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}

// 019 回收余兴
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaReclaimAfterglow : ArabellaRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public ArabellaReclaimAfterglow()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> options = PileType.Discard.GetPile(Owner).Cards
            .Where(IsEligible)
            .ToList();

        if (IsUpgraded)
        {
            options.AddRange(PileType.Exhaust.GetPile(Owner).Cards.Where(IsEligible));
        }

        CardModel? selected = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            options,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();

        if (selected == null)
            return;

        await CardPileCmd.Add(selected, PileType.Hand);
        selected.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);
    }

    private static bool IsEligible(CardModel card) => card.Rarity != CardRarity.Basic;
}

// 020 诱导步法
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaGuidedFootwork : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(7m, ValueProp.Move)];

    public ArabellaGuidedFootwork()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        CardModel? selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            filter: null,
            source: this)).FirstOrDefault();

        if (selected == null)
            return;

        await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
        if (IsUpgraded)
            selected.EnergyCost.AddUntilPlayed(-1, reduceOnly: true);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

// 021 放纵准备
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaIndulgencePreparation : ArabellaRewardCard, IIndulgenceCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];

    public ArabellaIndulgencePreparation()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
            filter: null,
            source: this)).FirstOrDefault();

        if (selected != null)
            await CardCmd.Exhaust(choiceContext, selected);
    }

    public Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal) =>
        CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

// 022 残渣护幕
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaResidueVeil : ArabellaRewardCard, IIndulgenceCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<BlurPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(6m, ValueProp.Move), new DynamicVar("PerCard", 3m), new DynamicVar("Blur", 1m)];

    public ArabellaResidueVeil()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int exhausted = ArabellaCardLogic.ExhaustedCardCount(this, thisTurn: true);
        decimal block = DynamicVars.Block.BaseValue + exhausted * DynamicVars["PerCard"].BaseValue;
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
    }

    public Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal) =>
        PowerCmd.Apply<BlurPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Blur"].BaseValue,
            Owner.Creature,
            this);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["PerCard"].UpgradeValueBy(1m);
        DynamicVars["Blur"].UpgradeValueBy(1m);
    }
}

// 023 欲火护幕
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaLustfireVeil : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 7;
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(14m, ValueProp.Move), new PowerVar<ArabellaLustfireVeilPower>(2m)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>(), HoverTipFactory.FromPower<ArabellaLustfireVeilPower>()];

    public ArabellaLustfireVeil()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<ArabellaLustfireVeilPower>(choiceContext, Owner.Creature,
            DynamicVars["ArabellaLustfireVeilPower"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
        DynamicVars["ArabellaLustfireVeilPower"].UpgradeValueBy(1m);
    }
}

// 034 欲火灼痕
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaLustfireScorch : ArabellaRewardCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(18m, ValueProp.Move),
        new DynamicVar("Laceration", 4m),
        new DynamicVar("ExcitementLoss", 4m)
    ];

    public ArabellaLustfireScorch()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        if (cardPlay.Target.IsAlive)
        {
            await ArabellaCardLogic.ApplyLaceration(
                choiceContext, this, cardPlay.Target, DynamicVars["Laceration"].IntValue);
            await ArabellaCardLogic.TriggerLaceration(choiceContext, cardPlay.Target, this);
        }
        await ArabellaExcitement.Lose(Owner, DynamicVars["ExcitementLoss"].IntValue, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
        DynamicVars["Laceration"].UpgradeValueBy(2m);
    }
}

// 035 蛇形贯穿
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSerpentinePierce : ArabellaRewardCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(11m, ValueProp.Move), new CardsVar(1)];

    public ArabellaSerpentinePierce()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        CardType? previousType = ArabellaCardLogic.PreviousCardType(this);
        CardType? nextHandType = PileType.Hand.GetPile(Owner).Cards.FirstOrDefault()?.Type;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);

        if (previousType.HasValue && nextHandType.HasValue && previousType != nextHandType)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

// 036 回身绞杀
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaTurningStrangle : ArabellaRewardCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move)];

    public ArabellaTurningStrangle()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        CardModel? previous = ArabellaCardLogic.PreviousFormalCard(this, excludePowers: true);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);

        if (previous?.Pile?.IsCombatPile == true && previous.Pile.Type != PileType.Play)
        {
            await CardPileCmd.Add(previous, PileType.Hand);
            previous.EnergyCost.AddThisTurnOrUntilPlayed(1);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2m);
}

// 037 失控突刺
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaUncontrolledThrust : ArabellaRewardCard, IIndulgenceCard, IControlCard
{
    public int ControlThreshold => 5;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Indulgence, ArabellaKeywords.Control];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(18m, ValueProp.Move), new DamageVar("IndulgenceDamage", 15m, ValueProp.Move)];

    public ArabellaUncontrolledThrust()
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

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        Creature? target = CombatState?.HittableEnemies.MinBy(enemy => enemy.CurrentHp);
        if (target == null)
            return;
        await DamageCmd.Attack(DynamicVars["IndulgenceDamage"].BaseValue)
            .FromCard(this, null).Targeting(target).Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
        DynamicVars["IndulgenceDamage"].UpgradeValueBy(5m);
    }
}

// 038 女王试刀
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaQueensTrial : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 7;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(13m, ValueProp.Move)];

    public ArabellaQueensTrial()
        : base(3, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool playedSkill = ArabellaCardLogic.PlayedThisTurn(this, CardType.Skill);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!).Execute(choiceContext);

        if (!playedSkill)
            return;

        foreach (Creature enemy in CombatState!.HittableEnemies.ToArray())
            await ArabellaCardLogic.TriggerLaceration(choiceContext, enemy, this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4m);
}

// 039 玩物余温
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaPlaythingsAfterglow : ArabellaRewardCard, IIndulgenceCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(7m, ValueProp.Move), new DynamicVar("PerCard", 2m)];

    public ArabellaPlaythingsAfterglow()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int exhausted = ArabellaCardLogic.ExhaustedCardCount(this, thisTurn: false);
        decimal damage = DynamicVars.Damage.BaseValue + exhausted * DynamicVars["PerCard"].BaseValue;
        await DamageCmd.Attack(damage).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        List<CardModel> attacks = PileType.Discard.GetPile(Owner).Cards
            .Where(card => card.Type == CardType.Attack)
            .ToList();
        if (attacks.Count == 0)
            return;

        CardModel? selected = IsUpgraded
            ? (await CardSelectCmd.FromSimpleGrid(
                choiceContext,
                attacks,
                Owner,
                new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault()
            : Owner.RunState.Rng.CombatCardSelection.NextItem(attacks);

        if (selected != null)
            await CardCmd.AutoPlay(choiceContext, selected, target: null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["PerCard"].UpgradeValueBy(1m);
    }
}

// 040 蛇腹回旋
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSerpentBellyReversal : ArabellaRewardCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move)];

    public ArabellaSerpentBellyReversal()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!).Execute(choiceContext);

        CardModel? selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Discard.GetPile(Owner),
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            card => card.Type == CardType.Skill)).FirstOrDefault();

        if (selected == null)
            return;

        await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
        if (IsUpgraded)
            selected.EnergyCost.AddUntilPlayed(-1, reduceOnly: true);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

// 041 空洞裁决
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaHollowJudgment : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 7;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(7m, ValueProp.Move), new DynamicVar("Laceration", 2m)];

    public ArabellaHollowJudgment()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature[] enemies = CombatState!.HittableEnemies.ToArray();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState).Execute(choiceContext);

        foreach (Creature enemy in enemies.Where(enemy => enemy.IsAlive))
        {
            await ArabellaCardLogic.ApplyLaceration(
                choiceContext, this, enemy, DynamicVars["Laceration"].IntValue);
            await ArabellaCardLogic.TriggerLaceration(choiceContext, enemy, this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["Laceration"].UpgradeValueBy(1m);
    }
}

// 042 奢靡回收
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaLavishRecovery : ArabellaRewardCard, IIndulgenceCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Lust", 3m), new CardsVar(3)];

    public ArabellaLavishRecovery()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? sacrifice = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
            filter: card => card != this && card is not IArabellaDerivedCard,
            source: this)).FirstOrDefault();
        if (sacrifice == null)
            return;

        await CardCmd.Exhaust(choiceContext, sacrifice);
        CardModel[] selected = (await CardSelectCmd.FromCombatPile(
            choiceContext, PileType.Exhaust.GetPile(Owner), Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 0, DynamicVars.Cards.IntValue),
            card => card is IArabellaSensingDerivedCard)).ToArray();
        await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Random);
    }

    public Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal) =>
        SecondaryResourceCmd.Gain(Owner, ArabellaResources.LustId, DynamicVars["Lust"].IntValue, this);

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(2m);
}

// 043 焚情换息
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaBurningPassionExchange : ArabellaRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new EnergyVar(2), new CardsVar(3), new DynamicVar("ExcitementLoss", 3m)];

    public ArabellaBurningPassionExchange()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        await ArabellaExcitement.Lose(Owner, DynamicVars["ExcitementLoss"].IntValue, this);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

// T05 裂创余响
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaLacerationEcho : ArabellaRewardCard, IArabellaSensingDerivedCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Sensing];

    protected override bool IsPlayable => false;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(3m, ValueProp.Move), new DynamicVar("Laceration", 1m)];

    public ArabellaLacerationEcho()
        : base(0, CardType.Attack, CardRarity.Token, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature[] enemies = CombatState!.HittableEnemies.ToArray();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState).Execute(choiceContext);
        foreach (Creature enemy in enemies.Where(enemy => enemy.IsAlive))
        {
            await ArabellaCardLogic.ApplyLaceration(
                choiceContext, this, enemy, DynamicVars["Laceration"].IntValue);
        }
    }

    public Task OnSensing() =>
        CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), this, null);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2m);
        DynamicVars["Laceration"].UpgradeValueBy(1m);
    }
}

// 069 蛇吻盛宴
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSerpentKissFeast : ArabellaRewardCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaSlitheringSerpent>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6m, ValueProp.Move), new DynamicVar("PerDerived", 2m)];

    public ArabellaSerpentKissFeast()
        : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal damage = DynamicVars.Damage.BaseValue +
                         ArabellaCardLogic.DerivedCardsAutoPlayedThisCombat(this) *
                         DynamicVars["PerDerived"].BaseValue;
        await DamageCmd.Attack(damage).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["PerDerived"].UpgradeValueBy(1m);
    }
}

// 070 反噬绞杀
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaBacklashStrangle : ArabellaRewardCard, IIndulgenceCard, IControlCard
{
    public int ControlThreshold => 8;
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [ArabellaKeywords.Indulgence, ArabellaKeywords.Control];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(12m, ValueProp.Move), new DamageVar("PerExhaust", 6m, ValueProp.Move)];

    public ArabellaBacklashStrangle()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        // Preserve the pre-hit wounds even when this attack kills its target.
        int laceration = cardPlay.Target.GetPower<ArabellaLacerationPower>()?.Amount ?? 0;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
        if (laceration > 0)
            await CreatureCmd.GainBlock(Owner.Creature, laceration, ValueProp.Move, cardPlay);
    }

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        int exhausted = ArabellaCardLogic.ExhaustedCardCount(this, thisTurn: true);
        decimal damage = exhausted * DynamicVars["PerExhaust"].BaseValue;
        if (damage <= 0)
            return;
        await DamageCmd.Attack(damage).FromCard(this, null)
            .TargetingAllOpponents(CombatState!).Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars["PerExhaust"].UpgradeValueBy(2m);
    }
}

// 071 绯红断章
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaCrimsonSeverance : ArabellaRewardCard, IControlCard, ISensingCard
{
    public int ControlThreshold => 8;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control, ArabellaKeywords.Sensing];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaLacerationEcho>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(18m, ValueProp.Move)];

    public ArabellaCrimsonSeverance()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
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
        CardModel echo = CombatState!.CreateCard<ArabellaLacerationEcho>(Owner);
        if (IsUpgraded)
        {
            echo.UpgradeInternal();
            echo.FinalizeUpgradeInternal();
        }
        await CardPileCmd.AddGeneratedCardToCombat(echo, PileType.Draw, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5m);
}

// 072 无慈悲
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaNoMercy : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 10;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(36m, ValueProp.Move), new DynamicVar("PerDebuff", 7m)];

    public ArabellaNoMercy()
        : base(4, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal damage = DynamicVars.Damage.BaseValue +
                         ArabellaCardLogic.HarmfulEffectCount(cardPlay.Target) *
                         DynamicVars["PerDebuff"].BaseValue;
        await DamageCmd.Attack(damage).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(8m);
        DynamicVars["PerDebuff"].UpgradeValueBy(2m);
    }
}

// 073 施虐克制
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSadisticRestraint : ArabellaRewardCard
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(7m, ValueProp.Move), new DynamicVar("LacerationTriggers", 2m)];

    public ArabellaSadisticRestraint()
        : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = 1 + ArabellaCardLogic.HarmfulEffectCount(cardPlay.Target);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).WithHitCount(hits)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute(choiceContext);
        await ArabellaCardLogic.TriggerLaceration(
            choiceContext, cardPlay.Target, this, DynamicVars["LacerationTriggers"].IntValue);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

// 074 解构艺术
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaArtOfDeconstruction : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 10;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<ArabellaLacerationPower>(),
        HoverTipFactory.FromPower<ArabellaLacerationAmplificationPower>(),
        HoverTipFactory.FromCard<ArabellaWithdrawal>()
    ];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(16m, ValueProp.Move),
        new DynamicVar("PerDebuff", 5m),
        new DynamicVar("Withdrawals", 2m)
    ];

    public ArabellaArtOfDeconstruction()
        : base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        bool hasAmplification = Owner.Creature
            .GetPower<ArabellaLacerationAmplificationPower>()?.Amount > 0;

        foreach (Creature enemy in CombatState!.HittableEnemies.ToArray())
        {
            decimal baseDamage = DynamicVars.Damage.BaseValue * (hasAmplification ? 2m : 1m);
            decimal damage = baseDamage +
                             ArabellaCardLogic.HarmfulEffectCount(enemy) *
                             DynamicVars["PerDebuff"].BaseValue;
            await DamageCmd.Attack(damage).FromCard(this, cardPlay)
                .Targeting(enemy).Execute(choiceContext);
        }

        await ArabellaCardLogic.GenerateWithdrawals(
            this,
            DynamicVars["Withdrawals"].IntValue,
            PileType.Discard);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars["PerDebuff"].UpgradeValueBy(2m);
    }
}
