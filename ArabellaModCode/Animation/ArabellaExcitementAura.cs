using ArabellaMod.Mechanics;
using ArabellaMod.Settings;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ArabellaMod.Animation;

/// <summary>
/// Plays the looping Excitement aura behind Arabella and cross-fades its red tint
/// as she moves between the active Excitement stages.
/// </summary>
internal sealed partial class ArabellaExcitementAura : Node2D
{
    private const int FrameCount = 40;
    private const double FramesPerSecond = 20.0;
    private static readonly Vector2 AuraScale = new(1.20f, 1.50f);
    private const float FadeInDuration = 0.32f;
    private const float FadeOutDuration = 0.28f;
    private const float StageFadeOutDuration = 0.14f;
    private const float StageFadeInDuration = 0.22f;
    private static readonly StringName AnimationName = "excitement_aura";
    private static readonly Vector2 AuraOffset = new(46f, -28f);

    private static SpriteFrames? _frames;
    private static ShaderMaterial? _tintMaterial;

    private Player? _player;
    private AnimatedSprite2D _sprite = null!;
    private Tween? _transitionTween;
    private int _displayedStage = -1;
    private ulong _transitionVersion;

    internal static void Attach(NCreature creature, AnimatedSprite2D characterSprite)
    {
        if (characterSprite.GetNodeOrNull<ArabellaExcitementAura>(
                "ArabellaExcitementAura") is not null ||
            creature.Entity?.Player is not { } player)
        {
            return;
        }

        ArabellaExcitementAura aura = new()
        {
            Name = "ArabellaExcitementAura",
            Position = AuraOffset,
            Scale = AuraScale,
            ShowBehindParent = true,
            ZAsRelative = true,
            // Keep the aura on the creature's canvas layer. NCombatRoom places the
            // whole scene at Z=-10, so a relative -1 would put this behind the room
            // background. ShowBehindParent alone supplies the required draw order.
            ZIndex = 0,
            _player = player
        };

        aura._sprite = new AnimatedSprite2D
        {
            Name = "AnimatedSprite2D",
            SpriteFrames = GetFrames(),
            Animation = AnimationName,
            Centered = true,
            Material = GetTintMaterial(),
            Visible = false,
            Modulate = Colors.Transparent
        };
        aura.AddChild(aura._sprite);
        characterSprite.AddChild(aura);
    }

    public override void _Process(double delta)
    {
        if (_player is null || !GodotObject.IsInstanceValid(_sprite))
        {
            SetProcess(false);
            return;
        }

        int amount = SecondaryResourceCmd.Get(
            _player,
            ArabellaResources.ExcitementId);
        int targetStage = ArabellaSettingsPage.SequenceFrameEffectsEnabledBinding.Read()
            ? GetStage(amount)
            : 0;

        if (targetStage != _displayedStage)
        {
            TransitionTo(targetStage);
        }
    }

    public override void _ExitTree()
    {
        _transitionTween?.Kill();
        _transitionTween = null;
    }

    private void TransitionTo(int targetStage)
    {
        int previousStage = _displayedStage;
        _displayedStage = targetStage;
        ulong version = ++_transitionVersion;
        _transitionTween?.Kill();
        _transitionTween = null;

        if (targetStage <= 0)
        {
            if (!_sprite.Visible)
            {
                _sprite.Stop();
                _sprite.Frame = 0;
                return;
            }

            Tween fadeOut = CreateTween();
            _transitionTween = fadeOut;
            fadeOut.TweenProperty(
                    _sprite,
                    "modulate",
                    WithAlpha(_sprite.Modulate, 0f),
                    FadeOutDuration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            fadeOut.Finished += () => FinishHiding(version);
            return;
        }

        Color targetColor = GetStageColor(targetStage);
        if (previousStage <= 0 || !_sprite.Visible)
        {
            _sprite.Visible = true;
            _sprite.Modulate = WithAlpha(targetColor, 0f);
            _sprite.Play(AnimationName);

            Tween fadeIn = CreateTween();
            _transitionTween = fadeIn;
            fadeIn.TweenProperty(
                    _sprite,
                    "modulate",
                    targetColor,
                    FadeInDuration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.Out);
            return;
        }

        // Cross-fade through transparency when changing stages so the stronger red
        // does not pop abruptly while the loop is in the middle of a frame.
        Tween crossFade = CreateTween();
        _transitionTween = crossFade;
        crossFade.TweenProperty(
                _sprite,
                "modulate",
                WithAlpha(_sprite.Modulate, 0f),
                StageFadeOutDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        crossFade.TweenCallback(Callable.From(() =>
        {
            if (version == _transitionVersion &&
                GodotObject.IsInstanceValid(_sprite))
            {
                _sprite.Modulate = WithAlpha(targetColor, 0f);
            }
        }));
        crossFade.TweenProperty(
                _sprite,
                "modulate",
                targetColor,
                StageFadeInDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
    }

    private void FinishHiding(ulong version)
    {
        if (version != _transitionVersion ||
            !GodotObject.IsInstanceValid(_sprite) ||
            _displayedStage > 0)
        {
            return;
        }

        _sprite.Stop();
        _sprite.Frame = 0;
        _sprite.Visible = false;
        _transitionTween = null;
    }

    private static int GetStage(int amount)
    {
        return amount switch
        {
            < ArabellaExcitement.RestlessThreshold => 0,
            >= ArabellaExcitement.ExcitedThreshold => 3,
            >= ArabellaExcitement.IndulgedThreshold => 2,
            _ => 1
        };
    }

    private static Color GetStageColor(int stage)
    {
        return stage switch
        {
            // 3-5: pale warm red; 6-9: clearly red; 10: vivid crimson.
            1 => new Color(1f, 0.58f, 0.30f, 0.52f),
            2 => new Color(1f, 0.25f, 0.09f, 0.72f),
            _ => new Color(1f, 0.08f, 0.02f, 0.94f)
        };
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
    }

    private static SpriteFrames GetFrames()
    {
        if (_frames is not null)
        {
            return _frames;
        }

        SpriteFrames frames = new();
        if (frames.HasAnimation("default"))
        {
            frames.RemoveAnimation("default");
        }

        frames.AddAnimation(AnimationName);
        frames.SetAnimationLoop(AnimationName, true);
        frames.SetAnimationSpeed(AnimationName, FramesPerSecond);

        for (int frameIndex = 0; frameIndex < FrameCount; frameIndex++)
        {
            string texturePath =
                $"{Entry.ResPath}/images/texiao/xingfen/001_{frameIndex:D2}.png";
#pragma warning disable RITSU013
            Texture2D? texture = ResourceLoader.Load<Texture2D>(
                texturePath,
                cacheMode: ResourceLoader.CacheMode.Reuse);
#pragma warning restore RITSU013
            if (texture is null)
            {
                Entry.Logger.Warn($"兴奋循环特效帧加载失败：{texturePath}");
                continue;
            }

            frames.AddFrame(AnimationName, texture);
        }

        _frames = frames;
        return frames;
    }

    private static ShaderMaterial GetTintMaterial()
    {
        if (_tintMaterial is not null)
        {
            return _tintMaterial;
        }

        // The replacement sequence is blue-white. Rebuild its RGB from source
        // brightness and the sprite's stage tint so all three stages stay red
        // without destroying the original flame and shield details.
        Shader shader = new()
        {
            Code = """
                shader_type canvas_item;
                render_mode unshaded;

                void fragment() {
                    vec4 source = texture(TEXTURE, UV);
                    float intensity = max(source.r, max(source.g, source.b));
                    vec4 tint = COLOR;
                    COLOR = vec4(tint.rgb * intensity, source.a * tint.a);
                }
                """
        };
        _tintMaterial = new ShaderMaterial
        {
            Shader = shader
        };
        return _tintMaterial;
    }
}
