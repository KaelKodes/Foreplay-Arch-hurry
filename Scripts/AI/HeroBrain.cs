using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

/// <summary>
/// AI decision engine for bot-controlled heroes.
/// Attached as a child of the bot's PlayerController.
/// Runs on the host (server authority) each physics frame.
/// </summary>
public partial class HeroBrain : Node
{
    // ── Configuration ─────────────────────────────────
    public BotDifficulty Difficulty { get; set; } = BotDifficulty.Beginner;
    public string HeroClass { get; set; } = "Ranger";

    // ── State Machine ─────────────────────────────────
    public enum BotState
    {
        Push,     // Walk toward enemy nexus, attack targets of opportunity
        Fight,    // Engage enemy hero in range
        Retreat,  // Walk back toward own nexus
        Recall,   // Triggering recall to base
        Shop,     // At base, buying items
        Dead      // Waiting for respawn
    }

    public BotState CurrentState { get; private set; } = BotState.Push;

    // ── References ────────────────────────────────────
    private PlayerController _player;
    private BotInputProvider _input;
    private ArcherySystem _archerySystem;
    private Stats _stats;
    private StatsService _statsService;

    // ── Decision State ────────────────────────────────
    private float _thinkTimer = 0f;
    private float _thinkInterval;
    private float _stateTimer = 0f;
    private int _itemsBought = 0;
    private Node3D _currentTarget;
    private Vector3 _laneDirection;   // World-space direction toward enemy nexus
    private Vector3 _retreatDirection; // Opposite
    private Vector3 _nexusPosition;   // Own team's nexus position
    private Vector3 _enemyNexusPosition;

    // ── Tuning (cached from difficulty) ───────────────
    private float _retreatHpPercent;
    private float _recallHpPercent;
    private float _engagementRange;

    public override void _Ready()
    {
        _player = GetParent<PlayerController>();
        if (_player == null)
        {
            GD.PrintErr("[HeroBrain] Must be a child of PlayerController!");
            QueueFree();
            return;
        }

        _input = _player.BotInput;
        if (_input == null)
        {
            GD.PrintErr("[HeroBrain] PlayerController has no BotInputProvider!");
            QueueFree();
            return;
        }

        // Cache difficulty parameters
        _retreatHpPercent = BotDifficultyProfile.GetRetreatHpPercent(Difficulty);
        _recallHpPercent = BotDifficultyProfile.GetRecallHpPercent(Difficulty);
        _engagementRange = BotDifficultyProfile.GetEngagementRange(Difficulty);
        _thinkInterval = (float)GD.RandRange(
            BotDifficultyProfile.GetReactionDelayMin(Difficulty),
            BotDifficultyProfile.GetReactionDelayMax(Difficulty)
        );

        // Defer heavy lookups until systems are initialized
        CallDeferred(nameof(DeferredInit));
    }

    private void DeferredInit()
    {
        // Find ArcherySystem on the player
        _archerySystem = _player.GetNodeOrNull<ArcherySystem>("ArcherySystem");
        if (_archerySystem != null)
        {
            _stats = _archerySystem.PlayerStats;
            _statsService = _archerySystem.PlayerStatsService;
        }

        // Resolve nexus positions
        ResolveLaneDirection();

        GD.Print($"[HeroBrain] Initialized: {HeroClass} ({Difficulty}) on team {_player.Team}");
    }

    private void ResolveLaneDirection()
    {
        // Try to find nexus positions from MobaGameManager
        if (MobaGameManager.Instance != null)
        {
            bool isRed = _player.Team == MobaTeam.Red;
            _nexusPosition = isRed ? MobaGameManager.Instance.RedSpawnPos : MobaGameManager.Instance.BlueSpawnPos;
            _enemyNexusPosition = isRed ? MobaGameManager.Instance.BlueSpawnPos : MobaGameManager.Instance.RedSpawnPos;
        }
        else
        {
            // Fallback: find spawn points in scene
            string ownSpawnName = $"SpawnPoint_{_player.Team}";
            string enemyTeam = _player.Team == MobaTeam.Red ? "Blue" : "Red";
            string enemySpawnName = $"SpawnPoint_{enemyTeam}";

            var ownSpawn = GetTree().CurrentScene.FindChild(ownSpawnName, true, false) as Node3D;
            var enemySpawn = GetTree().CurrentScene.FindChild(enemySpawnName, true, false) as Node3D;

            _nexusPosition = ownSpawn?.GlobalPosition ?? Vector3.Zero;
            _enemyNexusPosition = enemySpawn?.GlobalPosition ?? new Vector3(0, 0, 50);
        }

        // Lane direction: normalized vector from own nexus to enemy nexus
        Vector3 diff = _enemyNexusPosition - _nexusPosition;
        diff.Y = 0;
        _laneDirection = diff.Normalized();
        _retreatDirection = -_laneDirection;
    }

    // ══════════════════════════════════════════════════════
    //  MAIN LOOP
    // ══════════════════════════════════════════════════════

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || _stats == null) return;

        float dt = (float)delta;
        _stateTimer += dt;
        _thinkTimer += dt;

        // Only make decisions at the think rate (simulates reaction time)
        if (_thinkTimer < _thinkInterval) return;
        _thinkTimer = 0f;

        // Randomize next think interval slightly
        _thinkInterval = (float)GD.RandRange(
            BotDifficultyProfile.GetReactionDelayMin(Difficulty),
            BotDifficultyProfile.GetReactionDelayMax(Difficulty)
        );

        // Clear previous frame's input
        _input.Clear();

        // Evaluate state transitions
        EvaluateState();

        // Execute current state behavior
        switch (CurrentState)
        {
            case BotState.Push: ExecutePush(); break;
            case BotState.Fight: ExecuteFight(); break;
            case BotState.Retreat: ExecuteRetreat(); break;
            case BotState.Recall: ExecuteRecall(); break;
            case BotState.Shop: ExecuteShop(); break;
            case BotState.Dead:    /* wait */        break;
        }
    }

    // ══════════════════════════════════════════════════════
    //  STATE EVALUATION
    // ══════════════════════════════════════════════════════

    private void EvaluateState()
    {
        // Dead check
        if (_player.CurrentState == PlayerState.Dead)
        {
            TransitionTo(BotState.Dead);
            return;
        }

        // Just respawned — go shop if near nexus, otherwise push
        if (CurrentState == BotState.Dead && _player.CurrentState != PlayerState.Dead)
        {
            TransitionTo(IsNearOwnNexus() ? BotState.Shop : BotState.Push);
            return;
        }

        float hpPercent = GetHpPercent();

        // Critical HP: recall
        if (hpPercent < _recallHpPercent && CurrentState != BotState.Recall && CurrentState != BotState.Shop)
        {
            TransitionTo(BotState.Retreat);
            return;
        }

        // Low HP: retreat
        if (hpPercent < _retreatHpPercent && CurrentState == BotState.Push)
        {
            TransitionTo(BotState.Retreat);
            return;
        }

        // Retreating and safe enough to recall
        if (CurrentState == BotState.Retreat && hpPercent < _recallHpPercent && !IsEnemyNearby(8f))
        {
            TransitionTo(BotState.Recall);
            return;
        }

        // Retreating and reached nexus — shop
        if (CurrentState == BotState.Retreat && IsNearOwnNexus())
        {
            TransitionTo(BotState.Shop);
            return;
        }

        // Shopping done — push
        if (CurrentState == BotState.Shop && _stateTimer > 1.5f)
        {
            TransitionTo(BotState.Push);
            return;
        }

        // Recall complete (at nexus) — shop
        if (CurrentState == BotState.Recall && IsNearOwnNexus())
        {
            TransitionTo(BotState.Shop);
            return;
        }

        // Pushing and enemy hero nearby — fight
        if (CurrentState == BotState.Push)
        {
            var enemyHero = FindNearestEnemyHero();
            if (enemyHero != null && DistanceTo(enemyHero) < _engagementRange)
            {
                _currentTarget = enemyHero;
                TransitionTo(BotState.Fight);
                return;
            }
        }

        // Fighting but target is gone — push
        if (CurrentState == BotState.Fight)
        {
            if (_currentTarget == null || !IsInstanceValid(_currentTarget) || TargetingHelper.IsTargetDead(_currentTarget))
            {
                _currentTarget = null;
                TransitionTo(BotState.Push);
                return;
            }

            // Target fled out of range
            if (DistanceTo(_currentTarget) > _engagementRange * 1.5f)
            {
                _currentTarget = null;
                TransitionTo(BotState.Push);
                return;
            }
        }

        // HP recovered while retreating — push again
        if (CurrentState == BotState.Retreat && hpPercent > _retreatHpPercent + 0.1f)
        {
            TransitionTo(BotState.Push);
        }
    }

    private void TransitionTo(BotState newState)
    {
        if (newState == CurrentState) return;
        GD.Print($"[HeroBrain] {_player.Name} ({HeroClass}): {CurrentState} → {newState}");
        CurrentState = newState;
        _stateTimer = 0f;
    }

    // ══════════════════════════════════════════════════════
    //  STATE BEHAVIORS
    // ══════════════════════════════════════════════════════

    private void ExecutePush()
    {
        // Walk down the lane toward enemy nexus
        _input.MoveDirection = _laneDirection;

        // Attack any nearby enemy (creep or hero)
        var target = FindNearestEnemy();
        if (target != null && DistanceTo(target) < _engagementRange)
        {
            _input.DesiredTarget = target;
            _input.WantAttackPress = true;

            // Face target
            Vector3 toTarget = (target.GlobalPosition - _player.GlobalPosition).Normalized();
            toTarget.Y = 0;
            _input.LookDirection = toTarget;
        }

        // Use abilities off cooldown (Beginner style)
        TryUseAbility();
    }

    private void ExecuteFight()
    {
        if (_currentTarget == null || !IsInstanceValid(_currentTarget)) return;

        float dist = DistanceTo(_currentTarget);

        // Move toward target if too far, stay if in range
        if (dist > _engagementRange * 0.7f)
        {
            Vector3 toTarget = (_currentTarget.GlobalPosition - _player.GlobalPosition).Normalized();
            toTarget.Y = 0;
            _input.MoveDirection = toTarget;
        }

        // Attack
        _input.DesiredTarget = _currentTarget;
        _input.WantAttackPress = true;

        // Face target
        Vector3 lookDir = (_currentTarget.GlobalPosition - _player.GlobalPosition).Normalized();
        lookDir.Y = 0;
        _input.LookDirection = lookDir;

        // Use abilities
        TryUseAbility();
    }

    private void ExecuteRetreat()
    {
        // Walk back toward own nexus
        Vector3 toNexus = (_nexusPosition - _player.GlobalPosition).Normalized();
        toNexus.Y = 0;
        _input.MoveDirection = toNexus;

        // Sprint if available
        _input.WantSprint = true;
    }

    private void ExecuteRecall()
    {
        // Stop moving and trigger recall
        _input.MoveDirection = Vector3.Zero;
        _input.WantRecall = true;
    }

    private void ExecuteShop()
    {
        // Don't move while shopping
        _input.MoveDirection = Vector3.Zero;

        // Buy next item from build path
        var buildOrder = BotItemBuild.GetBuildOrder(HeroClass);
        if (_itemsBought < buildOrder.Count)
        {
            string itemId = buildOrder[_itemsBought];
            var item = ItemData.Get(itemId);
            if (item != null && _stats.Gold >= item.GoldCost)
            {
                BuyItem(item);
                _itemsBought++;
                GD.Print($"[HeroBrain] {_player.Name} bought {item.Name} ({_itemsBought}/{buildOrder.Count})");
            }
        }
    }

    // ══════════════════════════════════════════════════════
    //  SHOPPING (replicate ShopUI.OnBuyPressed logic)
    // ══════════════════════════════════════════════════════

    private void BuyItem(ItemInfo item)
    {
        if (_statsService == null || _stats == null) return;

        // Deduct gold
        _statsService.AddGold(-item.GoldCost);

        // Add to inventory slot
        if (ToolManager.Instance != null)
        {
            var slots = ToolManager.Instance.InventorySlots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || string.IsNullOrEmpty(slots[i].DisplayName))
                {
                    slots[i] = new ToolItem(ToolType.None, item.Name, item.IconPath ?? "", item.Id ?? "");
                    break;
                }
            }
        }

        // Apply stat bonuses (same as ShopUI)
        _stats.Strength += item.Stats.Strength;
        _stats.Intelligence += item.Stats.Intelligence;
        _stats.Vitality += item.Stats.Vitality;
        _stats.Wisdom += item.Stats.Wisdom;
        _stats.Agility += item.Stats.Agility;
        _stats.Haste += item.Stats.Haste;
        _stats.Concentration += item.Stats.Concentration;
    }

    // ══════════════════════════════════════════════════════
    //  ABILITY USAGE
    // ══════════════════════════════════════════════════════

    private void TryUseAbility()
    {
        if (_player == null) return;

        // Cycle through abilities, pick the first one that's off cooldown
        int startSlot = (int)(GD.Randi() % 3); // Randomize starting slot each think
        for (int i = 0; i < 3; i++)
        {
            int slot = (startSlot + i) % 3;
            if (_player.IsAbilityReady(slot))
            {
                _input.WantAbility = slot;
                return; // Use only one per think cycle
            }
        }
    }

    // ══════════════════════════════════════════════════════
    //  HELPER: TARGET FINDING
    // ══════════════════════════════════════════════════════

    private Node3D FindNearestEnemy()
    {
        Node3D nearest = null;
        float nearestDist = float.MaxValue;

        // Check all targetable entities
        var targets = GetTree().GetNodesInGroup("targetables");
        foreach (var node in targets)
        {
            if (node is not Node3D n3d) continue;
            if (TargetingHelper.IsTargetDead(n3d)) continue;
            if (IsSameTeam(n3d)) continue;

            float dist = _player.GlobalPosition.DistanceTo(n3d.GlobalPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = n3d;
            }
        }

        return nearest;
    }

    private Node3D FindNearestEnemyHero()
    {
        Node3D nearest = null;
        float nearestDist = float.MaxValue;

        var players = GetTree().GetNodesInGroup("Players");
        foreach (var node in players)
        {
            if (node is not PlayerController pc) continue;
            if (pc == _player) continue;
            if (pc.Team == _player.Team) continue;
            if (pc.CurrentState == PlayerState.Dead) continue;

            float dist = _player.GlobalPosition.DistanceTo(pc.GlobalPosition);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = pc;
            }
        }

        return nearest;
    }

    private bool IsSameTeam(Node3D target)
    {
        if (target is PlayerController pc) return pc.Team == _player.Team;

        // Check MobaMinion / MobaTower team groups
        string ownTeamGroup = $"team_{_player.Team.ToString().ToLower()}";
        return target.IsInGroup(ownTeamGroup);
    }

    // ══════════════════════════════════════════════════════
    //  HELPER: NAVIGATION
    // ══════════════════════════════════════════════════════

    private float DistanceTo(Node3D target)
    {
        return _player.GlobalPosition.DistanceTo(target.GlobalPosition);
    }

    private float GetHpPercent()
    {
        if (_stats == null || _stats.MaxHealth <= 0) return 1f;
        return (float)_stats.CurrentHealth / _stats.MaxHealth;
    }

    private bool IsNearOwnNexus()
    {
        return _player.GlobalPosition.DistanceTo(_nexusPosition) < 15f;
    }

    private bool IsEnemyNearby(float range)
    {
        var nearest = FindNearestEnemy();
        return nearest != null && DistanceTo(nearest) < range;
    }
}
