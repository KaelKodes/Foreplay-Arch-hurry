using Godot;
using System;
using System.Collections.Generic;

namespace Archery;

public partial class ArcherySystem
{
    public void PrepareNextShot()
    {
        // Only the authority (the player themselves) should trigger a new arrow spawn.
        // Other peers will receive the arrow via replication (OnArrowSpawned).
        if (_currentPlayer != null && !_currentPlayer.IsLocal) return;

        var toolManager = ToolManager.Instance;
        bool isRPG = toolManager != null && toolManager.CurrentMode == ToolManager.HotbarMode.RPG;

        if (ArrowCount <= 0 && MobaGameManager.Instance == null && !isRPG)
        {
            SetPrompt(true, "Out of Arrows!");
            return;
        }

        if (_arrow != null)
        {
            if (!_arrow.HasBeenShot)
            {
                _arrow.QueueFree();
            }
            _arrow = null;
        }

        // Increment ticket for unique naming
        _shotTicket++;

        // Spawn new arrow (Networked)
        if (ArrowScene != null)
        {
            // If Singleplayer, do old logic
            if (Multiplayer.MultiplayerPeer == null || Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
            {
                _arrow = ArrowScene.Instantiate<ArrowController>();
                GetTree().CurrentScene.AddChild(_arrow);
                SetupArrow(_arrow);
            }
            else
            {
                // Multiplayer:
                if (_currentPlayer != null)
                {
                    if (Multiplayer.IsServer())
                    {
                        // If we are the server, just spawn it directly.
                        SpawnNetworkedArrow(int.Parse(_currentPlayer.Name.ToString()), _shotTicket);
                    }
                    else
                    {
                        // Client: Request Server to spawn arrow for us
                        RpcId(1, nameof(RequestSpawnArrow), int.Parse(_currentPlayer.Name.ToString()), _shotTicket);
                    }
                }
            }
        }

        _stage = DrawStage.Idle;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;

        if (_camera != null) { _camera.SetTarget(_currentPlayer, false); _camera.SetFollowing(false); _camera.SetFreeLook(false); }

        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
        EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);

        UpdateArrowLabel();
    }

    public void CancelDraw()
    {
        _stage = DrawStage.Idle;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;
        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
        EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);
    }

    public void ResetMatch()
    {
        // Refund current/last shot if it was just completed
        if (_stage == DrawStage.ShotComplete || _stage == DrawStage.Executing)
        {
            ArrowCount++;
        }

        _stage = DrawStage.Idle;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;

        // Clean up previous arrow and prepare a fresh one
        PrepareNextShot();

        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
        EmitSignal(SignalName.ArcheryValuesUpdated, 0, -1, -1);
    }

    private float CalculateOptimalLoft(Vector3 start, Vector3 target, float velocity)
    {
        return TargetingHelper.CalculateOptimalLoft(start, target, velocity, ArcheryConstants.GRAVITY);
    }

    public bool StartCharge()
    {
        if (_bowCooldownRemaining > 0) return false;

        _stage = DrawStage.Drawing;
        _timer = 0.0f;
        _isReturnPhase = false;
        _lockedPower = -1.0f;
        _lockedAccuracy = -1.0f;
        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
        return true;
    }

    public void UpdateChargeProgress(float percent)
    {
        EmitSignal(SignalName.ArcheryValuesUpdated, percent, -1, -1);
    }

    public void ExecuteAttack(float holdTime)
    {
        var toolManager = ToolManager.Instance;
        bool isRPG = toolManager != null && toolManager.CurrentMode == ToolManager.HotbarMode.RPG;

        if (_stage != DrawStage.Drawing) return;

        // Map hold duration to power (Calibrated for RPG Mode to match abilities)
        float finalPower = isRPG ? 25f : 50f;
        if (holdTime >= 2.5f) finalPower = isRPG ? 35f : 200f;
        else if (holdTime >= 1.5f) finalPower = isRPG ? 32f : 150f;
        else if (holdTime >= 0.75f) finalPower = isRPG ? 28f : 100f;


        _lockedPower = finalPower;
        _lockedAccuracy = isRPG ? ArcheryConstants.PERFECT_ACCURACY_VALUE : 100f;
        _stage = DrawStage.Executing;

        if (isRPG) _isNextShotFlat = true;

        EmitSignal(SignalName.ArcheryValuesUpdated, _lockedPower, _lockedPower, _lockedAccuracy);
        EmitSignal(SignalName.DrawStageChanged, (int)_stage);

        ExecuteShot();
    }

    public void QuickFire(float holdTime)
    {
        var toolManager = ToolManager.Instance;
        bool isRPG = toolManager != null && toolManager.CurrentMode == ToolManager.HotbarMode.RPG;

        if (ArrowCount <= 0 && MobaGameManager.Instance == null && !isRPG) return;

        // Ensure we have a fresh arrow ready
        if (_arrow == null || _arrow.HasBeenShot)
        {
            PrepareNextShot();
        }

        if (_arrow == null) return;

        _lockedPower = 30.0f; // Calibrated for RPG mode visibility (reduced from 85)
        _lockedAccuracy = ArcheryConstants.PERFECT_ACCURACY_VALUE; // Perfect center (fix for "firing to the right")

        _stage = DrawStage.Drawing;
        _forcedDrawTime = 0.25f; // Short mandatory draw for animation snappy-ness

        if (isRPG) _isNextShotFlat = true;

        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
        // ExecuteShot() is now called by _Process when _forcedDrawTime reaches 0
    }

    public void PlayAbilityAnimation(bool skipArrow = false)
    {
        _shouldSkipArrow = skipArrow;
        _stage = DrawStage.Drawing;
        _forcedDrawTime = 0.25f;
        EmitSignal(SignalName.DrawStageChanged, (int)_stage);
    }

    public void SetNextShotPiercing(bool enabled)
    {
        _isNextShotPiercing = enabled;
    }

    public void SetNextShotFlat(bool enabled)
    {
        _isNextShotFlat = enabled;
    }

    private void ExecuteShot()
    {
        var toolManager = ToolManager.Instance;
        bool isRPG = toolManager != null && toolManager.CurrentMode == ToolManager.HotbarMode.RPG;

        // 1. Power Calculation (Incorporate Stats + Locked Power)
        float powerFactor = _lockedPower / 100.0f;
        float powerStatMult = PlayerStats.Strength / 10.0f;

        // Apply Forgiveness (Snap to perfect)
        if (Mathf.Abs(_lockedPower - ArcheryConstants.PERFECT_POWER_VALUE) < ArcheryConstants.TOLERANCE_POWER)
        {
            _lockedPower = ArcheryConstants.PERFECT_POWER_VALUE;
            powerFactor = _lockedPower / 100.0f;
        }

        float velocityMag = ArcheryConstants.BASE_VELOCITY * powerFactor * powerStatMult;

        // 2. Accuracy Calculation
        float accuracyError = _lockedAccuracy - ArcheryConstants.PERFECT_ACCURACY_VALUE;

        // Apply Forgiveness
        if (Mathf.Abs(accuracyError) < ArcheryConstants.TOLERANCE_ACCURACY)
        {
            accuracyError = 0.0f;
            _lockedAccuracy = ArcheryConstants.PERFECT_ACCURACY_VALUE;
        }

        // Power Multiplier for Error
        if (_lockedPower > ArcheryConstants.PERFECT_POWER_VALUE)
        {
            float overPower = _lockedPower - ArcheryConstants.PERFECT_POWER_VALUE;
            accuracyError *= (1.0f + overPower * 0.15f);
        }

        // --- NEW: Force Perfect for Locked Targets ---
        if (CurrentTarget != null)
        {
            accuracyError = 0.0f;
            _lockedAccuracy = ArcheryConstants.PERFECT_ACCURACY_VALUE;
        }

        if (_arrow != null)
        {
            if (_shouldSkipArrow)
            {
                GD.Print($"[ArcherySystem] Executing shot WITHOUT physical arrow. Skip: {_shouldSkipArrow}");
            }
            else
            {
                Vector3 launchDir;
                if (CurrentTarget != null)
                {
                    // Snap aiming to target
                    Vector3 targetPos = CurrentTarget.GlobalPosition;
                    if (CurrentTarget is InteractableObject io)
                    {
                        targetPos = io.GlobalPosition + new Vector3(0, 1.0f, 0);
                    }
                    launchDir = (targetPos - _arrow.GlobalPosition).Normalized();
                }
                else
                {
                    if (isRPG && _currentPlayer != null && _camera != null)
                    {
                        // NEW: For flat shots without a target, follow crosshair (camera forward) exactly
                        if (_isNextShotFlat)
                            launchDir = -_camera.GlobalBasis.Z;
                        else
                        {
                            launchDir = _currentPlayer.GlobalBasis.Z;
                            launchDir.Y = 0;
                            launchDir = launchDir.Normalized();
                        }
                    }
                    else
                    {
                        Vector3 camFwd = -_camera.GlobalBasis.Z;
                        launchDir = (_camera != null) ? new Vector3(camFwd.X, 0, camFwd.Z).Normalized() : Vector3.Forward;
                    }
                }

                // Apply Loft
                float loftDeg = 12.0f;
                bool skipLoftOverride = false;

                if (_isNextShotFlat)
                {
                    loftDeg = 0.0f; // Perfect direct line (for targeted shots)
                    skipLoftOverride = true; // For no-target crosshair shots, keep the camera Y
                }
                else if (CurrentTarget != null)
                {
                    Vector3 targetPos = CurrentTarget.GlobalPosition;
                    if (CurrentTarget is InteractableObject io) targetPos += new Vector3(0, 1.0f, 0);

                    loftDeg = CalculateOptimalLoft(_arrow.GlobalPosition, targetPos, velocityMag);
                }
                else
                {
                    switch (_currentMode)
                    {
                        case ArcheryShotMode.Standard: loftDeg = 12.0f; break;
                        case ArcheryShotMode.Long: loftDeg = 25.0f; break;
                        case ArcheryShotMode.Max: loftDeg = 45.0f; break;
                    }
                }

                if (!skipLoftOverride)
                {
                    float loftRad = Mathf.DegToRad(loftDeg);
                    launchDir.Y = Mathf.Sin(loftRad);
                    launchDir = launchDir.Normalized();
                }

                // Apply Accuracy Deviation
                float rotationDeg = -accuracyError * 0.75f;
                launchDir = launchDir.Rotated(Vector3.Up, Mathf.DegToRad(rotationDeg));

                // Apply Wind
                if (_windSystem != null && _windSystem.IsWindEnabled)
                {
                    Vector3 wind = _windSystem.WindDirection * _windSystem.WindSpeedMph;
                    _arrow.SetWind(wind);
                }
                else if (_arrow != null)
                {
                    _arrow.SetWind(Vector3.Zero);
                }

                if (Multiplayer.MultiplayerPeer != null && !Multiplayer.IsServer())
                {
                    RpcId(1, nameof(RequestLaunchArrow), _arrow.Name, _arrow.GlobalPosition, _arrow.GlobalRotation, launchDir * velocityMag, Vector3.Zero, _isNextShotPiercing);
                }
                else
                {
                    if (Multiplayer.MultiplayerPeer != null)
                    {
                        _arrow.Rpc(nameof(ArrowController.Launch), _arrow.GlobalPosition, _arrow.GlobalRotation, launchDir * velocityMag, Vector3.Zero, _isNextShotPiercing);
                    }
                    else
                    {
                        _arrow.Launch(_arrow.GlobalPosition, _arrow.GlobalRotation, launchDir * velocityMag, Vector3.Zero, _isNextShotPiercing);
                    }
                }
            }

            EmitSignal(SignalName.ShotResult, _lockedPower, _lockedAccuracy);

            if (MobaGameManager.Instance == null && !isRPG)
            {
                ArrowCount--;
                UpdateArrowLabel();
            }
            _stage = DrawStage.ShotComplete;
            EmitSignal(SignalName.DrawStageChanged, (int)_stage);

            // Reset flags
            _isNextShotPiercing = false;
            _isNextShotFlat = false;
            _shouldSkipArrow = false;

            PrepareNextShot();

            _bowCooldownRemaining = BowCooldownTime;
            EmitSignal(SignalName.BowCooldownUpdated, _bowCooldownRemaining, BowCooldownTime);
        }
    }
}
