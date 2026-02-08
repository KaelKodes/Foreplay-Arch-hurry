using Godot;

namespace Archery;

/// <summary>
/// Floating tooltip panel that displays ability info when hovering over a hotbar slot.
/// Uses MobaTheme styling for visual consistency.
/// </summary>
public partial class AbilityTooltip : PanelContainer
{
    private Label _nameLabel;
    private Label _descLabel;
    private Label _cooldownLabel;
    private Label _costLabel;
    private Label _nextLevelHeader;
    private Label _nextLevelLabel;

    public override void _Ready()
    {
        // Apply tooltip panel style
        AddThemeStyleboxOverride("panel", MobaTheme.CreateTooltipStyle());

        // Tooltip should float above everything and not block input
        ZIndex = 100;
        MouseFilter = MouseFilterEnum.Ignore;

        // Layout
        var vbox = new VBoxContainer();
        vbox.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddThemeConstantOverride("separation", 4);
        AddChild(vbox);

        // Ability Name (gold heading)
        _nameLabel = MobaTheme.CreateHeadingLabel("Ability Name", MobaTheme.AccentGold);
        vbox.AddChild(_nameLabel);

        // Description
        _descLabel = new Label();
        _descLabel.AddThemeFontSizeOverride("font_size", MobaTheme.FontSizeTooltip);
        _descLabel.AddThemeColorOverride("font_color", MobaTheme.TextPrimary);
        _descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _descLabel.CustomMinimumSize = new Vector2(260, 0);
        _descLabel.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(_descLabel);

        // Separator
        var sep = new HSeparator();
        sep.AddThemeConstantOverride("separation", 4);
        sep.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(sep);

        // Stats row (Cooldown | Cost)
        var statsHBox = new HBoxContainer();
        statsHBox.MouseFilter = MouseFilterEnum.Ignore;
        statsHBox.AddThemeConstantOverride("separation", 20);
        vbox.AddChild(statsHBox);

        _cooldownLabel = MobaTheme.CreateBodyLabel("", MobaTheme.TextSecondary);
        statsHBox.AddChild(_cooldownLabel);

        _costLabel = MobaTheme.CreateBodyLabel("", MobaTheme.TextSecondary);
        statsHBox.AddChild(_costLabel);

        // Next Level section
        _nextLevelHeader = MobaTheme.CreateBodyLabel("▲ Next Level:", new Color(0.5f, 0.85f, 1f));
        vbox.AddChild(_nextLevelHeader);

        _nextLevelLabel = new Label();
        _nextLevelLabel.AddThemeFontSizeOverride("font_size", MobaTheme.FontSizeBody);
        _nextLevelLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.9f, 0.6f));
        _nextLevelLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _nextLevelLabel.CustomMinimumSize = new Vector2(260, 0);
        _nextLevelLabel.MouseFilter = MouseFilterEnum.Ignore;
        vbox.AddChild(_nextLevelLabel);

        Visible = false;
    }

    /// <summary>
    /// Populate and show the tooltip with ability data.
    /// </summary>
    public void ShowAbility(AbilityInfo ability, Vector2 globalPos)
    {
        if (ability == null) { Hide(); return; }

        _nameLabel.Text = ability.Name;
        _descLabel.Text = ability.Description;
        _cooldownLabel.Text = $"⏱ {ability.Cooldown}";
        _costLabel.Text = $"⚡ {ability.Cost} {ability.CostType}";
        _nextLevelLabel.Text = ability.NextLevelPreview;

        // Position above the slot
        GlobalPosition = new Vector2(
            globalPos.X - Size.X / 2f,
            globalPos.Y - Size.Y - 12f
        );

        Visible = true;
    }

    public void HideTooltip()
    {
        Visible = false;
    }
}
