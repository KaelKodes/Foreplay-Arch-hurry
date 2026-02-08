using Godot;
using System.Collections.Generic;

namespace Archery;

/// <summary>
/// Coordinates MOBA game state - tracks towers, nexuses, win conditions,
/// and manages minion wave spawning with progressive scaling.
/// </summary>
public partial class MobaGameManager : Node
{
    [Signal] public delegate void GameEndedEventHandler(MobaTeam winner);
    [Signal] public delegate void TowerDestroyedEventHandler(MobaTower tower);

    public static MobaGameManager Instance { get; private set; }

    public bool IsGameOver { get; private set; } = false;
    public MobaTeam Winner { get; private set; } = MobaTeam.None;

    // ── Wave configuration ──────────────────────────────────
    private const float BaseWaveInterval = 30f;
    private const float InnerTowerDestroyedPenalty = 1.15f; // +15% spawn timer
    private const int ZombiesPerWave = 3;
    private const int CrawlersPerWave = 2;

    // ── Scene paths ─────────────────────────────────────────
    private readonly string ZombieScene = "res://Scenes/Entities/MobaZombie.tscn";
    private readonly string CrawlerScene = "res://Scenes/Entities/MobaCrawler.tscn";

    // ── State ────────────────────────────────────────────────
    private List<MobaTower> _redTowers = new();
    private List<MobaTower> _blueTowers = new();
    private MobaNexus _redNexus;
    private MobaNexus _blueNexus;

    private float _waveTimer = 0f;
    private int _waveCount = 0;
    private bool _spawningEnabled = false;

    // Track inner tower destruction per team
    private bool _redInnerDestroyed = false;
    private bool _blueInnerDestroyed = false;

    /// <summary>Public accessor for HUD to read the countdown.</summary>
    public float WaveTimeRemaining => _waveTimer;

    // Spawn positions (inner turret positions, persisted even after destruction)
    public Vector3 RedSpawnPos { get; private set; } = Vector3.Zero;
    public Vector3 BlueSpawnPos { get; private set; } = Vector3.Zero;

    // Container for spawned minions
    private Node _minionContainer;

    public override void _Ready()
    {
        Instance = this;
        AddToGroup("moba_game_manager");

        // Create MobaHUD overlay
        var hud = new MobaHUD();
        hud.Name = "MobaHUD";
        GetTree().CurrentScene.CallDeferred("add_child", hud);

        // Defer registration to let structures initialize
        CallDeferred(nameof(RegisterStructures));

        GD.Print("[MobaGameManager] Initialized");
    }

    private void RegisterStructures()
    {
        // Find all towers
        foreach (var node in GetTree().GetNodesInGroup("towers"))
        {
            if (node is MobaTower tower)
            {
                if (tower.Team == MobaTeam.Red)
                {
                    _redTowers.Add(tower);
                    if (tower.Type == TowerType.Inner)
                        RedSpawnPos = tower.GlobalPosition;
                }
                else if (tower.Team == MobaTeam.Blue)
                {
                    _blueTowers.Add(tower);
                    if (tower.Type == TowerType.Inner)
                        BlueSpawnPos = tower.GlobalPosition;
                }
            }
        }

        // Find nexuses
        foreach (var node in GetTree().GetNodesInGroup("nexus"))
        {
            if (node is MobaNexus nexus)
            {
                if (nexus.Team == MobaTeam.Red) _redNexus = nexus;
                else if (nexus.Team == MobaTeam.Blue) _blueNexus = nexus;
            }
        }

        // Create container for minions
        _minionContainer = new Node();
        _minionContainer.Name = "MinionWaves";
        GetTree().CurrentScene.AddChild(_minionContainer);

        GD.Print($"[MobaGameManager] Registered - Red Towers: {_redTowers.Count}, Blue Towers: {_blueTowers.Count}");
        GD.Print($"[MobaGameManager] Red Nexus: {_redNexus != null}, Blue Nexus: {_blueNexus != null}");
        GD.Print($"[MobaGameManager] Red Spawn: {RedSpawnPos}, Blue Spawn: {BlueSpawnPos}");

        // Enable spawning after a short delay (let everything settle)
        _spawningEnabled = true;
        _waveTimer = 5f; // First wave in 5 seconds
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsGameOver || !_spawningEnabled) return;

        float dt = (float)delta;
        _waveTimer -= dt;

        if (_waveTimer <= 0)
        {
            _waveCount++;
            SpawnMinionWave();

            // Calculate next wave interval
            float redInterval = BaseWaveInterval;
            float blueInterval = BaseWaveInterval;

            if (_redInnerDestroyed) redInterval *= InnerTowerDestroyedPenalty;
            if (_blueInnerDestroyed) blueInterval *= InnerTowerDestroyedPenalty;

            // Use the longer of the two (each team spawns on the same timer)
            _waveTimer = Mathf.Max(redInterval, blueInterval);
        }
    }

    /// <summary>
    /// Spawn a wave of minions for both teams.
    /// </summary>
    public void SpawnMinionWave()
    {
        GD.Print($"[MobaGameManager] ═══ WAVE {_waveCount} ═══");

        SpawnTeamWave(MobaTeam.Red, RedSpawnPos, _redInnerDestroyed);
        SpawnTeamWave(MobaTeam.Blue, BlueSpawnPos, _blueInnerDestroyed);
    }

    private void SpawnTeamWave(MobaTeam team, Vector3 spawnPos, bool innerDestroyed)
    {
        MobaTeam enemyTeam = TeamSystem.GetEnemyTeam(team);
        bool enemyInnerDestroyed = (enemyTeam == MobaTeam.Red) ? _redInnerDestroyed : _blueInnerDestroyed;

        // Spawn zombies (melee)
        for (int i = 0; i < ZombiesPerWave; i++)
        {
            var minion = SpawnMinion(ZombieScene, team, spawnPos, i);
            if (minion != null)
            {
                minion.ApplyWaveScaling(_waveCount);
                if (enemyInnerDestroyed) minion.MakeSuperCreep();
            }
        }

        // Spawn crawlers (ranged)
        for (int i = 0; i < CrawlersPerWave; i++)
        {
            var minion = SpawnMinion(CrawlerScene, team, spawnPos, ZombiesPerWave + i);
            if (minion != null)
            {
                minion.ApplyWaveScaling(_waveCount);
                if (enemyInnerDestroyed) minion.MakeSuperCreep();
            }
        }

        GD.Print($"[MobaGameManager] Spawned wave for {team}: {ZombiesPerWave} Zombies + {CrawlersPerWave} Crawlers");
    }

    private MobaMinion SpawnMinion(string scenePath, MobaTeam team, Vector3 spawnPos, int index)
    {
        var scene = GD.Load<PackedScene>(scenePath);
        if (scene == null)
        {
            GD.PrintErr($"[MobaGameManager] Failed to load minion scene: {scenePath}");
            return null;
        }

        var instance = scene.Instantiate();
        if (instance is not MobaMinion minion)
        {
            GD.PrintErr($"[MobaGameManager] Instantiated node is not MobaMinion: {instance.GetType()}");
            instance.QueueFree();
            return null;
        }

        // IMPORTANT: Set team BEFORE adding to tree so _Ready() registers correct groups
        minion.Team = team;
        minion.Name = $"{(team == MobaTeam.Red ? "Red" : "Blue")}_{minion.ObjectName}_{_waveCount}_{index}";

        // Offset spawn position for formation: 3 melee front, 2 ranged back
        float xOffset = 0;
        float zOffset = (team == MobaTeam.Red ? 5f : -5f); // Front row Z base

        if (index < 3) // Melee Row (Front)
        {
            xOffset = (index - 1) * 2.0f; // Spaced at -2.0, 0, 2.0
        }
        else // Ranged Row (Back)
        {
            xOffset = (index == 3 ? -1.0f : 1.0f); // Spaced at -1.0, 1.0 (behind melee gaps)
            zOffset -= (team == MobaTeam.Red ? 2.5f : -2.5f); // Move 2.5 units behind front row
        }

        Vector3 offset = new Vector3(xOffset, 0, zOffset);

        // Add to tree — _Ready() fires here with correct Team
        _minionContainer.AddChild(minion);
        minion.GlobalPosition = spawnPos + offset;

        // Ground snap: Force Y=0 for flat MOBA map
        // (Raycast was hitting minion's own CollisionShape3D at Y=0.9)
        minion.GlobalPosition = new Vector3(minion.GlobalPosition.X, 0f, minion.GlobalPosition.Z);


        return minion;
    }

    // ── Tower / Nexus Events ────────────────────────────────

    /// <summary>
    /// Called when a tower is destroyed.
    /// </summary>
    public void OnTowerDestroyed(MobaTower tower)
    {
        GD.Print($"[MobaGameManager] Tower destroyed: {tower.Name} (Team: {tower.Team}, Type: {tower.Type})");
        EmitSignal(SignalName.TowerDestroyed, tower);

        // Track inner tower destruction
        if (tower.Type == TowerType.Inner)
        {
            if (tower.Team == MobaTeam.Red)
            {
                _redInnerDestroyed = true;
                GD.Print("[MobaGameManager] RED Inner Tower destroyed! Blue minions now spawn 15% slower, but Blue gets SUPER CREEPS against Red!");
            }
            else if (tower.Team == MobaTeam.Blue)
            {
                _blueInnerDestroyed = true;
                GD.Print("[MobaGameManager] BLUE Inner Tower destroyed! Red minions now spawn 15% slower, but Red gets SUPER CREEPS against Blue!");
            }
        }
    }

    /// <summary>
    /// Called when a nexus is destroyed - triggers game end.
    /// </summary>
    public void OnNexusDestroyed(MobaNexus nexus)
    {
        if (IsGameOver) return;

        IsGameOver = true;
        Winner = TeamSystem.GetEnemyTeam(nexus.Team);

        GD.Print($"[MobaGameManager] GAME OVER! Winner: {Winner}");
        EmitSignal(SignalName.GameEnded, (int)Winner);

        // Stop spawning
        _spawningEnabled = false;

        // TODO: Show victory/defeat UI
    }

    /// <summary>
    /// Get remaining tower count for a team.
    /// </summary>
    public int GetTowerCount(MobaTeam team)
    {
        var towers = team == MobaTeam.Red ? _redTowers : _blueTowers;
        int count = 0;
        foreach (var tower in towers)
        {
            if (!tower.IsDestroyed) count++;
        }
        return count;
    }

    /// <summary>
    /// Get the current wave number.
    /// </summary>
    public int GetWaveCount() => _waveCount;
}
