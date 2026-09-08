using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace ArabellaMod.Animation;

/// <summary>
/// 管理 NCreature 与动画控制器之间的一对一关系。
/// ConditionalWeakTable 不会阻止战斗人物在离开房间后被回收。
/// </summary>
internal static class ArabellaAnimationSystem
{
    private static readonly ConditionalWeakTable<NCreature, ArabellaAnimationController> Controllers = new();

    public static ArabellaAnimationController? GetOrCreate(NCreature creature)
    {
        if (!IsArabella(creature))
        {
            return null;
        }

        if (Controllers.TryGetValue(creature, out ArabellaAnimationController? existing))
        {
            return existing;
        }

        AnimatedSprite2D? sprite = FindAnimatedSprite(creature.Visuals);
        if (sprite == null)
        {
            Entry.Logger.Warn("找不到 Arabella 战斗场景中的 AnimatedSprite2D 节点 %Visuals。");
            return null;
        }

        // 控制器同时保留 NCreature，攻击时才能像 YukiMod 一样移动整个人物节点，
        // 而不是只移动贴图导致 hitbox、特效锚点和人物脱节。
        ArabellaAnimationController controller = new(creature, sprite);
        Controllers.Add(creature, controller);
        ArabellaExcitementAura.Attach(creature, sprite);
        return controller;
    }

    private static bool IsArabella(NCreature creature)
    {
        return creature.Entity?.Player?.Character is Characters.ArabellaModCharacter;
    }

    private static AnimatedSprite2D? FindAnimatedSprite(Node node)
    {
        if (node is AnimatedSprite2D sprite)
        {
            return sprite;
        }

        foreach (Node child in node.GetChildren())
        {
            AnimatedSprite2D? result = FindAnimatedSprite(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
