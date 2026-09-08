using Godot;
using ArabellaMod.Characters;
using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ArabellaMod.Mechanics;

/// <summary>
/// Registers Arabella's secondary combat resource, Lust (欲火).
/// </summary>
public static class ArabellaResources
{
    public const string LustLocalId = "lust";
    public const int MaxLust = 9;
    public const string ExcitementLocalId = "excitement";
    public const int MaxExcitement = 10;

    private const string LustCounterNodeName = "LustCounter";
    private const float LustCounterSize = 144f;

    private const string LustIconPath =
        $"{Entry.ResPath}/images/characters/sucai/icon_heart_nodelist_30115.png";

    public const string ExcitementIconPath =
        $"{Entry.ResPath}/images/powers/ArabellaGenericPower.png";

    public static SecondaryResourceDefinition LustDefinition { get; private set; } = null!;
    public static SecondaryResourceDefinition ExcitementDefinition { get; private set; } = null!;

    public static string LustId => LustDefinition.Id;
    public static string ExcitementId => ExcitementDefinition.Id;

    public static void Register()
    {
        LustSurvivalRunData.Register();
        ModSecondaryResourceRegistry registry =
            RitsuLibFramework.GetSecondaryResourceRegistry(Entry.ModId);

        LustDefinition = registry.Register(LustLocalId, new SecondaryResourceDefinition(
            defaultAmount: 0,
            baseMaxAmount: MaxLust,
            minAmount: 0,
            hardMaxAmount: MaxLust,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.Combat,
            smallIconPath: LustIconPath,
            largeIconPath: LustIconPath) { ClampToMaxAmount = true });

        ExcitementDefinition = registry.Register(ExcitementLocalId, new SecondaryResourceDefinition(
            defaultAmount: 0,
            baseMaxAmount: MaxExcitement,
            minAmount: 0,
            hardMaxAmount: MaxExcitement,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.Combat,
            smallIconPath: ExcitementIconPath,
            largeIconPath: ExcitementIconPath));

        // Arabella always needs to see Lust, including while its current amount is zero.
        registry.AlwaysShowInCombatUiForCharacter<ArabellaModCharacter>(LustDefinition.LocalId);
        registry.AlwaysShowInCombatUiForCharacter<ArabellaModCharacter>(ExcitementDefinition.LocalId);

        registry.RegisterCombatUi(
            "lust_combat_counter",
            parent =>
            {
                // Keep the character restriction outside the generic counter as well. The
                // wrapper is the final visibility gate, so Lust can never leak into another
                // character's combat UI even if that player somehow receives a nonzero value.
                ArabellaLustCounterHost host = new()
                {
                    Name = "ArabellaLustCounterHost",
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

                NSecondaryResourceCounter counter = NSecondaryResourceCounter.Create(
                    LustDefinition,
                    new SecondaryResourceCounterStyle
                    {
                        CounterSize = Vector2.One * LustCounterSize,
                        IconSize = Vector2.One * LustCounterSize,
                        FontSize = 32,
                        OutlineSize = 10,
                        PositiveColor = new Color("f3ddff"),
                        ZeroColor = new Color("c27aff"),
                        OutlineColor = new Color("35104f"),
                        AmountLabelOffset = Vector2.Zero,
                        AnimateAmountGain = false,
                        GainFeedback = null,
                        FormatAmount = (amount, maximum) => $"{amount}/{maximum ?? MaxLust}",
                        IconStyle = SecondaryResourceIconStyle.Default with
                        {
                            Size = Vector2.One * LustCounterSize,
                            HoverTip = SecondaryResourceHoverTipStyle.Default with
                            {
                                ScreenOffset = new Vector2(72f, 0f)
                            }
                        }
                    });

                counter.Name = LustCounterNodeName;

                TextureRect shadow = new()
                {
                    Name = "LustIconShadow",
                    Texture = ResourceLoader.Load<Texture2D>(LustIconPath),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    SelfModulate = new Color(0.07f, 0.01f, 0.12f, 0.60f),
                    Material = ArabellaLustCounterHost.CreateShadowMaterial(),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ZIndex = -2
                };

                TextureRect afterimage = new()
                {
                    Name = "LustPulseAfterimage",
                    Texture = ResourceLoader.Load<Texture2D>(LustIconPath),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    SelfModulate = new Color(0.80f, 0.10f, 0.34f, 0f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    ZIndex = -1
                };

                TextureRect secondAfterimage = (TextureRect)afterimage.Duplicate();
                secondAfterimage.Name = "LustSecondPulseAfterimage";

                // Keep the Lust icon centered over the energy dial. Only the hover tip moves
                // right. The softened shadow hugs the heart; each beat leaves its own echo.
                ApplyLustCounterLayout(counter);
                ApplyLustCounterLayout(
                    shadow,
                    offset: new Vector2(0f, -2f),
                    scale: ArabellaLustCounterHost.ShadowRestScaleValue);
                ApplyLustCounterLayout(
                    afterimage,
                    offset: new Vector2(1f, 0f));
                ApplyLustCounterLayout(secondAfterimage, offset: new Vector2(-1f, 0f));

                host.AddChild(shadow);
                host.AddChild(afterimage);
                host.AddChild(secondAfterimage);
                host.AddChild(counter);
                host.Initialize(counter, shadow, afterimage, secondAfterimage);
                return host;
            },
            context =>
            {
                if (context.Player is { Character: ArabellaModCharacter } arabellaPlayer)
                {
                    context.Node.Visible = true;
                    ((ArabellaLustCounterHost)context.Node).Bind(arabellaPlayer);
                    return;
                }

                context.Node.Visible = false;
            },
            context =>
            {
                if (context.Player.Character is ArabellaModCharacter &&
                    context.Definition.Id == LustId && context.Delta > 0)
                    context.Node.PlayHeartbeat();
            });

        registry.RegisterCombatUi(
            "excitement_ability_meter",
            _ => ArabellaExcitementMeter.Create(),
            context =>
            {
                if (context.Player is { Character: ArabellaModCharacter } arabellaPlayer)
                {
                    context.Node.Visible = true;
                    context.Node.Bind(arabellaPlayer);
                    return;
                }

                context.Node.Visible = false;
            },
            context =>
            {
                if (context.Player.Character is ArabellaModCharacter &&
                    context.Definition.Id == ExcitementId && context.Delta > 0)
                    context.Node.PlayGainParticles(context.Delta);
            });

        registry.RegisterCardUi(
            "lust_card_cost",
            parent =>
            {
                NSecondaryResourceCardCostUi ui = NSecondaryResourceCardCostUi.Create(
                    LustId,
                    new SecondaryResourceCardCostUiStyle
                    {
                        IconSize = new Vector2(72, 72),
                        FontSize = 24
                    });

                TextureRect energyIcon = parent.GetNode<TextureRect>("%EnergyIcon");
                ui.Position = energyIcon.Position + new Vector2(-12, 66);
                return ui;
            },
            context => context.Node.Refresh(context));

        registry.RegisterCardUi(
            "control_card_progress",
            parent =>
            {
                ArabellaControlProgressUi ui =
                    ArabellaControlProgressUi.Create(parent);
                Control descriptionLabel = parent.GetNode<Control>("%DescriptionLabel");
                ui.Position = descriptionLabel.Position + new Vector2(
                    (descriptionLabel.Size.X - ui.Size.X) * 0.5f,
                    descriptionLabel.Size.Y - 7f);
                return ui;
            },
            context => context.Node.Refresh());
    }

    private static void ApplyLustCounterLayout(
        Control counter,
        Vector2 offset = default,
        float scale = ArabellaLustCounterHost.CounterRestScaleValue)
    {
        counter.AnchorLeft = 0f;
        counter.AnchorTop = 1f;
        counter.AnchorRight = 0f;
        counter.AnchorBottom = 1f;
        float halfSize = LustCounterSize * 0.5f;
        // Enlarge the icon around the existing center without scaling the text.
        counter.OffsetLeft = 128f - halfSize + offset.X;
        counter.OffsetTop = -132f - halfSize + offset.Y;
        counter.OffsetRight = 128f + halfSize + offset.X;
        counter.OffsetBottom = -132f + halfSize + offset.Y;
        counter.GrowVertical = Control.GrowDirection.Begin;
        counter.PivotOffset = Vector2.One * halfSize;
        counter.Scale = Vector2.One * scale;
    }
}
