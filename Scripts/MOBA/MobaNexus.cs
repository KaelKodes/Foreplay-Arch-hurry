using Godot;

namespace Archery;

public partial class MobaNexus : InteractableObject
{
	[Export] public float MaxHealth = 5000f;
	[Signal] public delegate void NexusDestroyedEventHandler(MobaTeam team);

	public float Health { get; private set; }
	public bool IsDestroyed => Health <= 0;

	private MeshInstance3D _teamColorMesh;
	private Node _lastAttacker;

	public override void _Ready()
	{
		Health = MaxHealth;
		AddToGroup("nexus");
		AddToGroup("targetables");
		AddToGroup($"team_{Team.ToString().ToLower()}");

		_teamColorMesh = FindMeshRecursive(this);
		ApplyTeamColor();

		base._Ready();
#if DEBUG
		GD.Print($"[MobaNexus] {Name} initialized - Team: {Team}");
#endif
	}

	public override void OnHit(float damage, Vector3 hitPosition, Vector3 hitNormal, Node attacker = null)
	{
		if (attacker != null) _lastAttacker = attacker;
		TakeDamage(damage);
	}

	public void TakeDamage(float damage)
	{
		if (IsDestroyed) return;
		Health -= damage;
		if (_lastAttacker is PlayerController pc)
		{
			pc.RegisterDealtDamage(damage);
		}
		if (Health <= 0) OnDestroyed();
	}

	private void OnDestroyed()
	{
		// Award Last Hit Bonus
		if (_lastAttacker is PlayerController pc)
		{
			var archerySystem = pc.GetNodeOrNull<ArcherySystem>("ArcherySystem")
							 ?? pc.FindChild("ArcherySystem", true, false) as ArcherySystem;
			var stats = archerySystem?.GetNodeOrNull<StatsService>("StatsService");

			if (stats != null)
			{
				stats.AddGold(1000);
				stats.AddExperience(1000);
				GD.Print($"[MobaNexus] {pc.Name} got LAST HIT bonus on Nexus!");
			}
		}

		EmitSignal(SignalName.NexusDestroyed, (int)Team);
		var gameManager = GetTree().GetFirstNodeInGroup("moba_game_manager") as MobaGameManager;
		gameManager?.OnNexusDestroyed(this);
		RemoveFromGroup("targetables");
		Visible = false;
	}

	private void ApplyTeamColor()
	{
		if (_teamColorMesh == null) return;
		Color teamColor = TeamSystem.GetTeamColor(Team);
		var mat = new StandardMaterial3D();
		mat.AlbedoColor = teamColor;
		_teamColorMesh.MaterialOverride = mat;
	}
}
