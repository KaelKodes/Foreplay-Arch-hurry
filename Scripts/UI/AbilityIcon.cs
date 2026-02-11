using Godot;
using System;

namespace Archery;

public partial class AbilityIcon : TextureButton
{
    [Export] public TextureRect IconRect;
    [Export] public TextureProgressBar CooldownOverlay;
    [Export] public ColorRect FlashOverlay;
    [Export] public ColorRect HighlightOverlay;
    [Export] public Label NumberLabel;
    [Export] public Label LevelLabel;
    [Export] public Button UpgradeButton;
    [Export] public AnimationPlayer AnimPlayer;

    private string _labelText = "";
    private Tween _cooldownTween;

    public override void _Ready()
    {
        // Cache nodes if not assigned
        if (IconRect == null) IconRect = GetNode<TextureRect>("VBox/IconContainer/IconRect");
        if (CooldownOverlay == null) CooldownOverlay = GetNode<TextureProgressBar>("VBox/IconContainer/CooldownOverlay");
        if (FlashOverlay == null) FlashOverlay = GetNode<ColorRect>("VBox/IconContainer/FlashOverlay");
        if (HighlightOverlay == null) HighlightOverlay = GetNodeOrNull<ColorRect>("VBox/IconContainer/Highlight");
        if (NumberLabel == null) NumberLabel = GetNodeOrNull<Label>("VBox/NumberLabel");
        if (LevelLabel == null) LevelLabel = GetNodeOrNull<Label>("VBox/LevelLabel");
        if (UpgradeButton == null) UpgradeButton = GetNodeOrNull<Button>("UpgradeButton");

        // These might be added dynamically or need finding
        if (AnimPlayer == null) AnimPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

        // Apply MobaTheme Styling
        ApplyThemeStyles();

        // Apply pending label text
        if (!string.IsNullOrEmpty(_labelText) && NumberLabel != null)
        {
            NumberLabel.Text = _labelText;
        }

        // Ensure overlay is hidden initially
        if (CooldownOverlay != null)
        {
            CooldownOverlay.Visible = false;
            CooldownOverlay.Value = 0;
        }
    }

    private void ApplyThemeStyles()
    {
        // Background Style (Unified Card)
        var bg = GetNodeOrNull<Panel>("Background");
        if (bg != null)
        {
            var slotStyle = new StyleBoxFlat();
            slotStyle.BgColor = new Color(0.1f, 0.1f, 0.15f, 0.9f); // Matches MobaTheme.PanelBg-ish
            slotStyle.CornerRadiusTopLeft = 4;
            slotStyle.CornerRadiusTopRight = 4;
            slotStyle.CornerRadiusBottomLeft = 4;
            slotStyle.CornerRadiusBottomRight = 4;
            slotStyle.BorderWidthTop = 1;
            slotStyle.BorderWidthBottom = 1;
            slotStyle.BorderWidthLeft = 1;
            slotStyle.BorderWidthRight = 1;
            slotStyle.BorderColor = MobaTheme.PanelBorder;
            bg.AddThemeStyleboxOverride("panel", slotStyle);
        }

        // Label Styles
        if (NumberLabel != null)
        {
            NumberLabel.AddThemeFontSizeOverride("font_size", 11);
            NumberLabel.AddThemeColorOverride("font_color", MobaTheme.TextMuted);
        }

        if (LevelLabel != null)
        {
            LevelLabel.AddThemeFontSizeOverride("font_size", 9);
            LevelLabel.AddThemeColorOverride("font_color", MobaTheme.AccentGold);
        }

        // Upgrade Button Style
        if (UpgradeButton != null)
        {
            UpgradeButton.AddThemeFontSizeOverride("font_size", 14);
            UpgradeButton.AddThemeColorOverride("font_color", MobaTheme.AccentGold);

            var btnStyle = new StyleBoxFlat();
            btnStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            btnStyle.BorderWidthBottom = 2;
            btnStyle.BorderColor = MobaTheme.AccentGold;
            UpgradeButton.AddThemeStyleboxOverride("normal", btnStyle);
        }
    }

    public void SetIcon(Texture2D texture)
    {
        if (IconRect != null)
        {
            IconRect.Texture = texture;
        }
    }

    public void SetLabel(string text)
    {
        _labelText = text;
        if (NumberLabel != null)
        {
            NumberLabel.Text = text;
        }
    }

    /// <summary>
    /// Starts the visual cooldown effect.
    /// </summary>
    public void StartCooldown(float duration)
    {
        if (CooldownOverlay == null || duration <= 0) return;

        // Kill existing tween if restarting
        if (_cooldownTween != null && _cooldownTween.IsValid())
        {
            _cooldownTween.Kill();
        }

        CooldownOverlay.Visible = true;
        CooldownOverlay.Value = 100; // Start full (dark)

        _cooldownTween = CreateTween();
        // Tween value from 100 down to 0 over 'duration'
        _cooldownTween.TweenProperty(CooldownOverlay, "value", 0, duration).SetTrans(Tween.TransitionType.Linear);
        _cooldownTween.TweenCallback(Callable.From(OnCooldownFinished));
    }

    private void OnCooldownFinished()
    {
        if (CooldownOverlay != null)
        {
            CooldownOverlay.Visible = false;
        }

        // Play flash animation
        if (AnimPlayer != null && AnimPlayer.HasAnimation("Refreshed"))
        {
            AnimPlayer.Play("Refreshed");
        }
    }
}
