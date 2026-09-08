using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ArabellaMod.Animation;

/// <summary>
/// 用动画的真实命中事件替代原版固定延迟。
/// 原版在快速模式下会把 AttackAnimDelay 压缩到最多 0.25 秒，比艾拉贝拉的位移还早。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim),
    [typeof(Creature), typeof(string), typeof(float)])]
internal static class ArabellaAttackImpactTimingPatch
{
    private static bool Prefix(Creature creature, string triggerName, ref Task __result)
    {
        if (!string.Equals(triggerName, "Attack", StringComparison.Ordinal) ||
            creature.Player?.Character is not Characters.ArabellaModCharacter ||
            NCombatRoom.Instance == null)
        {
            return true;
        }

        NCreature? creatureNode = NCombatRoom.Instance.GetCreatureNode(creature);
        ArabellaAnimationController? controller = creatureNode == null
            ? null
            : ArabellaAnimationSystem.GetOrCreate(creatureNode);
        if (controller == null)
        {
            // 场景节点尚未就绪时保留原版逻辑，避免攻击命令被卡住。
            return true;
        }

        __result = controller.PlayAttackAndWaitForImpact(out bool startedNewAttack);
        if (startedNewAttack)
        {
            ArabellaAudio.Play(ArabellaAudio.Cue.Attack);
        }

        return false;
    }
}

/// <summary>
/// 接收原版战斗人物的标准动画触发名。
/// 结构参考卡莉佩与蕾欧娜模组，但实际播放交给可维护的序列控制器。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class ArabellaCreatureAnimationTriggerPatch
{
    private static void Postfix(NCreature __instance, string trigger)
    {
        ArabellaAnimationController? controller = ArabellaAnimationSystem.GetOrCreate(__instance);
        if (controller == null)
        {
            return;
        }

        switch (trigger.Trim().ToLowerInvariant())
        {
            case "attack":
                if (controller.PlayAttack()) ArabellaAudio.Play(ArabellaAudio.Cue.Attack);
                break;
            case "cast":
                if (controller.PlaySkill()) ArabellaAudio.Play(ArabellaAudio.Cue.Skill);
                break;
            case "hit":
            case "hurt":
                if (controller.PlayHit()) ArabellaAudio.Play(ArabellaAudio.Cue.Hit);
                break;
            case "dead":
            case "death":
            case "die":
                if (controller.PlayDeath()) ArabellaAudio.Play(ArabellaAudio.Cue.Death);
                break;
        }
    }
}

/// <summary>
/// 人物节点完成初始化时立刻创建控制器，从进入战斗的第一帧开始播放待机动画。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class ArabellaCreatureReadyPatch
{
    private static void Postfix(NCreature __instance)
    {
        ArabellaAnimationSystem.GetOrCreate(__instance);
    }
}

/// <summary>
/// 非 Spine 人物不会收到原版死亡 Trigger，因此在死亡流程入口补发一次死亡动画。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class ArabellaCreatureDeathPatch
{
    private static void Prefix(NCreature __instance)
    {
        ArabellaAnimationController? controller = ArabellaAnimationSystem.GetOrCreate(__instance);
        if (controller?.PlayDeath() == true)
        {
            ArabellaAudio.Play(ArabellaAudio.Cue.Death);
        }
    }
}

/// <summary>
/// 技能牌和能力牌不一定调用 CreatureCmd.TriggerAnim，因此在统一出牌入口按卡牌类型触发动画。
/// 攻击牌仍由实际 AttackCommand 触发，以便动作尽量贴近伤害发生时机。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class ArabellaCardTypeAnimationPatch
{
    private static void Prefix(CardModel __instance, Creature? target)
    {
        if (__instance.Owner?.Character is not Characters.ArabellaModCharacter ||
            NCombatRoom.Instance == null)
        {
            return;
        }

        NCreature? creature = NCombatRoom.Instance.GetCreatureNode(__instance.Owner.Creature);
        if (creature == null)
        {
            return;
        }

        ArabellaAnimationController? controller = ArabellaAnimationSystem.GetOrCreate(creature);
        switch (__instance.Type)
        {
            case CardType.Attack:
                // 单体攻击使用玩家选中的敌人；群体/无指定目标时选取第一个存活敌人。
                // 这个目标只保留到下一次 Attack Trigger，避免后续攻击误用旧目标。
                bool targetsAllEnemies = __instance.TargetType == TargetType.AllEnemies;
                Creature? movementTarget = targetsAllEnemies
                    ? null
                    : target ?? __instance.Owner.Creature.CombatState?.HittableEnemies
                        .FirstOrDefault(enemy => enemy.IsAlive);
                controller?.SetAttackTarget(movementTarget, targetsAllEnemies);
                break;
            case CardType.Skill when controller?.PlaySkill() == true:
                ArabellaAudio.Play(ArabellaAudio.Cue.Skill);
                break;
            case CardType.Power when controller?.PlayPower() == true:
                ArabellaAudio.Play(ArabellaAudio.Cue.Power);
                break;
        }
    }
}
