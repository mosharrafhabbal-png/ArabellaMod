using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using ArabellaMod.Settings;

namespace ArabellaMod.Animation;

/// <summary>
/// 参考蕾欧娜与卡莉佩的卡牌选择背身立绘：悬停时播放待机，
/// 确认攻击牌或技能牌时切换到相应的一次性动画。
/// </summary>
internal static class ArabellaBackPoseVfx
{
    private static readonly Vector2 ViewportAnchor = new(0.19f, 0.687f);
    // 与蕾欧娜、卡莉佩保持一致：设置为 150% 时使用 1.30 倍显示。
    private const float MaxEffectScaleAt150Percent = 1.30f;
    private const float EnterStartX = -425f;
    private const float EnterDuration = 0.55f;
    private const float ExitDuration = 0.45f;
    private const int OneShotSlideStartFrames = 3;
    private const float OneShotSpeedScale = 1.25f;
    private const float OneShotSlideOffsetX = -90f;
    private const float OneShotSlideDuration = 0.20f;
    private const double HoverExitDelay = 0.5;
    private const ulong VoiceCooldownMilliseconds = 5_000;

    private enum EffectMode
    {
        None,
        Idle,
        Attack,
        Skill
    }

    private static Node2D? _activeEffect;
    private static Tween? _activeTween;
    private static EffectMode _activeMode;
    private static bool _isCardPlayActive;
    private static bool _isCardHovered;
    private static bool _isIdleExiting;
    private static CardType? _pendingCardType;
    private static ulong _hoverVersion;
    private static ulong _effectVersion;

    private static float GetConfiguredScale()
    {
        return ScaleForPercent(ArabellaSettingsPage.BackPoseScaleBinding.Read());
    }

    private static float ScaleForPercent(int percent)
    {
        int clampedPercent = Mathf.Clamp(percent, 80, 150);
        return MaxEffectScaleAt150Percent * clampedPercent / 150f;
    }

    internal static void ApplyEnabled(bool enabled)
    {
        if (!enabled)
        {
            RemoveActiveImmediately();
        }
    }

    internal static void ApplyScalePercent(int percent)
    {
        if (!GodotObject.IsInstanceValid(_activeEffect))
        {
            return;
        }

        float oldScale = _activeEffect.Scale.Y;
        float newScale = ScaleForPercent(percent);
        float canvasBottom = GetLocalCanvasBottom(_activeEffect);
        _activeEffect.GlobalPosition += Vector2.Down * (canvasBottom * (oldScale - newScale));
        _activeEffect.Scale = Vector2.One * newScale;
    }

    public static void OnCardPlayStarted(CardType cardType)
    {
        _pendingCardType = cardType;
        _isCardHovered = false;
        _hoverVersion++;
        _isCardPlayActive = true;
        ShowIdle();
    }

    public static void OnCardCommitted(CardType? cardType)
    {
        CardType? committedType = cardType ?? _pendingCardType;
        _pendingCardType = null;
        _isCardPlayActive = false;

        switch (committedType)
        {
            case CardType.Attack:
                PlayOneShot(ArabellaBackPoseAssets.AttackName, EffectMode.Attack);
                break;
            case CardType.Skill:
                PlayOneShot(ArabellaBackPoseAssets.SkillName, EffectMode.Skill);
                break;
            default:
                Hide();
                break;
        }
    }

    public static void OnCardPlayEnded()
    {
        _pendingCardType = null;
        _isCardPlayActive = false;
        if (!IsOneShotActive() && !_isCardHovered)
        {
            Hide();
        }
    }

    public static void OnCardHovered()
    {
        _isCardHovered = true;
        _hoverVersion++;
        ShowIdle();
    }

    public static void OnCardUnhovered()
    {
        _isCardHovered = false;
        ulong version = ++_hoverVersion;
        if (_isCardPlayActive || IsOneShotActive())
        {
            return;
        }

        NCombatRoom? room = NCombatRoom.Instance;
        if (!GodotObject.IsInstanceValid(room))
        {
            Hide();
            return;
        }

        room.GetTree().CreateTimer(HoverExitDelay).Timeout += () =>
        {
            if (version == _hoverVersion && !_isCardHovered &&
                !_isCardPlayActive && !IsOneShotActive())
            {
                Hide();
            }
        };
    }

    private static void ShowIdle()
    {
        if (!ArabellaSettingsPage.BackPoseEnabledBinding.Read())
        {
            RemoveActiveImmediately();
            return;
        }

        NCombatRoom? room = NCombatRoom.Instance;
        if (!GodotObject.IsInstanceValid(room))
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_activeEffect))
        {
            // 一次性出牌动画优先级最高；悬停变化不能中途覆盖它。
            return;
        }

        Node2D effect = CreateEffect(ArabellaBackPoseAssets.IdleName);
        Vector2 destination = room.GetViewportRect().Size * ViewportAnchor;
        float effectScale = GetConfiguredScale();
        destination.Y += GetBottomAnchorCorrection(effect, effectScale);
        effect.GlobalPosition = new Vector2(EnterStartX, destination.Y);
        effect.Scale = Vector2.One * effectScale;
        effect.Modulate = new Color(1f, 1f, 1f, 0f);

        _activeEffect = effect;
        _activeMode = EffectMode.Idle;
        _isIdleExiting = false;
        _effectVersion++;

        _activeTween?.Kill();
        _activeTween = effect.CreateTween().SetParallel();
        _activeTween.TweenProperty(effect, "global_position", destination, EnterDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _activeTween.TweenProperty(effect, "modulate", Colors.White, EnterDuration * 0.8f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        ArabellaAudio.Play(
            ArabellaAudio.Cue.BackPose,
            VoiceCooldownMilliseconds);
    }

    private static void PlayOneShot(StringName animation, EffectMode mode)
    {
        if (!ArabellaSettingsPage.BackPoseEnabledBinding.Read())
        {
            RemoveActiveImmediately();
            return;
        }

        NCombatRoom? room = NCombatRoom.Instance;
        if (!GodotObject.IsInstanceValid(room))
        {
            RemoveActiveImmediately();
            return;
        }

        RemoveActiveImmediately();

        Node2D effect = CreateEffect(animation);
        Vector2 destination = room.GetViewportRect().Size * ViewportAnchor;
        float effectScale = GetConfiguredScale();
        destination.Y += GetBottomAnchorCorrection(effect, effectScale);
        effect.GlobalPosition = destination;
        effect.Scale = Vector2.One * effectScale;
        effect.Modulate = Colors.White;

        _activeEffect = effect;
        _activeMode = mode;
        _isIdleExiting = false;
        ulong version = ++_effectVersion;

        switch (mode)
        {
            case EffectMode.Attack:
                ArabellaAudio.PlayBackPoseCardVoice(ArabellaAudio.Cue.Attack);
                break;
            case EffectMode.Skill:
                ArabellaAudio.PlayBackPoseCardVoice(ArabellaAudio.Cue.Skill);
                break;
        }

        AnimatedSprite2D sprite = effect.GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        sprite.Stop();
        sprite.Frame = 0;
        sprite.SpeedScale = OneShotSpeedScale;
        sprite.Play(animation);

        int frameCount = sprite.SpriteFrames.GetFrameCount(animation);
        double framesPerSecond =
            sprite.SpriteFrames.GetAnimationSpeed(animation) * sprite.SpeedScale;
        if (frameCount <= 0 || framesPerSecond <= 0.0)
        {
            FinishOneShot(effect, version);
            return;
        }

        double slideDelay = Math.Max(
            0.0,
            (frameCount - Math.Min(OneShotSlideStartFrames, frameCount)) /
            framesPerSecond);
        effect.GetTree().CreateTimer(slideDelay).Timeout +=
            () => BeginOneShotSlide(effect, version);
    }

    private static Node2D CreateEffect(StringName animation)
    {
        Node2D effect = new()
        {
            Name = $"ArabellaBackPose_{animation}",
            ZAsRelative = true,
            ZIndex = 0
        };
        AnimatedSprite2D sprite = new()
        {
            Name = "AnimatedSprite2D",
            SpriteFrames = ArabellaBackPoseAssets.GetFrames(),
            Animation = animation,
            Material = ArabellaBackPoseAssets.GetGlowMaterial()
        };
        effect.AddChild(sprite);
        sprite.Play(animation);

        // 放在 CombatUi 的第一个普通子节点，使立绘盖住战斗单位和伤害数字，
        // 同时让手牌等同 Z UI 在之后绘制并保持可见。
        NCombatRoom.Instance!.Ui.AddChild(effect);
        NCombatRoom.Instance.Ui.MoveChild(effect, 0);
        return effect;
    }

    private static float GetBottomAnchorCorrection(Node2D effect, float targetScale)
    {
        return GetLocalCanvasBottom(effect) * (MaxEffectScaleAt150Percent - targetScale);
    }

    private static float GetLocalCanvasBottom(Node2D effect)
    {
        AnimatedSprite2D? sprite = effect.GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (!GodotObject.IsInstanceValid(sprite) || sprite.SpriteFrames == null)
        {
            return 0f;
        }

        Texture2D? texture = sprite.SpriteFrames.GetFrameTexture(
            sprite.Animation,
            Math.Max(sprite.Frame, 0));
        if (texture == null)
        {
            return 0f;
        }

        float textureBottom = sprite.Centered
            ? texture.GetHeight() * 0.5f
            : texture.GetHeight();
        return sprite.Position.Y + (sprite.Offset.Y + textureBottom) * sprite.Scale.Y;
    }

    private static bool IsOneShotActive()
    {
        return _activeMode is EffectMode.Attack or EffectMode.Skill;
    }

    private static void BeginOneShotSlide(Node2D effect, ulong version)
    {
        if (!GodotObject.IsInstanceValid(effect) || effect != _activeEffect ||
            version != _effectVersion || !IsOneShotActive())
        {
            return;
        }

        _activeTween?.Kill();
        Vector2 destination = effect.GlobalPosition + new Vector2(OneShotSlideOffsetX, 0f);
        Tween slideTween = effect.CreateTween();
        _activeTween = slideTween;
        slideTween.TweenProperty(effect, "global_position", destination, OneShotSlideDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        slideTween.Finished += () => FinishOneShot(effect, version);
    }

    private static void FinishOneShot(Node2D effect, ulong version)
    {
        if (!GodotObject.IsInstanceValid(effect) || effect != _activeEffect ||
            version != _effectVersion || !IsOneShotActive())
        {
            return;
        }

        _activeTween?.Kill();
        _activeTween = null;
        _activeEffect = null;
        _activeMode = EffectMode.None;
        effect.QueueFree();
    }

    private static void Hide()
    {
        if (IsOneShotActive() || _isIdleExiting)
        {
            return;
        }

        Node2D? effect = _activeEffect;
        if (!GodotObject.IsInstanceValid(effect))
        {
            _activeEffect = null;
            _activeMode = EffectMode.None;
            _activeTween = null;
            _isIdleExiting = false;
            return;
        }

        _isIdleExiting = true;
        ulong version = _effectVersion;
        _activeTween?.Kill();
        Vector2 destination = new(EnterStartX, effect.GlobalPosition.Y);
        Tween exitTween = effect.CreateTween().SetParallel();
        _activeTween = exitTween;
        exitTween.TweenProperty(effect, "global_position", destination, ExitDuration)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);
        exitTween.TweenProperty(
                effect,
                "modulate",
                new Color(1f, 1f, 1f, 0f),
                ExitDuration * 0.65f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        exitTween.Finished += () =>
        {
            bool shouldShowAgain = false;
            if (effect == _activeEffect && version == _effectVersion)
            {
                _activeEffect = null;
                _activeMode = EffectMode.None;
                _activeTween = null;
                _isIdleExiting = false;
                shouldShowAgain = _isCardHovered && !_isCardPlayActive;
            }

            if (GodotObject.IsInstanceValid(effect))
            {
                effect.QueueFree();
            }

            if (shouldShowAgain)
            {
                ShowIdle();
            }
        };
    }

    private static void RemoveActiveImmediately()
    {
        Node2D? effect = _activeEffect;
        _activeEffect = null;
        _activeMode = EffectMode.None;
        _isIdleExiting = false;
        _activeTween?.Kill();
        _activeTween = null;
        _effectVersion++;
        if (GodotObject.IsInstanceValid(effect))
        {
            effect.QueueFree();
        }
    }
}

[HarmonyPatch(typeof(NPlayerHand), "StartCardPlay")]
internal static class ArabellaBackPoseCardPlayStartedPatch
{
    private static void Postfix(NHandCardHolder holder)
    {
        if (holder.CardModel?.Owner?.Character is Characters.ArabellaModCharacter)
        {
            ArabellaBackPoseVfx.OnCardPlayStarted(holder.CardModel.Type);
        }
    }
}

[HarmonyPatch(typeof(NCardPlay), "TryPlayCard")]
internal static class ArabellaBackPoseCardCommittedPatch
{
    private static void Prefix(NCardPlay __instance, Creature? target)
    {
        CardModel? card = __instance.Holder?.CardModel;
        if (card?.Owner?.Character is Characters.ArabellaModCharacter &&
            card.CanPlayTargeting(target))
        {
            ArabellaBackPoseVfx.OnCardCommitted(card.Type);
        }
    }
}

[HarmonyPatch(typeof(NCardPlay), nameof(NCardPlay.CancelPlayCard))]
internal static class ArabellaBackPoseCardPlayCancelledPatch
{
    private static void Postfix(NCardPlay __instance)
    {
        if (__instance.Player?.Character is Characters.ArabellaModCharacter)
        {
            ArabellaBackPoseVfx.OnCardPlayEnded();
        }
    }
}

[HarmonyPatch(typeof(NPlayerHand), "OnHolderFocused")]
internal static class ArabellaBackPoseCardHoveredPatch
{
    private static void Postfix(NHandCardHolder holder)
    {
        if (holder.CardModel?.Owner?.Character is Characters.ArabellaModCharacter)
        {
            ArabellaBackPoseVfx.OnCardHovered();
        }
    }
}

[HarmonyPatch(typeof(NPlayerHand), "OnHolderUnfocused")]
internal static class ArabellaBackPoseCardUnhoveredPatch
{
    private static void Postfix(NHandCardHolder holder)
    {
        if (holder.CardModel?.Owner?.Character is Characters.ArabellaModCharacter)
        {
            ArabellaBackPoseVfx.OnCardUnhovered();
        }
    }
}
