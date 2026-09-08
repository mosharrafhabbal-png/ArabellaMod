using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ArabellaMod.Mechanics;

/// <summary>
/// Displays Excitement as Arabella's character ability directly below her health bar.
/// Excitement still uses RitsuLib's combat-scoped resource storage, but it no longer
/// occupies the generic secondary-resource counter beside the energy orb.
/// </summary>
public sealed partial class ArabellaExcitementMeter : Control
{
    private const float MeterHeight = 14f;
    private const float MeterWidthRatio = 0.8f;
    private const float HealthBarGap = 5f;
    private const float StatusGap = 1f;
    private const float StatusHeight = 18f;
    private const float PowerIconGap = 3f;
    private const int MeterCornerRadius = 7;
    private const float PulseSpeed = 2.1f;

    private Player? _player;
    private Panel _outerGlow = null!;
    private Panel _innerGlow = null!;
    private Panel _background = null!;
    private Panel _fill = null!;
    private ColorRect _restlessMark = null!;
    private ColorRect _indulgedMark = null!;
    private TextureRect _icon = null!;
    private Label _amountLabel = null!;
    private Label _statusLabel = null!;
    private StyleBoxFlat _outerGlowStyle = null!;
    private StyleBoxFlat _innerGlowStyle = null!;
    private StyleBoxFlat _fillStyle = null!;
    private int _lastAmount = -1;
    private float _pulseTime;
    private ArabellaExcitementGainParticles _gainParticles = null!;

    public static ArabellaExcitementMeter Create()
    {
        ArabellaExcitementMeter meter = new()
        {
            Name = "ArabellaExcitementAbilityMeter",
            MouseFilter = MouseFilterEnum.Pass,
            ClipContents = false,
            ZIndex = 0
        };
        meter.SetAnchorsPreset(LayoutPreset.TopLeft);
        return meter;
    }

    public override void _Ready()
    {
        _outerGlowStyle = CreateRoundedStyle(Colors.Transparent, MeterCornerRadius + 5);
        _outerGlow = CreatePanel("OuterGlow", _outerGlowStyle);
        AddChild(_outerGlow);

        _innerGlowStyle = CreateRoundedStyle(Colors.Transparent, MeterCornerRadius + 2);
        _innerGlow = CreatePanel("InnerGlow", _innerGlowStyle);
        AddChild(_innerGlow);

        StyleBoxFlat backgroundStyle = CreateRoundedStyle(new Color("32101de8"));
        backgroundStyle.BorderColor = new Color("7d2038e6");
        SetBorderWidth(backgroundStyle, 1);
        _background = CreatePanel("Background", backgroundStyle);
        AddChild(_background);

        _fillStyle = CreateRoundedStyle(new Color("a62945"), MeterCornerRadius - 2);
        _fill = CreatePanel("Fill", _fillStyle);
        AddChild(_fill);

        _restlessMark = CreateThresholdMark("RestlessThreshold");
        _indulgedMark = CreateThresholdMark("IndulgedThreshold");
        AddChild(_restlessMark);
        AddChild(_indulgedMark);

        _icon = new TextureRect
        {
            Name = "Icon",
            Texture = ResourceLoader.Load<Texture2D>(ArabellaResources.ExcitementIconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_icon);

        // Draw the motes over the fill but under the number, preserving readability.
        _gainParticles = new ArabellaExcitementGainParticles
        {
            Name = "ExcitementGainParticles",
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_gainParticles);

        _amountLabel = new Label
        {
            Name = "AmountLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _amountLabel.AddThemeColorOverride("font_color", new Color("fff0dd"));
        _amountLabel.AddThemeColorOverride("font_outline_color", new Color("4a0b1a"));
        _amountLabel.AddThemeConstantOverride("outline_size", 4);
        _amountLabel.AddThemeFontSizeOverride("font_size", 15);
        AddChild(_amountLabel);

        _statusLabel = new Label
        {
            Name = "StatusLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _statusLabel.AddThemeColorOverride("font_color", new Color("ffe5d3"));
        _statusLabel.AddThemeColorOverride("font_outline_color", new Color("310710e6"));
        _statusLabel.AddThemeConstantOverride("outline_size", 4);
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        _statusLabel.Visible = false;
        AddChild(_statusLabel);

        MouseEntered += () => _statusLabel.Visible = true;
        MouseExited += () => _statusLabel.Visible = false;
    }

    public void Bind(Player player)
    {
        if (_player == player)
            return;
        _player = player;
        _lastAmount = -1;
        Visible = true;
        SetProcess(true);
    }

    internal void PlayGainParticles(int gain)
    {
        if (!IsNodeReady() || !IsInsideTree())
            return;

        // Overlay the track itself, independent of its fill level.
        _gainParticles.Emit(Size.X, MeterHeight, gain);
    }

    public override void _Process(double delta)
    {
        if (_player is null || NCombatRoom.Instance is null)
        {
            Visible = false;
            return;
        }

        NCreature? creatureNode = NCombatRoom.Instance.GetCreatureNode(_player.Creature);
        NCreatureStateDisplay? stateDisplay =
            creatureNode?.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        NHealthBar? healthBar = stateDisplay?.GetNodeOrNull<NHealthBar>("%HealthBar");
        Control? hpBar = healthBar?.HpBarContainer;
        if (stateDisplay is null || healthBar is null || hpBar is null)
        {
            Visible = false;
            return;
        }

        // The generic resource UI lives under CombatUi and therefore draws above the
        // back-pose animation. Reparent this meter beside the health bar so both use
        // the same canvas branch and the back-pose occludes them consistently.
        if (GetParent() != stateDisplay)
        {
            Reparent(stateDisplay, keepGlobalTransform: true);
            ZAsRelative = healthBar.ZAsRelative;
            ZIndex = healthBar.ZIndex;
        }

        Rect2 hpRect = hpBar.GetGlobalRect();
        float meterWidth = hpBar.Size.X * MeterWidthRatio;
        Vector2 hpLocalPosition =
            stateDisplay.GetGlobalTransform().AffineInverse() * hpRect.Position;
        Position = hpLocalPosition + new Vector2(
            (hpBar.Size.X - meterWidth) * 0.5f,
            hpBar.Size.Y + HealthBarGap);
        Size = new Vector2(meterWidth, MeterHeight);

        // Arabella's power icons normally start immediately below the health bar.
        // Move the container itself below the custom meter so neither UI overlaps.
        NPowerContainer? powerContainer =
            stateDisplay.GetNodeOrNull<NPowerContainer>("%PowerContainer");
        if (powerContainer is not null)
        {
            Vector2 powerPosition = powerContainer.Position;
            powerPosition.Y = Position.Y + MeterHeight + StatusGap + StatusHeight + PowerIconGap;
            powerContainer.Position = powerPosition;
        }

        Visible = stateDisplay.Visible;
        Modulate = new Color(1f, 1f, 1f, stateDisplay.Modulate.A);

        LayoutChildren();
        RefreshAmount();
        UpdateGlow((float)delta);
    }

    private void RefreshAmount()
    {
        if (_player is null)
            return;

        int amount = SecondaryResourceCmd.Get(_player, ArabellaResources.ExcitementId);
        if (amount == _lastAmount)
            return;

        _lastAmount = amount;
        float ratio = Mathf.Clamp(
            (float)amount / ArabellaResources.MaxExcitement,
            0f,
            1f);
        _fill.Visible = amount > 0;
        _fill.Size = new Vector2(Mathf.Max(0f, (Size.X - 4f) * ratio), MeterHeight - 4f);
        _amountLabel.Text = $"{amount}/{ArabellaResources.MaxExcitement}";
        _statusLabel.Text = GetStatusText(amount);
        _fillStyle.BgColor = amount switch
        {
            >= ArabellaExcitement.ExcitedThreshold => new Color("ffbd4a"),
            >= ArabellaExcitement.IndulgedThreshold => new Color("e33b55"),
            >= ArabellaExcitement.RestlessThreshold => new Color("c42e66"),
            _ => new Color("942442")
        };
    }

    private void LayoutChildren()
    {
        _outerGlow.Position = new Vector2(-5f, -5f);
        _outerGlow.Size = Size + new Vector2(10f, 10f);
        _innerGlow.Position = new Vector2(-2f, -2f);
        _innerGlow.Size = Size + new Vector2(4f, 4f);

        _background.Position = Vector2.Zero;
        _background.Size = Size;
        _fill.Position = new Vector2(2f, 2f);

        float contentWidth = Mathf.Max(0f, Size.X - 4f);
        _restlessMark.Position = new Vector2(
            2f + contentWidth * ArabellaExcitement.RestlessThreshold / ArabellaResources.MaxExcitement,
            2f);
        _indulgedMark.Position = new Vector2(
            2f + contentWidth * ArabellaExcitement.IndulgedThreshold / ArabellaResources.MaxExcitement,
            2f);
        _restlessMark.Size = new Vector2(2f, MeterHeight - 4f);
        _indulgedMark.Size = new Vector2(2f, MeterHeight - 4f);

        _icon.Position = new Vector2(-23f, -4f);
        _icon.Size = new Vector2(22f, 22f);
        _amountLabel.Position = Vector2.Zero;
        _amountLabel.Size = Size;
        _statusLabel.Position = new Vector2(-24f, MeterHeight + StatusGap);
        _statusLabel.Size = new Vector2(Size.X + 48f, StatusHeight);

        if (_lastAmount >= 0)
        {
            float ratio = Mathf.Clamp(
                (float)_lastAmount / ArabellaResources.MaxExcitement,
                0f,
                1f);
            _fill.Size = new Vector2(contentWidth * ratio, MeterHeight - 4f);
        }
    }

    private void UpdateGlow(float delta)
    {
        _pulseTime = Mathf.PosMod(_pulseTime + delta, Mathf.Tau / PulseSpeed);
        float pulse = (Mathf.Sin(_pulseTime * PulseSpeed) + 1f) * 0.5f;

        _outerGlowStyle.BgColor = new Color(
            0.95f,
            0.03f,
            0.12f,
            Mathf.Lerp(0.04f, 0.10f, pulse));
        _innerGlowStyle.BgColor = new Color(
            1f,
            0.06f,
            0.16f,
            Mathf.Lerp(0.08f, 0.20f, pulse));
    }

    private static Panel CreatePanel(string name, StyleBoxFlat style)
    {
        Panel panel = new()
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Ignore
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static StyleBoxFlat CreateRoundedStyle(Color color, int radius = MeterCornerRadius)
    {
        StyleBoxFlat style = new()
        {
            BgColor = color,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            AntiAliasing = true
        };
        return style;
    }

    private static void SetBorderWidth(StyleBoxFlat style, int width)
    {
        style.BorderWidthLeft = width;
        style.BorderWidthTop = width;
        style.BorderWidthRight = width;
        style.BorderWidthBottom = width;
    }

    private static string GetStatusText(int amount)
    {
        bool chinese = TranslationServer.GetLocale().StartsWith(
            "zh",
            StringComparison.OrdinalIgnoreCase);

        if (chinese)
        {
            return amount switch
            {
                >= ArabellaExcitement.ExcitedThreshold => "亢奋：进入时抽2张；欲火可1:1代替不足的能量",
                >= ArabellaExcitement.IndulgedThreshold => "沉迷：掌控回手额外减费1，可跨回合叠加；低于6点清除",
                >= ArabellaExcitement.RestlessThreshold => "躁动：感应触发时获得2点格挡",
                _ => "平静：暂无额外效果"
            };
        }

        return amount switch
        {
            >= ArabellaExcitement.ExcitedThreshold => "Excited: draw 2; Lust replaces missing Energy 1:1",
            >= ArabellaExcitement.IndulgedThreshold => "Indulged: Control returns stack -1 cost across turns; cleared below 6",
            >= ArabellaExcitement.RestlessThreshold => "Restless: Sensing grants 2 Block",
            _ => "Calm: no additional effect"
        };
    }

    private static ColorRect CreateThresholdMark(string name)
    {
        return new ColorRect
        {
            Name = name,
            Color = new Color("fff0dd80"),
            MouseFilter = MouseFilterEnum.Ignore
        };
    }
}
