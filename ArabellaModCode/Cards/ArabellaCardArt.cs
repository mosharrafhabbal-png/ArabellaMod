using MegaCrit.Sts2.Core.Entities.Cards;

namespace ArabellaMod.Cards;

/// <summary>
/// 临时卡图映射。正式卡图补齐前，所有新卡统一从这里按类型取图。
/// </summary>
internal static class ArabellaCardArt
{
    public const string Attack = $"{Entry.ResPath}/images/cards/start_30115_01.png";
    public const string Skill = $"{Entry.ResPath}/images/cards/start_30115_02.png";
    public const string Power = $"{Entry.ResPath}/images/cards/unique_30115_05.png";
    public const string FirstTaming = $"{Entry.ResPath}/images/cards/unique_30115_05.png";

    public const string ArtOfDeconstruction =
        $"{Entry.ResPath}/images/cards/generated/art_of_deconstruction.png";

    private static readonly IReadOnlyDictionary<Type, string> NumberedPortraits =
        new Dictionary<Type, string>
        {
            [typeof(ArabellaSlitheringSerpent)] = Token("T01"),
            [typeof(ArabellaVipersKiss)] = Token("T02"),
            [typeof(ArabellaRedScale)] = Token("T03"),
            [typeof(ArabellaBackGlance)] = Token("T04"),
            [typeof(ArabellaLacerationEcho)] = Token("T05"),
            [typeof(ArabellaWithdrawal)] = Token("T06"),
            [typeof(ArabellaSerpentBellyThrust)] = Numbered(6),
            [typeof(ArabellaEntanglingSlash)] = Numbered(7),
            [typeof(ArabellaPamperedLash)] = Numbered(8),
            [typeof(ArabellaColdEyeLaceration)] = Numbered(9),
            [typeof(ArabellaRewindingLaceration)] = Numbered(10),
            [typeof(ArabellaLustScarPierce)] = Numbered(11),
            [typeof(ArabellaSerpentShadowSweep)] = Numbered(12),
            [typeof(ArabellaScornfulStrike)] = Numbered(13),
            [typeof(ArabellaWristBind)] = Numbered(14),
            [typeof(ArabellaTailTipCounterstrike)] = Numbered(15),
            [typeof(ArabellaMakeMeScream)] = Numbered(16),
            [typeof(ArabellaOpportunityLash)] = Numbered(17),
            [typeof(ArabellaSinuousStep)] = Numbered(18),
            [typeof(ArabellaReclaimAfterglow)] = Numbered(19),
            [typeof(ArabellaGuidedFootwork)] = Numbered(20),
            [typeof(ArabellaIndulgencePreparation)] = Numbered(21),
            [typeof(ArabellaResidueVeil)] = Numbered(22),
            [typeof(ArabellaLustfireVeil)] = Numbered(23),
            [typeof(ArabellaTameTheSerpent)] = Numbered(24),
            [typeof(ArabellaExtendTheSerpentBlade)] = Numbered(25),
            [typeof(ArabellaLingeringWarmth)] = Numbered(26),
            [typeof(ArabellaWasteNot)] = Numbered(27),
            [typeof(ArabellaCrimsonDance)] = Numbered(28),
            [typeof(ArabellaSerpentKissChain)] = Numbered(29),
            [typeof(ArabellaUntamedBlade)] = Numbered(30),
            [typeof(ArabellaDominatingLash)] = Numbered(31),
            [typeof(ArabellaIndulgentAssault)] = Numbered(32),
            [typeof(ArabellaRepeatedTaste)] = Numbered(33),
            [typeof(ArabellaLustfireScorch)] = Numbered(34),
            [typeof(ArabellaSerpentinePierce)] = Numbered(35),
            [typeof(ArabellaTurningStrangle)] = Numbered(36),
            [typeof(ArabellaUncontrolledThrust)] = Numbered(37),
            [typeof(ArabellaQueensTrial)] = Numbered(38),
            [typeof(ArabellaPlaythingsAfterglow)] = Numbered(39),
            [typeof(ArabellaSerpentBellyReversal)] = Numbered(40),
            [typeof(ArabellaHollowJudgment)] = Numbered(41),
            [typeof(ArabellaLavishRecovery)] = Numbered(42),
            [typeof(ArabellaBurningPassionExchange)] = Numbered(43),
            [typeof(ArabellaIndulgentBarrier)] = Numbered(44),
            [typeof(ArabellaMomentaryLossOfControl)] = Numbered(45),
            [typeof(ArabellaApproachingTheLimit)] = Numbered(46),
            [typeof(ArabellaDominateTheRhythm)] = Numbered(47),
            [typeof(ArabellaCrimsonExchange)] = Numbered(48),
            [typeof(ArabellaDesirePull)] = Numbered(49),
            [typeof(ArabellaCoiledBladeRecovery)] = Numbered(50),
            [typeof(ArabellaSnakeWalk)] = Numbered(51),
            [typeof(ArabellaRepeatedTaming)] = Numbered(52),
            [typeof(ArabellaLustfireCharge)] = Numbered(53),
            [typeof(ArabellaPainTaming)] = Numbered(54),
            [typeof(ArabellaRewindingStep)] = Numbered(55),
            [typeof(ArabellaViperFeint)] = Numbered(56),
            [typeof(ArabellaSpectatorsDelight)] = Numbered(57),
            [typeof(ArabellaMomentumRemains)] = Numbered(58),
            [typeof(ArabellaEscalating)] = Numbered(59),
            [typeof(ArabellaHighPressureTaming)] = Numbered(60),
            [typeof(ArabellaSerpentDanceRhythm)] = Numbered(61),
            [typeof(ArabellaSensoryHeating)] = Numbered(62),
            [typeof(ArabellaTamingExhibition)] = Numbered(63),
            [typeof(ArabellaFilthyPleasure)] = Numbered(64),
            [typeof(ArabellaAbsoluteObedience)] = Numbered(65),
            [typeof(ArabellaFeastFinale)] = Numbered(66),
            [typeof(ArabellaQueensDance)] = Numbered(67),
            [typeof(ArabellaCrimsonExecution)] = Numbered(68),
            [typeof(ArabellaSerpentKissFeast)] = Numbered(69),
            [typeof(ArabellaBacklashStrangle)] = Numbered(70),
            [typeof(ArabellaCrimsonSeverance)] = Numbered(71),
            [typeof(ArabellaNoMercy)] = Numbered(72),
            [typeof(ArabellaSadisticRestraint)] = Numbered(73),
            [typeof(ArabellaArtOfDeconstruction)] = Numbered(74),
            [typeof(ArabellaTotalIndulgence)] = Numbered(75),
            [typeof(ArabellaTurnTheTables)] = Numbered(76),
            [typeof(ArabellaMomentOfClarity)] = Numbered(77),
            [typeof(ArabellaSensoryOverload)] = Numbered(78),
            [typeof(ArabellaTideOfDesire)] = Numbered(79),
            [typeof(ArabellaLustfireFeast)] = Numbered(80),
            [typeof(ArabellaTameEverything)] = Numbered(81),
            [typeof(ArabellaSerpentMolt)] = Numbered(82),
            [typeof(ArabellaEndlessDance)] = Numbered(83),
            [typeof(ArabellaIndulgenceQueen)] = Numbered(84),
            [typeof(ArabellaGlobalControl)] = Numbered(85),
            [typeof(ArabellaDesireDomination)] = Numbered(86),
            [typeof(ArabellaDeadlyFlexibility)] = Numbered(87),
            [typeof(ArabellaDiscardPleasure)] = Numbered(88),
            [typeof(ArabellaCleanPlaythings)] = Numbered(89),
            [typeof(ArabellaPatheticEnd)] = Numbered(90),
            [typeof(ArabellaOuroboros)] = Numbered(91),
            [typeof(ArabellaShedTheWorld)] = Numbered(92),
        };

    public static string Numbered(int number) =>
        $"{Entry.ResPath}/images/cards/{number:000}.png";

    public static string Token(string number) =>
        $"{Entry.ResPath}/images/cards/{number}.png";

    public static string For(Type cardModelType, CardType cardType)
    {
        return NumberedPortraits.GetValueOrDefault(cardModelType) ?? For(cardType);
    }

    public static string For(CardType cardType)
    {
        return cardType switch
        {
            CardType.Attack => Attack,
            CardType.Skill => Skill,
            CardType.Power => Power,
            _ => Skill
        };
    }
}
