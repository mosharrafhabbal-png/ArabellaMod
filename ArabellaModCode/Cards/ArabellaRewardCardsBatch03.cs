using ArabellaMod.Characters;
using ArabellaMod.Mechanics;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace ArabellaMod.Cards;

internal static class ArabellaBatch03Logic
{
    public static async Task GenerateBackGlances(
        CardModel source,
        int amount,
        CardPilePosition position = CardPilePosition.Bottom)
    {
        for (int i = 0; i < amount; i++)
        {
            CardModel card = source.CombatState!.CreateCard<ArabellaBackGlance>(source.Owner);
            if (source.IsUpgraded)
            {
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
            }

            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, source.Owner, position);
        }
    }
}

// T04 回眸
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaBackGlance : ArabellaRewardCard, IArabellaSensingDerivedCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Sensing];

    protected override bool IsPlayable => false;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar(1), new PowerVar<VigorPower>(2m)];

    public ArabellaBackGlance()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        await PowerCmd.Apply<VigorPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["VigorPower"].BaseValue,
            Owner.Creature,
            this);
    }

    public Task OnSensing() => CardCmd.AutoPlay(
        new ThrowingPlayerChoiceContext(),
        this,
        null);

    protected override void OnUpgrade() =>
        DynamicVars["VigorPower"].UpgradeValueBy(1m);
}

// 024 驯蛇
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaTameTheSerpent : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 6;
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(12m, ValueProp.Move)];

    public ArabellaTameTheSerpent()
        : base(2, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4m);
}

// 025 舒展蛇刃
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaExtendTheSerpentBlade : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaSlitheringSerpent>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(4m, ValueProp.Move), new DynamicVar("Serpents", 2m)];

    public ArabellaExtendTheSerpentBlade()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await ArabellaCardLogic.GenerateSerpents(this, DynamicVars["Serpents"].IntValue);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["Serpents"].UpgradeValueBy(1m);
    }
}

// 026 余温未散
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaLingeringWarmth : ArabellaRewardCard, IIndulgenceCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(5m, ValueProp.Move), new CardsVar(2), new DynamicVar("CostReduction", 2m)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<DrawCardsNextTurnPower>()];

    public ArabellaLingeringWarmth()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Owner.Creature,
            DynamicVars.Cards.BaseValue, Owner.Creature, this);
    }

    public Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        CardModel? card = PileType.Hand.GetPile(Owner).Cards
            .Where(candidate => !candidate.EnergyCost.CostsX)
            .OrderByDescending(candidate => candidate.EnergyCost.GetWithModifiers(CostModifiers.Local))
            .FirstOrDefault(candidate => candidate.EnergyCost.GetWithModifiers(CostModifiers.Local) > 0);

        card?.EnergyCost.AddThisTurnOrUntilPlayed(
            -DynamicVars["CostReduction"].IntValue,
            reduceOnly: true);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["CostReduction"].UpgradeValueBy(1m);
    }
}

// 027 废物利用
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaWasteNot : ArabellaRewardCard, IIndulgenceCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(12m, ValueProp.Move), new CardsVar(1)];

    public ArabellaWasteNot()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

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

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        CardModel[] selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Discard.GetPile(Owner),
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, DynamicVars.Cards.IntValue)))
            .ToArray();

        foreach (CardModel card in selected)
            await CardCmd.Exhaust(choiceContext, card);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

// 044 纵情屏障
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaIndulgentBarrier : ArabellaRewardCard, IIndulgenceCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(9m, ValueProp.Move), new DynamicVar("IndulgenceBlock", 15m)];

    public ArabellaIndulgentBarrier()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    public Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal) =>
        CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars["IndulgenceBlock"].BaseValue,
            ValueProp.Unpowered,
            cardPlay: null);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["IndulgenceBlock"].UpgradeValueBy(3m);
    }
}

// 045 失控一瞬
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaMomentaryLossOfControl : ArabellaRewardCard, IIndulgenceCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new EnergyVar(1), new CardsVar(1)];

    public ArabellaMomentaryLossOfControl()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
            filter: null,
            source: this)).FirstOrDefault();
        if (selected == null)
            return;

        bool hasControl = ControlSingleton.GetThreshold(selected) > 0;
        await CardCmd.Exhaust(choiceContext, selected);
        if (!hasControl)
            return;

        await ControlSingleton.TriggerControlNow(selected);
        if (IsUpgraded)
            selected.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);
    }

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }
}

// 046 逼近极限
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaApproachingTheLimit : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 5;
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaApproachingLimitPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(10m, ValueProp.Move), new PowerVar<ArabellaApproachingLimitPower>(1m)];

    public ArabellaApproachingTheLimit()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<ArabellaApproachingLimitPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ArabellaApproachingLimitPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4m);
}

// 047 支配节奏
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaDominateTheRhythm : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 5;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Increase", 2m), new CardsVar(0)];

    public ArabellaDominateTheRhythm()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            filter: null,
            source: this)).FirstOrDefault();
        if (selected == null)
            return;

        if (selected.EnergyCost.Canonical >= 0 &&
            selected.EnergyCost.GetWithModifiers(CostModifiers.Local) < 2)
        {
            selected.EnergyCost.SetThisCombat(2);
        }

        ControlSingleton.GrantControl(selected, 5);
        if (selected.DynamicVars.TryGetValue("Damage", out DynamicVar? damage))
            damage.BaseValue += DynamicVars["Increase"].BaseValue;
        if (selected.DynamicVars.TryGetValue("Block", out DynamicVar? block))
            block.BaseValue += DynamicVars["Increase"].BaseValue;

        if (DynamicVars.Cards.IntValue > 0)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Increase"].UpgradeValueBy(3m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

// 048 绯红换手
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaCrimsonExchange : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar(2), new BlockVar(6m, ValueProp.Move)];

    public ArabellaCrimsonExchange()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

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

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

        if (PileType.Hand.GetPile(Owner).Cards.Count > 0)
        {
            IEnumerable<CardModel> discard = await CardSelectCmd.FromHandForDiscard(
                choiceContext,
                Owner,
                new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 1),
                filter: null,
                source: this);
            await CardCmd.Discard(choiceContext, discard);
        }

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1m);
        DynamicVars.Block.UpgradeValueBy(2m);
    }
}

// 049 欲念牵引
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaDesirePull : ArabellaRewardCard, ISensingCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Sensing];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaBackGlance>()];

    public ArabellaDesirePull()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public Task OnSensing() => ArabellaBatch03Logic.GenerateBackGlances(this, 1);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Draw.GetPile(Owner),
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
        if (selected != null)
            await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// 050 卷刃回收
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaCoiledBladeRecovery : ArabellaRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar(0)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaCoiledBladeRecoveryPower>()];

    public ArabellaCoiledBladeRecovery()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> options = PileType.Discard.GetPile(Owner).Cards
            .Where(card => card.Type == CardType.Attack && card is not IArabellaDerivedCard)
            .ToList();

        CardModel? selected = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            options,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
        if (selected == null)
            return;

        var power = await PowerCmd.Apply<ArabellaCoiledBladeRecoveryPower>(
            choiceContext, Owner.Creature, 1m, Owner.Creature, this);
        power?.Prepare(selected);
        await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
        if (IsUpgraded)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1m);
}

// 051 蛇行
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSnakeWalk : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaSnakeWalkPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(5m, ValueProp.Move), new PowerVar<ArabellaSnakeWalkPower>(2m)];

    public ArabellaSnakeWalk()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<ArabellaSnakeWalkPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ArabellaSnakeWalkPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["ArabellaSnakeWalkPower"].UpgradeValueBy(1m);
    }
}

// 052 反复驯化
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaRepeatedTaming : ArabellaRewardCard, IControlCard
{
    public int ControlThreshold => 8;
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(15m, ValueProp.Move), new DynamicVar("Increase", 4m)];

    public ArabellaRepeatedTaming()
        : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    public Task OnControl(PlayerChoiceContext choiceContext)
    {
        DynamicVars.Block.BaseValue += DynamicVars["Increase"].BaseValue;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(5m);
        DynamicVars["Increase"].UpgradeValueBy(1m);
    }
}

// 053 欲火蓄势
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaLustfireCharge : ArabellaRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Lust", 2m)];

    public ArabellaLustfireCharge()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await SecondaryResourceCmd.Gain(
            Owner,
            ArabellaResources.LustId,
            DynamicVars["Lust"].IntValue,
            this);
        ControlSingleton.DrawOnNextLustSpend(Owner);
    }

    protected override void OnUpgrade() => DynamicVars["Lust"].UpgradeValueBy(1m);
}

// 075 完全放纵
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaTotalIndulgence : ArabellaRewardCard, IIndulgenceCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3m, ValueProp.Move),
        new CardsVar(2),
        new DynamicVar("IndulgenceLust", 3m)
    ];

    public ArabellaTotalIndulgence()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        List<CardModel> selected = (await CardSelectCmd.FromHand(
            choiceContext,
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 0, 999999999),
            filter: null,
            source: this)).ToList();

        foreach (CardModel card in selected)
            await CardCmd.Exhaust(choiceContext, card);

        if (selected.Count > 0)
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                selected.Count * DynamicVars.Block.BaseValue,
                ValueProp.Move,
                cardPlay);
        }

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    public Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal) =>
        SecondaryResourceCmd.Gain(
            Owner,
            ArabellaResources.LustId,
            DynamicVars["IndulgenceLust"].IntValue,
            this);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
        DynamicVars["IndulgenceLust"].UpgradeValueBy(1m);
    }
}

// 076 反客为主
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaTurnTheTables : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(1m, ValueProp.Move), new CardsVar(0)];

    public ArabellaTurnTheTables()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel[] cards = PileType.Exhaust.GetPile(Owner).Cards
            .Where(card => card.Type is not (CardType.Status or CardType.Curse))
            .ToArray();

        foreach (CardModel card in cards)
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Random);

        if (cards.Length > 0)
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                cards.Length * DynamicVars.Block.BaseValue,
                ValueProp.Move,
                cardPlay);
        }

        if (DynamicVars.Cards.IntValue > 0)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

// 077 刹那清醒
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaMomentOfClarity : ArabellaRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("PerExcitement", 3m), new EnergyVar(1), new CardsVar(1)];

    public ArabellaMomentOfClarity()
        : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int excitement = ArabellaExcitement.Get(Owner);
        int triggers = excitement / DynamicVars["PerExcitement"].IntValue;
        await ArabellaExcitement.Lose(Owner, excitement, this);
        if (triggers <= 0)
            return;

        await PlayerCmd.GainEnergy(triggers * DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(
            choiceContext,
            triggers * DynamicVars.Cards.IntValue,
            Owner);
    }

    protected override void OnUpgrade() =>
        DynamicVars["PerExcitement"].UpgradeValueBy(-1m);
}

// 078 感官过载
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSensoryOverload : ArabellaRewardCard, ISensingCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Sensing];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaBackGlance>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(8m, ValueProp.Move), new DynamicVar("BackGlances", 1m)];

    public ArabellaSensoryOverload()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    public Task OnSensing()
    {
        CardPilePosition position = ArabellaExcitement.Get(Owner) >= ArabellaExcitement.IndulgedThreshold
            ? CardPilePosition.Top
            : CardPilePosition.Random;
        return ArabellaBatch03Logic.GenerateBackGlances(
            this,
            DynamicVars["BackGlances"].IntValue,
            position);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    protected override void OnUpgrade() =>
        DynamicVars["BackGlances"].UpgradeValueBy(1m);
}

// 079 欲念回潮
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaTideOfDesire : ArabellaRewardCard, IIndulgenceCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(5m, ValueProp.Move), new DynamicVar("IndulgenceBlock", 5m)];

    public ArabellaTideOfDesire()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal block = ControlSingleton.GetLustSpentThisCombat(Owner) +
                        DynamicVars.Block.BaseValue;
        return CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
    }

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        int refund = ControlSingleton.GetLustSpentThisTurn(Owner);
        if (refund > 0)
        {
            await SecondaryResourceCmd.Gain(
                Owner,
                ArabellaResources.LustId,
                refund,
                this);
        }

        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars["IndulgenceBlock"].BaseValue,
            ValueProp.Unpowered,
            cardPlay: null);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(7m);
        DynamicVars["IndulgenceBlock"].UpgradeValueBy(3m);
    }
}

// 080 欲火狂宴
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaLustfireFeast : ArabellaRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaEnergyDebtPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar(3),
        new CardsVar(4),
        new PowerVar<ArabellaEnergyDebtPower>(1m)
    ];

    public ArabellaLustfireFeast()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 4);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);

        CardModel? selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Discard.GetPile(Owner),
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
        if (selected != null)
        {
            await CardPileCmd.Add(selected, PileType.Hand);
            if (IsUpgraded)
                selected.EnergyCost.AddThisTurnOrUntilPlayed(-1, reduceOnly: true);
        }

        await PowerCmd.Apply<ArabellaEnergyDebtPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ArabellaEnergyDebtPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}
