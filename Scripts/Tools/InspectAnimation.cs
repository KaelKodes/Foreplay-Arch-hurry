using Godot;
using System;

[Tool]
public partial class InspectAnimation : Node
{
    [Export] public string AnimPath = "res://Assets/Animations/Monsters/Anim__026.res";

    private bool _runInspect;
    [Export]
    public bool RunInspect
    {
        get => _runInspect;
        set
        {
            _runInspect = value;
            if (_runInspect && IsInsideTree())
            {
                _runInspect = false;
                Inspect();
            }
        }
    }

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            GD.Print("[Inspect] Tool Ready. Toggle 'Run Inspect' to check.");
        }
    }

    public override void _Process(double delta)
    {
        if (RunInspect)
        {
            RunInspect = false;
            Inspect();
        }
    }

    private void Inspect()
    {
        if (!ResourceLoader.Exists(AnimPath))
        {
            GD.PrintErr($"[Inspect] File not found: {AnimPath}");
            return;
        }

        var anim = ResourceLoader.Load<Animation>(AnimPath);
        if (anim == null)
        {
            GD.PrintErr($"[Inspect] Failed to load animation: {AnimPath}");
            return;
        }

        int count = anim.GetTrackCount();
        GD.Print($"[Inspect] Animation: {AnimPath} has {count} tracks.");
        for (int i = 0; i < Math.Min(count, 20); i++)
        {
            GD.Print($"  Track {i}: {anim.TrackGetPath(i)}");
        }
        if (count > 20) GD.Print("  ... (truncated)");
    }
}
