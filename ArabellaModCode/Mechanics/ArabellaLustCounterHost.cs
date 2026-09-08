using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ArabellaMod.Mechanics;

/// <summary>Two heartbeat beats, each leaving a short fading silhouette.</summary>
public sealed partial class ArabellaLustCounterHost : Control
{
    internal const float CounterRestScaleValue = 0.8f;
    internal const float ShadowRestScaleValue = 0.88f;

    private NSecondaryResourceCounter? _counter;
    private TextureRect? _shadow;
    private TextureRect[] _afterimages = [];
    private Tween?[] _afterimageTweens = [];
    private Player? _player;
    private Tween? _heartbeatTween;
    private bool _pendingHeartbeat;

    public void Initialize(
        NSecondaryResourceCounter counter,
        TextureRect shadow,
        params TextureRect[] afterimages)
    {
        _counter = counter;
        _shadow = shadow;
        _afterimages = afterimages;
        _afterimageTweens = new Tween?[afterimages.Length];
    }

    public void Bind(Player player)
    {
        if (_player == player)
            return;
        _player = player;
        _counter?.Bind(player);
    }

    public override void _ExitTree()
    {
        _heartbeatTween?.Kill();
        _heartbeatTween = null;
        _pendingHeartbeat = false;
        foreach (Tween? tween in _afterimageTweens)
            tween?.Kill();
    }

    internal void PlayHeartbeat()
    {
        if (_counter is null || _shadow is null || !IsInsideTree())
            return;

        // Finish the current rhythm before replaying. Rapid gains coalesce into one
        // extra heartbeat, instead of snapping the heart to its resting scale.
        if (_heartbeatTween is { } active && active.IsRunning())
        {
            _pendingHeartbeat = true;
            return;
        }

        _heartbeatTween = CreateTween();
        AppendPulseScale(0.98f, 0.10f);
        _heartbeatTween.TweenCallback(Callable.From(() => PlayAfterimage(0, 0.98f, 0.62f)));
        AppendPulseScale(0.76f, 0.13f);
        _heartbeatTween.TweenInterval(0.06f);
        AppendPulseScale(0.92f, 0.10f);
        _heartbeatTween.TweenCallback(Callable.From(() => PlayAfterimage(1, 0.92f, 0.46f)));
        AppendPulseScale(0.78f, 0.13f);
        AppendPulseScale(CounterRestScaleValue, 0.18f);
        _heartbeatTween.Finished += () =>
        {
            _heartbeatTween = null;
            if (!_pendingHeartbeat)
                return;
            _pendingHeartbeat = false;
            PlayHeartbeat();
        };
    }

    private void PlayAfterimage(int index, float startScale, float opacity)
    {
        if (index >= _afterimages.Length)
            return;

        TextureRect echo = _afterimages[index];
        _afterimageTweens[index]?.Kill();
        echo.Scale = Vector2.One * startScale;
        // Preserve the original heart's color and chain detail in the echo.
        echo.SelfModulate = new Color(1f, 0.72f, 0.84f, opacity);
        Tween tween = CreateTween();
        _afterimageTweens[index] = tween;
        tween.TweenProperty(echo, "scale", Vector2.One * (startScale + 0.24f), 0.55f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(echo, "self_modulate:a", 0f, 0.55f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
    }

    private void AppendPulseScale(float scale, float duration)
    {
        _heartbeatTween!.TweenProperty(_counter, "scale", Vector2.One * scale, duration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _heartbeatTween.Parallel().TweenProperty(
                _shadow, "scale",
                Vector2.One * (scale + ShadowRestScaleValue - CounterRestScaleValue), duration)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    internal static ShaderMaterial CreateShadowMaterial()
    {
        // Blur only the silhouette's alpha; sample the tint before the texture is
        // applied so transparent source pixels can receive the feathered edge.
        return new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = """
                    shader_type canvas_item;
                    varying vec4 shadow_tint;
                    void vertex() {
                        shadow_tint = COLOR;
                    }
                    void fragment() {
                        float alpha = 0.0;
                        for (int x = -2; x <= 2; x++) {
                            for (int y = -2; y <= 2; y++) {
                                float weight = (3.0 - abs(float(x))) * (3.0 - abs(float(y)));
                                vec2 offset = vec2(float(x), float(y)) * TEXTURE_PIXEL_SIZE * 1.8;
                                alpha += texture(TEXTURE, UV + offset).a * weight;
                            }
                        }
                        COLOR = vec4(shadow_tint.rgb, shadow_tint.a * alpha / 81.0);
                    }
                    """
            }
        };
    }
}
