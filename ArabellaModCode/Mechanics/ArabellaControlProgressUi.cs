using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace ArabellaMod.Mechanics;

/// <summary>
/// Shows the independent Control progress of the card instance currently displayed by an NCard.
/// </summary>
public sealed partial class ArabellaControlProgressUi : PanelContainer
{
    private NCard? _cardNode;
    private Label? _label;
    private StyleBoxFlat? _panelStyle;
    private CardModel? _lastCard;
    private int _lastProgress = -1;
    private int _lastThreshold = -1;
    private bool _lastActive;
    private string _lastLocale = string.Empty;

    public static ArabellaControlProgressUi Create(NCard cardNode)
    {
        ArabellaControlProgressUi ui = new()
        {
            Name = "ArabellaControlProgressUi",
            CustomMinimumSize = new Vector2(160f, 34f),
            Size = new Vector2(160f, 34f),
            MouseFilter = MouseFilterEnum.Ignore,
            ZAsRelative = true,
            ZIndex = 0
        };

        ui._cardNode = cardNode;
        ui.BuildVisuals();
        ui.Refresh();
        return ui;
    }

    public void Refresh()
    {
        UpdateDisplay(force: true);
    }

    public override void _Process(double delta)
    {
        UpdateDisplay(force: false);
    }

    private void BuildVisuals()
    {
        _panelStyle = new StyleBoxFlat
        {
            BgColor = new Color("2a0714e8"),
            BorderColor = new Color("7f294f"),
            CornerRadiusTopLeft = 9,
            CornerRadiusTopRight = 9,
            CornerRadiusBottomLeft = 9,
            CornerRadiusBottomRight = 9,
            ContentMarginLeft = 6f,
            ContentMarginRight = 6f,
            ContentMarginTop = 2f,
            ContentMarginBottom = 2f
        };
        _panelStyle.SetBorderWidthAll(2);
        AddThemeStyleboxOverride("panel", _panelStyle);

        _label = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _label.AddThemeFontSizeOverride("font_size", 18);
        _label.AddThemeColorOverride("font_color", new Color("ffe8f2"));
        _label.AddThemeColorOverride("font_outline_color", new Color("21040d"));
        _label.AddThemeConstantOverride("outline_size", 4);
        AddChild(_label);
    }

    private void UpdateDisplay(bool force)
    {
        CardModel? card = _cardNode?.Model;
        int threshold = card == null ? 0 : ControlSingleton.GetThreshold(card);
        if (card == null || threshold <= 0 ||
            !card.Keywords.Contains(ArabellaKeywords.Control))
        {
            Visible = false;
            _lastCard = card;
            return;
        }

        int progress = ControlSingleton.GetProgress(card);
        bool active = card.Pile?.Type == PileType.Exhaust;
        string locale = TranslationServer.GetLocale();

        if (!force && card == _lastCard && progress == _lastProgress &&
            threshold == _lastThreshold && active == _lastActive &&
            string.Equals(locale, _lastLocale, StringComparison.Ordinal))
        {
            return;
        }

        Visible = true;
        string title = locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? "掌控"
            : "Control";
        _label!.Text = $"{title} {progress}/{threshold}";
        _label.Modulate = active ? Colors.White : new Color("c7a8b4");
        _panelStyle!.BorderColor = active
            ? new Color("ed3f77")
            : new Color("6a4050");

        _lastCard = card;
        _lastProgress = progress;
        _lastThreshold = threshold;
        _lastActive = active;
        _lastLocale = locale;
    }
}
