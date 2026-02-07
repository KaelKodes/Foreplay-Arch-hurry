using Godot;

namespace Archery;

/// <summary>
/// The nexus - destroying it wins the game for the opposing team.
/// </summary>
public partial class MobaNexus : Node3D
{
	[Export] public MobaTeam Team = MobaTeam.None;
	[Export] public float MaxHealth = 5000f;

	[Signal] public delegate void NexusDestroyedEventHandler(MobaTeam team);

	public float Health { get; private set; }
	public bool IsDestroyed => Health <= 0;

	private MeshInstance3D _teamColorMesh;

	public override void _Ready()
	{
		Health = MaxHealth;
		AddToGroup("nexus");
		AddToGroup($"team_{Team.ToString().ToLower()}");

		// Find mesh to apply team color
		_teamColorMesh = FindMeshRecursive(this);
		ApplyTeamColor();

		GD.Print($"[MobaNexus] {Name} initialized - Team: {Team}");
	}

	/// <summary>
	/// Called when nexus takes damage.
	/// </summary>
	public void TakeDamage(float damage)
	{
		if (IsDestroyed) return;

		Health -= damage;
		GD.Print($"[MobaNexus] {Name} took {damage} damage. Health: {Health}/{MaxHealth}");

		if (Health <= 0)
		{
			OnDestroyed();
		}
	}

	private void OnDestroyed()
	{
		GD.Print($"[MobaNexus] {Name} DESTROYED! Team {TeamSystem.GetEnemyTeam(Team)} WINS!");

		EmitSignal(SignalName.NexusDestroyed, (int)Team);

		// Notify game manager
		var gameManager = GetTree().GetFirstNodeInGroup("moba_game_manager") as MobaGameManager;
		gameManager?.OnNexusDestroyed(this);

		// Visual: could explode, etc.
		Visible = false;
	}

	private void ApplyTeamColor()
	{
		if (_teamColorMesh == null) return;

		bool colorblind = false; // TODO: Get from GameSettings
		Color teamColor = TeamSystem.GetTeamColor(Team, colorblind);

		var mat = new StandardMaterial3D();
		mat.AlbedoColor = teamColor;
		mat.EmissionEnabled = true;
		mat.Emission = teamColor;
		mat.EmissionEnergyMultiplier = 0.5f;

		_teamColorMesh.MaterialOverride = mat;
	}

	private MeshInstance3D FindMeshRecursive(Node node)
	{
		if (node is MeshInstance3D mesh) return mesh;
		foreach (Node child in node.GetChildren())
		{
			var found = FindMeshRecursive(child);
			if (found != null) return found;
		}
		return null;
	}
}
