using Godot;

namespace Archery;

/// <summary>
/// Floating tooltip panel that displays ability info when hovering over a hotbar slot.
/// Uses MobaTheme styling for visual consistency.
/// </summary>
public partial class AbilityTooltip : PanelContainer
{
    private Label _nameLabel;
    private RichTextLabel _descLabel;
    private Label _cooldownLabel;
    private Label _costLabel;
    private Label _nextLevelHeader;
    private RichTextLabel _nextLevelLabel;

    public override void _Ready()
    {
        // Apply tooltip panel style
        AddThemeStyleboxOverride("panel", MobaTheme.CreateTooltipStyle());

        // Tooltip should float above everything. 
        // We set MouseFilter to PASS so we can hover over hint tags (tooltips inside tooltips).
        ZIndex = 100;
        MouseFilter = MouseFilterEnum.Pass;

        // Layout
        var vbox = new VBoxContainer();
        vbox.MouseFilter = MouseFilterEnum.Pass;
        vbox.AddThemeConstantOverride("separation", 4);
        AddChild(vbox);

        // Ability Name (gold heading)
        _nameLabel = MobaTheme.CreateHeadingLabel("Ability Name", MobaTheme.AccentGold);
        vbox.AddChild(_nameLabel);

        // Description (RichTextLabel for BBCode [hint])
        _descLabel = new RichTextLabel();
        _descLabel.BbcodeEnabled = true;
        _descLabel.FitContent = true;
        _descLabel.AddThemeFontSizeOverride("normal_font_size", MobaTheme.FontSizeTooltip);
        _descLabel.AddThemeColorOverride("default_color", MobaTheme.TextPrimary);
        _descLabel.CustomMinimumSize = new Vector2(260, 0);
        _descLabel.MouseFilter = MouseFilterEnum.Pass;
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

        _nextLevelLabel = new RichTextLabel();
        _nextLevelLabel.BbcodeEnabled = true;
        _nextLevelLabel.FitContent = true;
        _nextLevelLabel.AddThemeFontSizeOverride("normal_font_size", MobaTheme.FontSizeBody);
        _nextLevelLabel.AddThemeColorOverride("default_color", new Color(0.6f, 0.9f, 0.6f));
        _nextLevelLabel.CustomMinimumSize = new Vector2(260, 0);
        _nextLevelLabel.MouseFilter = MouseFilterEnum.Pass;
        vbox.AddChild(_nextLevelLabel);

        Visible = false;
    }

    /// <summary>
    /// Populate and show the tooltip with ability data.
    /// </summary>
    public void ShowAbility(AbilityInfo ability, Vector2 globalPos, Stats stats = null)
    {
        if (ability == null) { Hide(); return; }

        _nameLabel.Text = ability.Name;
        _descLabel.Text = ProcessDescription(ability.Description, stats);
        _cooldownLabel.Text = $"⏱ {ability.Cooldown}";
        _costLabel.Text = $"⚡ {ability.Cost} {ability.CostType}";
        _nextLevelLabel.Text = ProcessDescription(ability.NextLevelPreview, stats);

        Visible = true;

        // Force update to calculate correct Size
        ForceUpdateTransform();

        // Initial position above the slot
        Vector2 targetPos = new Vector2(
            globalPos.X - Size.X / 2f,
            globalPos.Y - Size.Y - 12f
        );

        // Clamping to screen bounds
        var viewportRect = GetViewportRect();
        float margin = 10f;

        targetPos.X = Mathf.Clamp(targetPos.X, margin, viewportRect.Size.X - Size.X - margin);
        targetPos.Y = Mathf.Clamp(targetPos.Y, margin, viewportRect.Size.Y - Size.Y - margin);

        GlobalPosition = targetPos;
    }

    private string ProcessDescription(string desc, Stats stats)
    {
        if (string.IsNullOrEmpty(desc) || stats == null) return desc;

        string processed = desc;

        // 1. Match formula pattern like "150 + (2.0 × INT)"
        // Regex: (Base) + (Multiplier x StatName)
        var formulaRegex = new System.Text.RegularExpressions.Regex(@"(\d+(?:\.\d+)?)\s*\+\s*\(\s*(\d+(?:\.\d+)?)\s*[×x*]\s*([A-Z]+)\s*\)");
        processed = formulaRegex.Replace(processed, match =>
        {
            string fullFormula = match.Value;
            float baseValue = float.Parse(match.Groups[1].Value);
            float multiplier = float.Parse(match.Groups[2].Value);
            string statName = match.Groups[3].Value;

            float statValue = GetStatValue(stats, statName);
            float calculated = baseValue + (multiplier * statValue);

            return $"[b][hint={fullFormula}]{Mathf.RoundToInt(calculated)}[/hint][/b]";
        });

        // 2. Match percentage scaling like "120% AD"
        var percentRegex = new System.Text.RegularExpressions.Regex(@"(\d+)%\s*([A-Z]+)");
        processed = percentRegex.Replace(processed, match =>
        {
            string fullFormula = match.Value;
            float percent = float.Parse(match.Groups[1].Value);
            string statName = match.Groups[2].Value;

            float statValue = GetStatValue(stats, statName);
            float calculated = (percent / 100f) * statValue;

            return $"[b][hint={fullFormula}]{Mathf.RoundToInt(calculated)}[/hint][/b]";
        });

        return processed;
    }

    private float GetStatValue(Stats stats, string statName)
    {
        switch (statName.ToUpper())
        {
            case "STR": return stats.Strength;
            case "INT": return stats.Intelligence;
            case "VIT": return stats.Vitality;
            case "WIS": return stats.Wisdom;
            case "AGI": return stats.Agility;
            case "AD": return stats.AttackDamage;
            case "PD": return stats.PhysicalDamage;
            case "MD": return stats.MagicDamage;
            case "HASTE": return stats.Haste;
            case "CONC": return stats.Concentration;
            case "LVL": return stats.Level;
            default: return 0;
        }
    }

    public void HideTooltip()
    {
        Visible = false;
    }
}
