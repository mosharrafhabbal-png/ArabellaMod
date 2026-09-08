using HarmonyLib;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Runs;

namespace ArabellaMod.Audio;

/// <summary>
/// Plays an attack-impact sound immediately before Arabella's damage number is created on a monster.
/// </summary>
[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.DamageReceived))]
internal static class ArabellaEnemyHitAudioPatch
{
    private static void Postfix(Creature receiver, Creature? dealer, DamageResult result)
    {
        if (!receiver.IsMonster || result.UnblockedDamage <= 0)
        {
            return;
        }

        if (Powers.ArabellaLacerationDamageContext.IsActiveFor(receiver))
        {
            // CombatHistory.DamageReceived is invoked immediately before NDamageNumVfx is
            // created, so both pieces of feedback land on the same visible impact frame.
            ArabellaAudio.PlayLacerationDamage();
            ArabellaVfx.PlayLacerationDamage(receiver);
            return;
        }

        if (dealer?.Player?.Character is Characters.ArabellaModCharacter)
        {
            // Every damaging hit in a multi-hit attack gets its own synchronized impact sound.
            // The first impact sound is preplayed by the animation controller about 0.5s
            // before damage. Further hits in a multi-hit attack still sound here.
            ArabellaAudio.PlayEnemyHitOnDamage();
        }
    }
}

/// <summary>
/// 普通模式选人界面的语音入口。
/// 原版打开界面时也可能调用一次 SelectCharacter，10 秒冷却会阻止连续点击造成叠音。
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
internal static class ArabellaCharacterSelectVoicePatch
{
    private static void Postfix(CharacterModel characterModel)
    {
        if (characterModel is Characters.ArabellaModCharacter)
        {
            ArabellaAudio.Play(
                ArabellaAudio.Cue.CharacterSelect,
                ArabellaAudio.CharacterSelectCooldownMilliseconds);
        }
    }
}

/// <summary>
/// 自定义模式有独立的选人界面，因此单独接入同一组语音与冷却规则。
/// </summary>
[HarmonyPatch(typeof(NCustomRunScreen), nameof(NCustomRunScreen.SelectCharacter))]
internal static class ArabellaCustomRunCharacterSelectVoicePatch
{
    private static void Postfix(CharacterModel characterModel)
    {
        if (characterModel is Characters.ArabellaModCharacter)
        {
            ArabellaAudio.Play(
                ArabellaAudio.Cue.CharacterSelect,
                ArabellaAudio.CharacterSelectCooldownMilliseconds);
        }
    }
}

/// <summary>
/// 一局游戏被判定为胜利时，为本地艾拉贝拉玩家播放获胜语音。
/// </summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
internal static class ArabellaVictoryVoicePatch
{
    private static void Prefix(RunManager __instance, bool isVictory)
    {
        if (!isVictory)
        {
            return;
        }

        RunState? state = __instance.DebugOnlyGetState();
        if (LocalContext.GetMe(state)?.Character is Characters.ArabellaModCharacter)
        {
            // 较长冷却用于防止结算流程重复进入时叠放同一条胜利语音。
            ArabellaAudio.Play(ArabellaAudio.Cue.Victory, 60_000);
        }
    }
}
