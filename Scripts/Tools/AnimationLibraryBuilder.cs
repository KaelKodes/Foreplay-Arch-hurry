using Godot;
using System;
using System.Collections.Generic;

[Tool]
public partial class AnimationLibraryBuilder : Node
{
	[Export] public string AnimationsDir = "res://Assets/Erika/";
	[Export] public NodePath TargetAnimationPlayer = "../Erika/AnimationPlayer";

	private bool _runImport = false;
	[Export]
	public bool RunImport
	{
		get => _runImport;
		set
		{
			if (value)
			{
				DoImport();
			}
			_runImport = false;
		}
	}

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
		{
			GD.Print("[AnimBuilder] Tool ready in editor.");
		}
	}

	private void DoImport()
	{
		var animPlayer = GetNodeOrNull<AnimationPlayer>(TargetAnimationPlayer);
		if (animPlayer == null)
		{
			GD.PrintErr("[AnimBuilder] Target AnimationPlayer not found!");
			return;
		}

		// Get or Create Library
		AnimationLibrary lib;
		if (animPlayer.HasAnimationLibrary(""))
		{
			lib = animPlayer.GetAnimationLibrary("");
		}
		else
		{
			lib = new AnimationLibrary();
			animPlayer.AddAnimationLibrary("", lib);
		}

		GD.Print($"[AnimBuilder] Scanning {AnimationsDir}...");

		using var dir = DirAccess.Open(AnimationsDir);
		if (dir != null)
		{
			dir.ListDirBegin();
			string fileName = dir.GetNext();
			while (fileName != "")
			{
				if (!dir.CurrentIsDir() && (fileName.EndsWith(".fbx") || fileName.EndsWith(".glb")))
				{
					string path = AnimationsDir + fileName;
					var scene = GD.Load<PackedScene>(path);
					if (scene != null)
					{
						var instance = scene.Instantiate();
						var sourcePlayer = FindAnimationPlayer(instance);
						if (sourcePlayer != null)
						{
							foreach (var animName in sourcePlayer.GetAnimationList())
							{
								var anim = sourcePlayer.GetAnimation(animName);
								// The internal name in Mixamo is usually "mixamo.com" or "Take 01"
								// We want to name it the filename for clarity
								string newName = fileName.Replace(".fbx", "").Replace(".glb", "").ToLower();

								if (lib.HasAnimation(newName)) lib.RemoveAnimation(newName);
								lib.AddAnimation(newName, (Animation)anim.Duplicate());
								GD.Print($"[AnimBuilder] Added animation: {newName} from {fileName}");
							}
						}
						instance.Free();
					}
				}
				fileName = dir.GetNext();
			}
		}
		GD.Print("[AnimBuilder] Done! Check your AnimationTree menu now.");
	}

	private AnimationPlayer FindAnimationPlayer(Node node)
	{
		if (node is AnimationPlayer ap) return ap;
		foreach (var child in node.GetChildren())
		{
			var res = FindAnimationPlayer(child);
			if (res != null) return res;
		}
		return null;
	}
}
