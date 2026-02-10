using Godot;
using System;

namespace Archery;

[Tool]
public partial class NatureObject : InteractableObject
{
	public override void _Ready()
	{
		ObjectName = Name;
		IsMovable = false;
		IsDeletable = true;
		IsTargetable = false; // Usually not targetable unless it's a special tree

		base._Ready();
	}
}
