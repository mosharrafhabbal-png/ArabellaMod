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

namespace ArabellaMod.Cards;

internal static class ArabellaBatch04Logic
{
    public static async Task GenerateRedScales(CardModel source, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            CardModel card = source.CombatState!.CreateCard<ArabellaRedScale>(source.Owner);
            if (source.IsUpgraded)
            {
                card.UpgradeInternal();
                card.FinalizeUpgradeInternal();
            }

            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, source.Owner);
        }
    }
}

// T02 蝮蛇之吻
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaVipersKiss : ArabellaRewardCard, IArabellaSensingDerivedCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Sensing];
    protected override bool IsPlayable => false;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaLacerationPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(8m, ValueProp.Move), new DynamicVar("Laceration", 1m)];

    public ArabellaVipersKiss()
        : base(0, CardType.Attack, CardRarity.Token, TargetType.RandomEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Creature? target = CombatState?.HittableEnemies.MinBy(enemy => enemy.CurrentHp);
        if (target == null)
            return;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .Execute(choiceContext);
        if (target.IsAlive)
        {
            await ArabellaCardLogic.ApplyLaceration(
                choiceContext,
                this,
                target,
                DynamicVars["Laceration"].IntValue);
        }
    }

    public Task OnSensing() => CardCmd.AutoPlay(
        new ThrowingPlayerChoiceContext(),
        this,
        null);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4m);
        DynamicVars["Laceration"].UpgradeValueBy(2m);
    }
}

// T03 红鳞
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaRedScale : ArabellaRewardCard, IArabellaSensingDerivedCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Sensing];
    protected override bool IsPlayable => false;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(6m, ValueProp.Move)];

    public ArabellaRedScale()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.Self) { }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    public Task OnSensing() => CardCmd.AutoPlay(
        new ThrowingPlayerChoiceContext(),
        this,
        null);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2m);
}

// 054 痛感驯化
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaPainTaming : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaPainTamingPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(6m, ValueProp.Move), new PowerVar<ArabellaPainTamingPower>(1m)];

    public ArabellaPainTaming()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<ArabellaPainTamingPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ArabellaPainTamingPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3m);
}

// 055 回卷步
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaRewindingStep : ArabellaRewardCard, IIndulgenceCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(7m, ValueProp.Move), new CardsVar(0)];

    public ArabellaRewindingStep()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        CardModel? selected = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            PileType.Discard.GetPile(Owner),
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            card => card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0))
            .FirstOrDefault();
        if (selected == null)
            return;

        await CardPileCmd.Add(selected, PileType.Hand);
        if (IsUpgraded)
            await CardPileCmd.Draw(choiceContext, 1, Owner);
    }

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        CardModel[] cards = PileType.Discard.GetPile(Owner).Cards
            .Where(card => card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0)
            .ToArray();
        foreach (CardModel card in cards)
        {
            await CardPileCmd.Add(
                card,
                PileType.Draw,
                IsUpgraded ? CardPilePosition.Top : CardPilePosition.Random);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

// 056 毒蛇假步
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaViperFeint : ArabellaRewardCard, ISensingCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [ArabellaKeywords.Sensing];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromCard<ArabellaRedScale>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(4m, ValueProp.Move), new DynamicVar("RedScales", 1m)];

    public ArabellaViperFeint()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 1);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    public Task OnSensing() =>
        ArabellaBatch04Logic.GenerateRedScales(this, DynamicVars["RedScales"].IntValue);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2m);
        DynamicVars["RedScales"].UpgradeValueBy(1m);
    }
}

// 057 旁观者之悦
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSpectatorsDelight : ArabellaRewardCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
    [
        HoverTipFactory.FromPower<ArabellaLacerationAmplificationPower>(),
        HoverTipFactory.FromPower<ArabellaSpectatorsDelightPower>()
    ];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<ArabellaLacerationAmplificationPower>(1m),
        new PowerVar<ArabellaSpectatorsDelightPower>(2m)
    ];

    public ArabellaSpectatorsDelight()
        : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<ArabellaLacerationAmplificationPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ArabellaLacerationAmplificationPower"].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<ArabellaSpectatorsDelightPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ArabellaSpectatorsDelightPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ArabellaLacerationAmplificationPower"].UpgradeValueBy(1m);
        DynamicVars["ArabellaSpectatorsDelightPower"].UpgradeValueBy(1m);
    }
}

public abstract class ArabellaBatch04PowerCard<TPower> : ArabellaRewardCard
    where TPower : PowerModel
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<TPower>()];

    protected ArabellaBatch04PowerCard(int cost, CardRarity rarity)
        : base(cost, CardType.Power, rarity, TargetType.Self) { }

    protected Task ApplyPower(
        PlayerChoiceContext choiceContext,
        decimal amount) =>
        PowerCmd.Apply<TPower>(
            choiceContext,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
}

// 063 驯化展演
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaTamingExhibition : ArabellaBatch04PowerCard<ArabellaTamingExhibitionPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaTamingExhibitionPower>(4m)];
    public ArabellaTamingExhibition() : base(2, CardRarity.Uncommon) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaTamingExhibitionPower"].BaseValue);
    protected override void OnUpgrade() =>
        DynamicVars["ArabellaTamingExhibitionPower"].UpgradeValueBy(2m);
}

// 064 污秽取悦
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaFilthyPleasure : ArabellaBatch04PowerCard<ArabellaFilthyPleasurePower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaFilthyPleasurePower>(1m)];
    public ArabellaFilthyPleasure() : base(1, CardRarity.Uncommon) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaFilthyPleasurePower"].BaseValue);
    protected override void OnUpgrade() { }
}

// 081 驯服一切
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaTameEverything : ArabellaRewardCard, IControlCard, ISensingCard, IIndulgenceCard
{
    public int ControlThreshold => 9;
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [CardKeyword.Exhaust, ArabellaKeywords.Control, ArabellaKeywords.Sensing, ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(28m, ValueProp.Move), new CardsVar(1), new DynamicVar("CostReduction", 1m)];

    public ArabellaTameEverything()
        : base(3, CardType.Skill, CardRarity.Rare, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 2);
    }

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);

    public Task OnSensing() => CardPileCmd.Draw(
        new ThrowingPlayerChoiceContext(),
        DynamicVars.Cards.IntValue,
        Owner);

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        List<CardModel> options = PileType.Exhaust.GetPile(Owner).Cards
            .Where(card => ControlSingleton.GetThreshold(card) > 0)
            .ToList();
        CardModel? selected = (await CardSelectCmd.FromSimpleGrid(
            choiceContext,
            options,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
        if (selected == null)
            return;

        await CardPileCmd.Add(selected, PileType.Hand);
        selected.EnergyCost.AddThisTurnOrUntilPlayed(
            -DynamicVars["CostReduction"].IntValue,
            reduceOnly: true);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(7m);
        DynamicVars.Cards.UpgradeValueBy(1m);
        DynamicVars["CostReduction"].UpgradeValueBy(1m);
    }
}

// 082 蛇蜕
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaSerpentMolt : ArabellaRewardCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(2m, ValueProp.Move), new CardsVar(0)];

    public ArabellaSerpentMolt()
        : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel[] cards = PileType.Hand.GetPile(Owner).Cards.ToArray();
        foreach (CardModel card in cards)
            await CardCmd.Exhaust(choiceContext, card);

        if (cards.Length > 0)
        {
            await CreatureCmd.GainBlock(
                Owner.Creature,
                cards.Length * DynamicVars.Block.BaseValue,
                ValueProp.Move,
                cardPlay);
        }

        await CardPileCmd.Draw(
            choiceContext,
            cards.Length + DynamicVars.Cards.IntValue,
            Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(1m);
        DynamicVars.Cards.UpgradeValueBy(1m);
    }
}

// 083 无休之舞
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaEndlessDance : ArabellaBatch04PowerCard<ArabellaEndlessDancePower>
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaEndlessDancePower>(),
         HoverTipFactory.FromCard<ArabellaSlitheringSerpent>(),
         HoverTipFactory.FromPower<VigorPower>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaEndlessDancePower>(1m), new PowerVar<VigorPower>(2m)];
    public ArabellaEndlessDance() : base(2, CardRarity.Rare) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaEndlessDancePower"].BaseValue);
    protected override void OnUpgrade() => DynamicVars["VigorPower"].UpgradeValueBy(1m);
}

// 084 放纵女王
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaIndulgenceQueen : ArabellaBatch04PowerCard<ArabellaIndulgenceQueenPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaIndulgenceQueenPower>(1m)];
    public ArabellaIndulgenceQueen() : base(2, CardRarity.Rare) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaIndulgenceQueenPower"].BaseValue);
    protected override void OnUpgrade() { }
}

// 085 全域掌控
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaGlobalControl : ArabellaBatch04PowerCard<ArabellaGlobalControlPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaGlobalControlPower>(1m)];
    public ArabellaGlobalControl() : base(3, CardRarity.Rare) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaGlobalControlPower"].BaseValue);
    protected override void OnUpgrade() { }
}

// 086 欲望支配
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaDesireDomination : ArabellaBatch04PowerCard<ArabellaDesireDominationPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaDesireDominationPower>(1m)];
    public ArabellaDesireDomination() : base(2, CardRarity.Rare) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaDesireDominationPower"].BaseValue);
    protected override void OnUpgrade() { }
}

// 087 致命柔韧
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaDeadlyFlexibility : ArabellaBatch04PowerCard<ArabellaDeadlyFlexibilityPower>
{
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [HoverTipFactory.FromPower<ArabellaDeadlyFlexibilityPower>(), HoverTipFactory.FromCard<ArabellaVipersKiss>()];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaDeadlyFlexibilityPower>(1m)];
    public ArabellaDeadlyFlexibility() : base(2, CardRarity.Rare) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaDeadlyFlexibilityPower"].BaseValue);
    protected override void OnUpgrade() { }
}

// 088 弃置快感
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaDiscardPleasure : ArabellaBatch04PowerCard<ArabellaDiscardPleasurePower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaDiscardPleasurePower>(1m)];
    public ArabellaDiscardPleasure() : base(2, CardRarity.Rare) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaDiscardPleasurePower"].BaseValue);
    protected override void OnUpgrade() { }
}

// 089 清理玩物
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaCleanPlaythings : ArabellaBatch04PowerCard<ArabellaCleanPlaythingsPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaCleanPlaythingsPower>(1m)];
    public ArabellaCleanPlaythings() : base(2, CardRarity.Rare) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaCleanPlaythingsPower"].BaseValue);
    protected override void OnUpgrade() { }
}

// 090 可悲终局
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaPatheticEnd : ArabellaBatch04PowerCard<ArabellaPatheticEndPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaPatheticEndPower>(1m)];
    public ArabellaPatheticEnd() : base(3, CardRarity.Rare) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaPatheticEndPower"].BaseValue);
    protected override void OnUpgrade() { }
}

// 091 衔尾蛇
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaOuroboros : ArabellaBatch04PowerCard<ArabellaOuroborosPower>
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<ArabellaOuroborosPower>(1m)];
    public ArabellaOuroboros() : base(2, CardRarity.Ancient) { }
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        ApplyPower(choiceContext, DynamicVars["ArabellaOuroborosPower"].BaseValue);
    protected override void OnUpgrade() { }
}

// 092 蜕世
[RegisterCard(typeof(ArabellaModCardPool))]
public sealed class ArabellaShedTheWorld : ArabellaRewardCard, ISensingCard, IIndulgenceCard
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [ArabellaKeywords.Sensing, ArabellaKeywords.Indulgence];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new CardsVar(5), new EnergyVar(1), new BlockVar(8m, ValueProp.Move)];

    public ArabellaShedTheWorld()
        : base(2, CardType.Skill, CardRarity.Ancient, TargetType.Self)
    {
        this.SecondaryCosts().Set(ArabellaResources.LustId, 3);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        CardModel[] cards = PileType.Discard.GetPile(Owner).Cards
            .Concat(PileType.Exhaust.GetPile(Owner).Cards)
            .Distinct()
            .ToArray();
        foreach (CardModel card in cards)
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Random);

        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
    }

    public Task OnSensing() =>
        PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);

    public async Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal)
    {
        if (Pile?.Type == PileType.Exhaust)
        {
            await CardPileCmd.Add(this, PileType.Hand);
            GiveSingleTurnRetain();
        }

        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay: null);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
        DynamicVars.Cards.UpgradeValueBy(2m);
        DynamicVars.Block.UpgradeValueBy(4m);
    }
}
