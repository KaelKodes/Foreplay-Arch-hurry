using Godot;

namespace Archery;

/// <summary>
/// Floating damage number that pops up, floats upward, and fades out.
/// </summary>
public partial class DamageNumber : Node3D
{
    private Label3D _label;
    private float _timer = 0f;
    private float _lifetime = 1.0f;
    private Vector3 _velocity = new Vector3(0, 2.0f, 0); // Float upward

    public override void _Ready()
    {
        _label = GetNodeOrNull<Label3D>("Label3D");

        // Start with a slight scale pop
        Scale = Vector3.One * 0.5f;
    }

    public void SetDamage(float damage, bool isLocalPlayer = true)
    {
        if (_label != null)
        {
            _label.Text = damage.ToString("F0");

            if (!isLocalPlayer)
            {
                _label.Modulate = new Color(0.6f, 0.6f, 0.6f); // Grey for others
                return;
            }

            // Color based on damage amount (Local Player Only)
            if (damage >= 30f)
            {
                _label.Modulate = new Color(1.0f, 0.3f, 0.1f); // Orange-red for big hits
            }
            else if (damage >= 20f)
            {
                _label.Modulate = new Color(1.0f, 0.8f, 0.2f); // Yellow for medium hits
            }
            else
            {
                _label.Modulate = new Color(1.0f, 1.0f, 1.0f); // White for normal hits
            }
        }
    }

    public void SetHeal(float amount)
    {
        if (_label != null)
        {
            _label.Text = "+" + amount.ToString("F0");
            _label.Modulate = new Color(0.2f, 1.0f, 0.2f); // Vibrant Green
        }
    }

    public void SetText(string text, Color color)
    {
        if (_label != null)
        {
            _label.Text = text;
            _label.Modulate = color;
        }
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        float t = _timer / _lifetime;

        if (t >= 1.0f)
        {
            QueueFree();
            return;
        }

        // Float upward
        GlobalPosition += _velocity * (float)delta;

        // Scale pop effect (grow then shrink slightly)
        float scaleT = Mathf.Min(t * 4f, 1f); // Quick pop in first 0.25s
        float scale = Mathf.Lerp(0.5f, 1.2f, scaleT);
        if (t > 0.25f)
        {
            scale = Mathf.Lerp(1.2f, 0.8f, (t - 0.25f) / 0.75f);
        }
        Scale = Vector3.One * scale;

        // Fade out in last half
        if (_label != null && t > 0.5f)
        {
            float alpha = 1.0f - ((t - 0.5f) / 0.5f);
            Color c = _label.Modulate;
            c.A = alpha;
            _label.Modulate = c;
        }
    }
}
