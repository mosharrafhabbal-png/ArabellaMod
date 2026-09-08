using Godot;

namespace ArabellaMod.Animation;

/// <summary>
/// 集中保存 Arabella 的逐帧动画路径与帧数。
/// 后续替换素材时优先维护此文件，避免动画控制器中散落硬编码路径。
/// </summary>
internal static class ArabellaAnimationAssets
{
    // 原素材命名中的 30 表示导出帧率；实际播放提高到 45 FPS，使动作更利落。
    // 如果后续觉得过快，只需要调整这一处以及场景脚本的默认值。
    public const double FramesPerSecond = 45.0;

    // 伤害点位于：转换 21 + 准备 6 + 主体攻击第 4 帧，共 31 帧。
    // 此值同时供动画控制器缺失时的原版延迟逻辑作为安全回退。
    public const int AttackFramesBeforeImpact = 31;
    public const float AttackDamageDelaySeconds =
        AttackFramesBeforeImpact / (float)FramesPerSecond;

    // Start the attack SFX about half a second before damage resolves. At 45 FPS this is
    // 23 frames before impact, i.e. frame 8 of the 21-frame attack transition.
    public const int AttackSoundLeadFrames = 23;
    public const int AttackSoundTriggerFrame =
        AttackFramesBeforeImpact - AttackSoundLeadFrames;

    // 冲刺在主体攻击开始前已经完成；播放到第 4 帧时放行伤害结算。
    // 相比之前在攻击结束动画倒数第 4 帧触发，整体约提前 0.5 秒。
    public const int AttackEndFrameCount = 10;
    public const int AttackImpactFrame = 4;

    private const string DonghuaRoot =
        $"{Entry.ResPath}/images/characters/donghua";
    private const string Root = $"{DonghuaRoot}/xiaorendonghua";

    internal sealed record Clip(
        StringName Name,
        string Folder,
        int FrameCount,
        bool Loop = false,
        double? PlaybackFps = null);

    // 战斗动画名称。名称仅供代码内部使用，不与原素材文件夹名称绑定。
    public static readonly StringName IdleName = "idle";
    public static readonly StringName TransitionName = "idle_to_b_idle";
    public static readonly StringName ReturnTransitionName = "b_idle_to_idle";
    public static readonly StringName AttackReadyName = "attack_ready";
    public static readonly StringName AttackAName = "attack_a";
    public static readonly StringName AttackBName = "attack_b";
    public static readonly StringName AttackEndName = "attack_end";
    public static readonly StringName SkillReadyName = "skill_ready";
    public static readonly StringName SkillReleaseName = "skill_release";
    public static readonly StringName PowerReadyName = "power_ready";
    public static readonly StringName PowerReleaseName = "power_release";
    public static readonly StringName HitName = "hit";
    public static readonly StringName DeathName = "death";

    /// <summary>
    /// 本轮已确认用途的全部战斗动画。帧数与文件夹中的 000000 起始编号严格对应。
    /// </summary>
    public static IReadOnlyList<Clip> CombatClips { get; } =
    [
        new(IdleName, $"{Root}/daiji/30115_260826093614_979208_30", 81, Loop: true),
        new(TransitionName, $"{Root}/gongji/30115_260826094123_25dd7d_30", 21),
        // 新素材本身就是 B 待机回普通待机，必须正向播放，不能再倒放旧过渡动画。
        new(
            ReturnTransitionName,
            $"{DonghuaRoot}/30115_260826191827_9534a4_30",
            21,
            PlaybackFps: 50.0),
        new(AttackReadyName, $"{Root}/gongji/30115_260826093733_6c004b_30", 6),
        new(AttackAName, $"{Root}/gongji/30115_260826094153_bdab60_30", 21),
        new(AttackBName, $"{Root}/gongji/30115_260826094204_56af13_30", 21),
        new(AttackEndName, $"{Root}/gongji/30115_260826094219_cae693_30", AttackEndFrameCount),
        new(SkillReadyName, $"{Root}/jineng/30115_260826094514_f8ed04_30", 15),
        new(SkillReleaseName, $"{Root}/jineng/30115_260826094528_743193_30", 34),
        new(PowerReadyName, $"{Root}/nengli/30115_260826094647_bc8c52_30", 15),
        new(PowerReleaseName, $"{Root}/nengli/30115_260826094657_c1c9e6_30", 34),
        new(HitName, $"{Root}/shoudaogongji/30115_260826095004_a3baea_30", 21),
        // 原第 25 帧内容错误且已删除，死亡动画现在以 000023 为正确结束帧。
        new(DeathName, $"{Root}/siwang/30115_260826095036_8965b4_30", 24)
    ];

    public const string MerchantFolder =
        $"{Root}/shangdian/30115_260826095544_c296d2_30";
    public const int MerchantFrameCount = 81;

    public const string RestSiteFolder =
        $"{Root}/huodui/30115_260826095610_d40c15_30";
    public const int RestSiteFrameCount = 321;

    private static SpriteFrames? _preloadedCombatFrames;

    // 商店和休息动画由 GDScript 播放，这里保留强引用以确保纹理不会被资源缓存提前回收。
    private static readonly List<Texture2D> PreloadedSceneTextures = [];

    /// <summary>
    /// 在模组初始化阶段一次性载入全部已使用人物动画。
    /// 之后战斗、商店和休息场景只复用内存中的纹理，不会在第一次出牌时临时解码图片。
    /// </summary>
    public static void PreloadAllCharacterFrames()
    {
        if (_preloadedCombatFrames != null)
        {
            return;
        }

        _preloadedCombatFrames = CreateSpriteFrames(
            CombatClips.Select(clip => clip.Name).ToArray());

        // RITSU013 只识别单个资源文件，无法识别这里用于拼接逐帧文件名的目录路径。
#pragma warning disable RITSU013
        PreloadSceneFolder(MerchantFolder, MerchantFrameCount);
        PreloadSceneFolder(RestSiteFolder, RestSiteFrameCount);
#pragma warning restore RITSU013

        int combatFrameCount = CombatClips.Sum(clip => clip.FrameCount);
        Entry.Logger.Info(
            $"Arabella 人物动画预载完成：战斗 {combatFrameCount} 帧，场景 {PreloadedSceneTextures.Count} 帧。可开始复用缓存。");
    }

    public static SpriteFrames GetPreloadedCombatFrames()
    {
        PreloadAllCharacterFrames();
        return _preloadedCombatFrames!;
    }

    /// <summary>
    /// 创建指定动画的 SpriteFrames。正常运行时由 PreloadAllCharacterFrames 一次性调用。
    /// </summary>
    public static SpriteFrames CreateSpriteFrames(params StringName[] animationNames)
    {
        SpriteFrames spriteFrames = new();
        if (spriteFrames.HasAnimation("default"))
        {
            spriteFrames.RemoveAnimation("default");
        }

        foreach (StringName animationName in animationNames)
        {
            Clip? clip = CombatClips.FirstOrDefault(candidate => candidate.Name == animationName);
            if (clip == null)
            {
                Entry.Logger.Warn($"未找到动画配置：{animationName}");
                continue;
            }

            AddClip(spriteFrames, clip);
        }

        return spriteFrames;
    }

    private static void AddClip(SpriteFrames spriteFrames, Clip clip)
    {
        spriteFrames.AddAnimation(clip.Name);
        spriteFrames.SetAnimationLoop(clip.Name, clip.Loop);
        // 个别短过渡可以单独提速；未配置时仍统一使用战斗动画的默认帧率。
        spriteFrames.SetAnimationSpeed(clip.Name, clip.PlaybackFps ?? FramesPerSecond);

        for (int frameIndex = 0; frameIndex < clip.FrameCount; frameIndex++)
        {
            string texturePath = $"{clip.Folder}/frame_30_{frameIndex:D6}.png";
            // Reuse 让战斗、商店和休息场景共享同一份纹理资源，避免重复解码。
            Texture2D? texture = ResourceLoader.Load<Texture2D>(
                texturePath,
                cacheMode: ResourceLoader.CacheMode.Reuse);

            if (texture == null)
            {
                // 缺帧时保留其余动画可用，并在日志中给出可直接定位的完整资源路径。
                Entry.Logger.Warn($"动画帧加载失败：{texturePath}");
                continue;
            }

            spriteFrames.AddFrame(clip.Name, texture);
        }
    }

    private static void PreloadSceneFolder(string folder, int frameCount)
    {
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            string texturePath = $"{folder}/frame_30_{frameIndex:D6}.png";
            Texture2D? texture = ResourceLoader.Load<Texture2D>(
                texturePath,
                cacheMode: ResourceLoader.CacheMode.Reuse);

            if (texture == null)
            {
                Entry.Logger.Warn($"场景动画预载失败：{texturePath}");
                continue;
            }

            PreloadedSceneTextures.Add(texture);
        }
    }
}
