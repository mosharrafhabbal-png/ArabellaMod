using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ArabellaMod.Settings;

namespace ArabellaMod;

/// <summary>
/// Arabella 的逐帧战斗特效与通用场景特效入口。
/// </summary>
public static class ArabellaVfx
{
    private const double SequenceFramesPerSecond = 30.0;
    private const float PowerBuffScaleMultiplier = 1.2f;
    private const float AttackSelfScaleMultiplier = 1.2f;
    private const float LacerationTargetCoverage = 1.15f;
    private const float LacerationMinScale = 0.28f;
    private const float LacerationMaxScale = 0.72f;
    private const float LacerationScaleMultiplier = 1.5f;
    private const float PowerBuffVisibleBottomFromCanvasCenter = 82f;
    // 素材透明画布里的有效像素重心整体偏上；按承载单位高度下移，避免写死像素偏移。
    private sealed record SequenceClip(
        StringName Name,
        string Folder,
        int FrameCount,
        Vector2 LocalOffset = default);

    private static readonly SequenceClip Buff = new(
        "buff",
        $"{Entry.ResPath}/images/texiao/buff",
        34);

    private static readonly SequenceClip Attack1Self = new(
        "attack_1_self",
        $"{Entry.ResPath}/images/texiao/gongjitexiao/attack_play1_eff_self",
        19);

    private static readonly SequenceClip Attack1Self2 = new(
        "attack_1_self_2",
        $"{Entry.ResPath}/images/texiao/gongjitexiao/attack_play1_eff_self2",
        15);

    private static readonly SequenceClip Attack2Self = new(
        "attack_2_self",
        $"{Entry.ResPath}/images/texiao/gongjitexiao/attack_play2_eff_self",
        19);

    private static readonly SequenceClip Attack2Self2 = new(
        "attack_2_self_2",
        $"{Entry.ResPath}/images/texiao/gongjitexiao/attack_play2_eff_self2",
        15);

    private static readonly SequenceClip SingleTarget = new(
        "attack_single_target",
        $"{Entry.ResPath}/images/texiao/guaiwushouji/attack_play1_eff_target",
        18,
        new Vector2(82f, 47f));

    private static readonly SequenceClip LacerationTarget = new(
        "laceration_target",
        $"{Entry.ResPath}/images/texiao/guaiwushouji/attack_play1_eff_target2",
        12);

    private static readonly SequenceClip AllAttackSelf = new(
        "attack_all_self",
        $"{Entry.ResPath}/images/texiao/gongjitexiao/u1_all_attack_play_obk_1",
        24);

    private static readonly SequenceClip AllAttackSelf2 = new(
        "attack_all_self_2",
        $"{Entry.ResPath}/images/texiao/gongjitexiao/u1_all_attack_play_obk_2",
        30);

    private static readonly SequenceClip AllTarget = new(
        "attack_all_target",
        $"{Entry.ResPath}/images/texiao/guaiwushouji/attack_play1_eff_target3",
        25);

    private static readonly SequenceClip[] SequenceClips =
    [
        Buff,
        Attack1Self,
        Attack1Self2,
        Attack2Self,
        Attack2Self2,
        SingleTarget,
        LacerationTarget,
        AllAttackSelf,
        AllAttackSelf2,
        AllTarget
    ];

    private static readonly Dictionary<string, PackedScene> Scenes =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<Node2D> ActiveSequenceEffects = [];

    private static SpriteFrames? _sequenceFrames;

    public static void Initialize()
    {
        RemoveActiveSequenceEffects();
        Scenes.Clear();
        PreloadSequenceFrames();
        Entry.Logger.Info(
            $"Arabella 战斗特效已预加载：{SequenceClips.Sum(clip => clip.FrameCount)} 帧。");
    }

    internal static void PlayPowerBuff(
        NCreature actor,
        AnimatedSprite2D actorSprite)
    {
        if (!CanPlay(actor, actorSprite))
        {
            return;
        }

        Vector2 scale = GetUniformGlobalScale(actorSprite) * PowerBuffScaleMultiplier;
        PlayComposite(
            AlignLocalBottomToCreatureFoot(
                actorSprite.GlobalPosition,
                actor,
                scale,
                PowerBuffVisibleBottomFromCanvasCenter),
            scale,
            Buff);
    }

    internal static void PlaySingleAttack(
        NCreature actor,
        AnimatedSprite2D actorSprite,
        Creature? target,
        bool useAttack1)
    {
        if (!CanPlay(actor, actorSprite))
        {
            return;
        }

        PlayComposite(
            actorSprite.GlobalPosition,
            GetUniformGlobalScale(actorSprite) * AttackSelfScaleMultiplier,
            useAttack1 ? [Attack1Self, Attack1Self2] : [Attack2Self, Attack2Self2]);

        NCreature? targetNode = target == null
            ? null
            : NCombatRoom.Instance?.GetCreatureNode(target);
        if (!GodotObject.IsInstanceValid(targetNode))
        {
            return;
        }

        PlayComposite(
            targetNode.VfxSpawnPosition,
            GetScaleForCreature(actor, actorSprite, targetNode),
            SingleTarget);
    }

    internal static void PlayAllAttack(
        NCreature actor,
        AnimatedSprite2D actorSprite,
        IReadOnlyList<NCreature> enemyNodes,
        Vector2 enemyCenter)
    {
        if (!CanPlay(actor, actorSprite))
        {
            return;
        }

        PlayComposite(
            actorSprite.GlobalPosition,
            GetUniformGlobalScale(actorSprite) * AttackSelfScaleMultiplier,
            AllAttackSelf,
            AllAttackSelf2);

        if (enemyNodes.Count == 0)
        {
            return;
        }

        PlayComposite(
            enemyCenter,
            GetScaleForEnemyGroup(actor, actorSprite, enemyNodes),
            AllTarget);
    }

    internal static void PlayLacerationDamage(Creature target)
    {
        NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
        if (!GodotObject.IsInstanceValid(targetNode) ||
            !GodotObject.IsInstanceValid(targetNode.Hitbox))
        {
            return;
        }

        PlayComposite(
            targetNode.VfxSpawnPosition,
            GetCenteredEffectScale(targetNode, LacerationTarget),
            LacerationTarget);
    }

    internal static void ApplySequenceFrameEffectsEnabled(bool enabled)
    {
        if (!enabled)
        {
            RemoveActiveSequenceEffects();
        }
    }

    private static bool CanPlay(NCreature actor, AnimatedSprite2D actorSprite)
    {
        return ArabellaSettingsPage.SequenceFrameEffectsEnabledBinding.Read() &&
               GodotObject.IsInstanceValid(actor) &&
               GodotObject.IsInstanceValid(actorSprite) &&
               GodotObject.IsInstanceValid(NCombatRoom.Instance);
    }

    private static void PreloadSequenceFrames()
    {
        if (_sequenceFrames != null)
        {
            return;
        }

        SpriteFrames frames = new();
        if (frames.HasAnimation("default"))
        {
            frames.RemoveAnimation("default");
        }

        foreach (SequenceClip clip in SequenceClips)
        {
            frames.AddAnimation(clip.Name);
            frames.SetAnimationLoop(clip.Name, false);
            frames.SetAnimationSpeed(clip.Name, SequenceFramesPerSecond);

            for (int frameIndex = 0; frameIndex < clip.FrameCount; frameIndex++)
            {
                string texturePath = $"{clip.Folder}/frame_30_{frameIndex:D6}.png";
#pragma warning disable RITSU013
                Texture2D? texture = ResourceLoader.Load<Texture2D>(
                    texturePath,
                    cacheMode: ResourceLoader.CacheMode.Reuse);
#pragma warning restore RITSU013
                if (texture == null)
                {
                    Entry.Logger.Warn($"特效帧加载失败：{texturePath}");
                    continue;
                }

                frames.AddFrame(clip.Name, texture);
            }
        }

        _sequenceFrames = frames;
    }

    private static void PlayComposite(
        Vector2 globalPosition,
        Vector2 globalScale,
        params SequenceClip[] clips)
    {
        NCombatRoom? room = NCombatRoom.Instance;
        if (!ArabellaSettingsPage.SequenceFrameEffectsEnabledBinding.Read() ||
            !GodotObject.IsInstanceValid(room) || clips.Length == 0)
        {
            return;
        }

        PreloadSequenceFrames();
        Node2D effect = new()
        {
            Name = $"ArabellaVfx_{clips[0].Name}",
            TopLevel = true,
            ZIndex = 10
        };
        room.CombatVfxContainer.AddChild(effect);
        ActiveSequenceEffects.Add(effect);
        effect.GlobalPosition = globalPosition;
        effect.GlobalScale = globalScale;

        double longestDuration = 0.0;
        foreach (SequenceClip clip in clips)
        {
            AnimatedSprite2D sprite = new()
            {
                Name = clip.Name,
                SpriteFrames = _sequenceFrames,
                Animation = clip.Name,
                Centered = true,
                Position = clip.LocalOffset
            };
            effect.AddChild(sprite);
            sprite.Play(clip.Name);
            longestDuration = Math.Max(
                longestDuration,
                clip.FrameCount / SequenceFramesPerSecond);
        }

        SceneTreeTimer timer = effect.GetTree().CreateTimer(longestDuration + 0.05);
        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(effect))
            {
                effect.QueueFree();
            }

            ActiveSequenceEffects.Remove(effect);
        };
    }

    private static void RemoveActiveSequenceEffects()
    {
        foreach (Node2D effect in ActiveSequenceEffects.ToArray())
        {
            if (GodotObject.IsInstanceValid(effect))
            {
                effect.QueueFree();
            }
        }

        ActiveSequenceEffects.Clear();
    }

    private static Vector2 GetUniformGlobalScale(Node2D node)
    {
        Vector2 scale = node.GlobalScale;
        float uniformScale = Math.Max(Mathf.Abs(scale.X), Mathf.Abs(scale.Y));
        return Vector2.One * Math.Max(uniformScale, 0.001f);
    }

    private static Vector2 AlignLocalBottomToCreatureFoot(
        Vector2 fallbackAnchor,
        NCreature creature,
        Vector2 globalScale,
        float localBottomFromCanvasCenter)
    {
        if (!GodotObject.IsInstanceValid(creature.Hitbox))
        {
            return fallbackAnchor;
        }

        Rect2 hitboxBounds = creature.Hitbox.GetGlobalRect();
        float footY = hitboxBounds.Position.Y + hitboxBounds.Size.Y;
        return new Vector2(
            fallbackAnchor.X,
            footY - localBottomFromCanvasCenter * Mathf.Abs(globalScale.Y));
    }

    private static Vector2 GetScaleForCreature(
        NCreature actor,
        AnimatedSprite2D actorSprite,
        NCreature target)
    {
        float actorScale = GetUniformGlobalScale(actorSprite).X;
        float actorHeight = GetGlobalHitboxSize(actor).Y;
        float targetHeight = GetGlobalHitboxSize(target).Y;
        if (actorHeight <= 0.001f || targetHeight <= 0.001f)
        {
            return Vector2.One * actorScale;
        }

        return Vector2.One * (actorScale * targetHeight / actorHeight);
    }

    private static Vector2 GetScaleForEnemyGroup(
        NCreature actor,
        AnimatedSprite2D actorSprite,
        IReadOnlyList<NCreature> enemyNodes)
    {
        float scale = GetUniformGlobalScale(actorSprite).X;
        foreach (NCreature enemyNode in enemyNodes)
        {
            scale = Math.Max(scale, GetScaleForCreature(actor, actorSprite, enemyNode).X);
        }

        Rect2 bounds = GetCombinedHitboxBounds(enemyNodes);
        Texture2D? texture = _sequenceFrames?.GetFrameTexture(AllTarget.Name, 0);
        if (texture != null && bounds.Size.X > 0f)
        {
            // 群攻受击素材横跨整张透明画布；敌方阵型比画布显示宽度更大时才继续放大。
            scale = Math.Max(scale, bounds.Size.X / texture.GetWidth());
        }

        return Vector2.One * Math.Max(scale, 0.001f);
    }

    private static Vector2 GetCenteredEffectScale(
        NCreature target,
        SequenceClip clip)
    {
        Rect2 bounds = target.Hitbox.GetGlobalRect();
        Texture2D? texture = _sequenceFrames?.GetFrameTexture(clip.Name, 0);
        if (texture == null || bounds.Size.X <= 0f || bounds.Size.Y <= 0f)
        {
            return Vector2.One *
                   (LacerationMinScale * LacerationScaleMultiplier);
        }

        float scaleToWidth = bounds.Size.X / texture.GetWidth();
        float scaleToHeight = bounds.Size.Y / texture.GetHeight();
        float scale = Math.Max(scaleToWidth, scaleToHeight) * LacerationTargetCoverage;
        float adaptiveScale = Mathf.Clamp(
            scale,
            LacerationMinScale,
            LacerationMaxScale);
        return Vector2.One * (adaptiveScale * LacerationScaleMultiplier);
    }

    internal static Rect2 GetCombinedHitboxBounds(IReadOnlyList<NCreature> creatures)
    {
        Rect2 bounds = default;
        bool hasBounds = false;
        foreach (NCreature creature in creatures)
        {
            if (!GodotObject.IsInstanceValid(creature) ||
                !GodotObject.IsInstanceValid(creature.Hitbox))
            {
                continue;
            }

            Rect2 creatureBounds = creature.Hitbox.GetGlobalRect();
            bounds = hasBounds ? bounds.Merge(creatureBounds) : creatureBounds;
            hasBounds = true;
        }

        return bounds;
    }

    private static Vector2 GetGlobalHitboxSize(NCreature creature)
    {
        return GodotObject.IsInstanceValid(creature.Hitbox)
            ? creature.Hitbox.GetGlobalRect().Size
            : Vector2.Zero;
    }

    public static bool RegisterScene(string id, string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new ArgumentException("特效 ID 与资源路径不能为空。");
        }

        PackedScene? scene = ResourceLoader.Load<PackedScene>(
            resourcePath,
            cacheMode: ResourceLoader.CacheMode.Reuse);
        if (scene == null)
        {
            Entry.Logger.Warn($"特效场景加载失败：{resourcePath}");
            return false;
        }

        Scenes[id] = scene;
        return true;
    }

    public static Node2D? PlayAt(
        string id,
        Vector2 globalPosition,
        float lifetimeSeconds = 2f)
    {
        if (NCombatRoom.Instance == null || !Scenes.TryGetValue(id, out PackedScene? scene))
        {
            return null;
        }

        Node2D effect = scene.Instantiate<Node2D>();
        NCombatRoom.Instance.CombatVfxContainer.AddChild(effect);
        effect.GlobalPosition = globalPosition;

        if (lifetimeSeconds > 0f)
        {
            SceneTreeTimer timer = effect.GetTree().CreateTimer(lifetimeSeconds);
            timer.Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(effect))
                {
                    effect.QueueFree();
                }
            };
        }

        return effect;
    }

    public static Node2D? PlayOnCreature(
        string id,
        Creature target,
        Vector2 offset = default,
        float lifetimeSeconds = 2f)
    {
        if (NCombatRoom.Instance == null)
        {
            return null;
        }

        NCreature? targetNode = NCombatRoom.Instance.GetCreatureNode(target);
        return targetNode == null
            ? null
            : PlayAt(id, targetNode.VfxSpawnPosition + offset, lifetimeSeconds);
    }

    public static Node2D? PlayFullScreen(string id, float lifetimeSeconds = 2f)
    {
        if (NCombatRoom.Instance == null)
        {
            return null;
        }

        Vector2 center = NCombatRoom.Instance.GetViewportRect().Size / 2f;
        return PlayAt(id, center, lifetimeSeconds);
    }
}
