using Godot;

namespace ArabellaMod.Animation;

/// <summary>
/// 艾拉贝拉卡牌悬停背身立绘所使用的三组序列帧。
/// 所有纹理在模组初始化时一次性载入，避免第一次悬停卡牌时卡顿。
/// </summary>
internal static class ArabellaBackPoseAssets
{
    public static readonly StringName IdleName = "idle";
    public static readonly StringName AttackName = "attack";
    public static readonly StringName SkillName = "skill";

    private const double FramesPerSecond = 24.0;
    private const string Root =
        $"{Entry.ResPath}/images/characters/donghua/lihuidonghua";

    private sealed record Clip(
        StringName Name,
        string Folder,
        int FrameCount,
        bool Loop);

    private static readonly IReadOnlyList<Clip> Clips =
    [
        new(IdleName, $"{Root}/daij", 97, Loop: true),
        new(AttackName, $"{Root}/gongji", 15, Loop: false),
        new(SkillName, $"{Root}/jineng", 15, Loop: false)
    ];

    private static SpriteFrames? _preloadedFrames;
    private static ShaderMaterial? _glowMaterial;

    public static void Preload()
    {
        if (_preloadedFrames != null)
        {
            return;
        }

        SpriteFrames frames = new();
        if (frames.HasAnimation("default"))
        {
            frames.RemoveAnimation("default");
        }

        foreach (Clip clip in Clips)
        {
            frames.AddAnimation(clip.Name);
            frames.SetAnimationLoop(clip.Name, clip.Loop);
            frames.SetAnimationSpeed(clip.Name, FramesPerSecond);

            for (int frameIndex = 0; frameIndex < clip.FrameCount; frameIndex++)
            {
                string texturePath = $"{clip.Folder}/frame_30_{frameIndex:D6}.png";
                Texture2D? texture = ResourceLoader.Load<Texture2D>(
                    texturePath,
                    cacheMode: ResourceLoader.CacheMode.Reuse);
                if (texture == null)
                {
                    Entry.Logger.Warn($"背身动画帧加载失败：{texturePath}");
                    continue;
                }

                frames.AddFrame(clip.Name, texture);
            }
        }

        Shader? glowShader = ResourceLoader.Load<Shader>(
            $"{Entry.ResPath}/shaders/arabella_back_pose_glow.gdshader",
            cacheMode: ResourceLoader.CacheMode.Reuse);
        if (glowShader != null)
        {
            _glowMaterial = new ShaderMaterial { Shader = glowShader };
        }
        else
        {
            Entry.Logger.Warn("背身动画轮廓光 Shader 加载失败，将使用无轮廓光的原始立绘。");
        }

        _preloadedFrames = frames;
        Entry.Logger.Info(
            $"艾拉贝拉背身动画预载完成：{Clips.Sum(clip => clip.FrameCount)} 帧。");
    }

    public static SpriteFrames GetFrames()
    {
        Preload();
        return _preloadedFrames!;
    }

    public static ShaderMaterial? GetGlowMaterial()
    {
        Preload();
        return _glowMaterial;
    }
}
