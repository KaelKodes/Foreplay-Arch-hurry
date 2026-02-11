using Godot;

namespace Archery;

/// <summary>
/// Centralized MOBA UI theme. Provides reusable StyleBox helpers and a shared
/// Theme resource that any Control can adopt via MobaTheme.Apply(control).
/// </summary>
public static class MobaTheme
{
    // ── Color Palette ──────────────────────────────────────────
    public static readonly Color PanelBg = new(0.078f, 0.078f, 0.118f, 0.88f);  // #14141E
    public static readonly Color PanelBorder = new(0.4f, 0.48f, 0.7f, 0.6f);        // #667AB3
    public static readonly Color AccentGold = new(1f, 0.85f, 0.4f);                 // #FFD966
    public static readonly Color TextPrimary = new(0.93f, 0.93f, 0.96f);             // near-white
    public static readonly Color TextSecondary = new(0.7f, 0.7f, 0.8f);               // muted lavender
    public static readonly Color TextMuted = new(0.5f, 0.5f, 0.6f);

    // Bar fills
    public static readonly Color HpFill = new(0.2f, 0.85f, 0.3f);               // #33D94D
    public static readonly Color HpBg = new(0.2f, 0.1f, 0.1f, 0.8f);
    public static readonly Color ShieldFill = new(0.3f, 0.6f, 1.0f);            // #4DA6FF (Bright Blue)
    public static readonly Color ManaFill = new(0.2f, 0.4f, 1f);                  // #3366FF
    public static readonly Color ManaBg = new(0.1f, 0.1f, 0.2f, 0.8f);
    public static readonly Color StaminaFill = new(1f, 0.72f, 0.2f);                 // #FFB833
    public static readonly Color StaminaBg = new(0.2f, 0.15f, 0.05f, 0.8f);
    public static readonly Color FuryFill = new(1f, 0.3f, 0.2f);                  // #FF4D33
    public static readonly Color FuryBg = new(0.25f, 0.08f, 0.08f, 0.8f);
    public static readonly Color XpFill = new(0.6f, 0.4f, 1f);                  // soft violet
    public static readonly Color XpBg = new(0.12f, 0.08f, 0.2f, 0.7f);

    // Layout constants
    public const int PanelCornerRadius = 8;
    public const int BarCornerRadius = 4;
    public const int BorderWidth = 2;
    public const int FontSizeBody = 12;
    public const int FontSizeHeading = 16;
    public const int FontSizeHero = 24;
    public const int FontSizeTooltip = 13;

    // ── StyleBox Builders ──────────────────────────────────────

    /// <summary>Creates a dark translucent panel style with frosted borders.</summary>
    public static StyleBoxFlat CreatePanelStyle()
    {
        var s = new StyleBoxFlat();
        s.BgColor = PanelBg;
        SetCorners(s, PanelCornerRadius);
        SetBorder(s, BorderWidth, PanelBorder);
        s.ContentMarginLeft = 10;
        s.ContentMarginRight = 10;
        s.ContentMarginTop = 8;
        s.ContentMarginBottom = 8;
        return s;
    }

    /// <summary>Creates a tooltip-specific panel (slightly brighter border, extra padding).</summary>
    public static StyleBoxFlat CreateTooltipStyle()
    {
        var s = new StyleBoxFlat();
        s.BgColor = new Color(0.1f, 0.1f, 0.16f, 0.95f);
        SetCorners(s, PanelCornerRadius);
        SetBorder(s, 2, new Color(0.5f, 0.6f, 0.85f, 0.7f));
        s.ContentMarginLeft = 14;
        s.ContentMarginRight = 14;
        s.ContentMarginTop = 10;
        s.ContentMarginBottom = 10;
        // Subtle shadow effect
        s.ShadowColor = new Color(0, 0, 0, 0.4f);
        s.ShadowSize = 6;
        return s;
    }

    /// <summary>Creates a fill StyleBoxFlat for a progress bar.</summary>
    public static StyleBoxFlat CreateBarFill(Color color)
    {
        var s = new StyleBoxFlat();
        s.BgColor = color;
        SetCorners(s, BarCornerRadius);
        return s;
    }

    /// <summary>Creates a background StyleBoxFlat for a progress bar.</summary>
    public static StyleBoxFlat CreateBarBg(Color color)
    {
        var s = new StyleBoxFlat();
        s.BgColor = color;
        SetCorners(s, BarCornerRadius);
        return s;
    }

    // ── Progress Bar Factories ─────────────────────────────────

    /// <summary>Creates and styles an HP progress bar.</summary>
    public static ProgressBar CreateHpBar(float maxHp = 100f)
    {
        var bar = CreateStyledBar(HpFill, HpBg, maxHp);
        bar.CustomMinimumSize = new Vector2(0, 22);
        return bar;
    }

    /// <summary>Creates and styles a Mana progress bar.</summary>
    public static ProgressBar CreateManaBar(float maxMana = 100f)
    {
        var bar = CreateStyledBar(ManaFill, ManaBg, maxMana);
        bar.CustomMinimumSize = new Vector2(0, 16);
        return bar;
    }

    /// <summary>Creates and styles a Stamina progress bar.</summary>
    public static ProgressBar CreateStaminaBar(float maxStamina = 100f)
    {
        var bar = CreateStyledBar(StaminaFill, StaminaBg, maxStamina);
        bar.CustomMinimumSize = new Vector2(0, 16);
        return bar;
    }

    /// <summary>Creates and styles a Fury progress bar.</summary>
    public static ProgressBar CreateFuryBar(float maxFury = 100f)
    {
        var bar = CreateStyledBar(FuryFill, FuryBg, maxFury);
        bar.CustomMinimumSize = new Vector2(0, 16);
        return bar;
    }

    /// <summary>Creates a thin XP progress bar.</summary>
    public static ProgressBar CreateXpBar(float maxXp = 100f)
    {
        var bar = CreateStyledBar(XpFill, XpBg, maxXp);
        bar.CustomMinimumSize = new Vector2(0, 6);
        return bar;
    }

    // ── Label Helpers ──────────────────────────────────────────

    /// <summary>Creates a label with the standard body style.</summary>
    public static Label CreateBodyLabel(string text = "", Color? color = null)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", FontSizeBody);
        label.AddThemeColorOverride("font_color", color ?? TextPrimary);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

    /// <summary>Creates a label with heading style.</summary>
    public static Label CreateHeadingLabel(string text = "", Color? color = null)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", FontSizeHeading);
        label.AddThemeColorOverride("font_color", color ?? AccentGold);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

    /// <summary>Creates a label styled for hero-level callouts.</summary>
    public static Label CreateHeroLabel(string text = "", Color? color = null)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeFontSizeOverride("font_size", FontSizeHero);
        label.AddThemeColorOverride("font_color", color ?? AccentGold);
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        return label;
    }

    // ── Panel Factory ──────────────────────────────────────────

    /// <summary>Creates a dark translucent Panel with theme styling applied.</summary>
    public static Panel CreatePanel()
    {
        var panel = new Panel();
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        return panel;
    }

    /// <summary>Creates a PanelContainer with tooltip styling.</summary>
    public static PanelContainer CreateTooltipPanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", CreateTooltipStyle());
        return panel;
    }

    // ── Internals ──────────────────────────────────────────────

    private static ProgressBar CreateStyledBar(Color fill, Color bg, float max)
    {
        var bar = new ProgressBar();
        bar.MinValue = 0;
        bar.MaxValue = max;
        bar.Value = max;
        bar.ShowPercentage = false;
        bar.AddThemeStyleboxOverride("fill", CreateBarFill(fill));
        bar.AddThemeStyleboxOverride("background", CreateBarBg(bg));
        return bar;
    }

    public static void SetCorners(StyleBoxFlat style, int radius)
    {
        style.CornerRadiusTopLeft = radius;
        style.CornerRadiusTopRight = radius;
        style.CornerRadiusBottomLeft = radius;
        style.CornerRadiusBottomRight = radius;
    }

    public static void SetBorder(StyleBoxFlat style, int width, Color color)
    {
        style.BorderWidthTop = width;
        style.BorderWidthBottom = width;
        style.BorderWidthLeft = width;
        style.BorderWidthRight = width;
        style.BorderColor = color;
    }
}
