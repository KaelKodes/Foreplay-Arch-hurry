using Godot;
using System;

namespace Archery;

public partial class PlayerController
{
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsLocal) return;

        var toolManager = ToolManager.Instance;
        bool isRPGMode = toolManager != null && toolManager.CurrentMode == ToolManager.HotbarMode.RPG;

        if (@event is InputEventKey homeKey && homeKey.Pressed && !homeKey.Echo && homeKey.Keycode == Key.Home)
        {
            if (_archerySystem != null)
            {
                Vector3 teePos = _archerySystem.SpawnPosition;
                // Teleport slightly behind and above the tee
                Vector3 offset = new Vector3(0, 0, -1.0f); // Face down-range (+Z)
                TeleportTo(teePos + offset, teePos + Vector3.Forward * 10.0f);
                GD.Print("PlayerController: Home teleport to Tee.");
            }
        }

        // M key: Cycle character model
        if (@event is InputEventKey modelKey && modelKey.Pressed && !modelKey.Echo && modelKey.Keycode == Key.M)
        {
            CycleCharacterModel();
        }

        // F key: Clear Target
        if (isRPGMode && @event is InputEventKey fKey && fKey.Pressed && !fKey.Echo && fKey.Keycode == Key.F)
        {
            _hardLockTarget = null;
            _archerySystem?.ClearTarget();
            GetViewport().SetInputAsHandled();
        }

        // Selection logic in Build Mode
        if (CurrentState == PlayerState.BuildMode)
        {
            HandleBuildModeSelection(@event);
        }

        // Combat Input Handling (Hold to Charge, Release to Shoot/Swing)
        if (@event is InputEventMouseButton attackBtn && attackBtn.ButtonIndex == MouseButton.Left)
        {
            HandleAttackInput(attackBtn, isRPGMode);
        }

        if (isRPGMode && @event is InputEventMouseButton rightBtn && rightBtn.ButtonIndex == MouseButton.Right && rightBtn.Pressed)
        {
            // NEW: Lock onto fluid target if available, otherwise fallback to raycast
            Node3D target = _fluidTarget ?? CheckUnitRaycast();

            if (target != null)
            {
                _hardLockTarget = target;
                if (_archerySystem != null) _archerySystem.SetTarget(_hardLockTarget);
                GD.Print($"[PlayerController] Right-Click Lock: {target.Name}");

                // NEW: Interaction Trigger on Right Click (Simplified System)
                if (target is InteractableObject io)
                {
                    io.OnInteract(this);
                }

                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsLocal) return;

        if (@event is InputEventKey key)
        {
            if (key.Keycode == Key.Alt)
            {
                _isAltPressed = key.Pressed;
                UpdateMouseCapture();
            }
        }
    }

    private void UpdateMouseCapture()
    {
        if (!IsLocal) return;

        bool shouldCapture = false;
        if (CurrentState == PlayerState.WalkMode || CurrentState == PlayerState.CombatMelee || CurrentState == PlayerState.CombatArcher)
        {
            if (!_isAltPressed && (GetViewport() == null || GetViewport().GuiGetFocusOwner() == null))
            {
                shouldCapture = true;
            }
        }

        if (shouldCapture != _isMouseCaptured)
        {
            _isMouseCaptured = shouldCapture;
            Input.MouseMode = shouldCapture ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        }
    }

    private void HandleBuildModeSelection(InputEvent @event)
    {
        // Mouse Wheel actions
        if (_selectedObject != null && @event is InputEventMouseButton mbScroll && mbScroll.Pressed)
        {
            bool isShift = mbScroll.ShiftPressed;
            bool isCtrl = mbScroll.CtrlPressed;

            if (mbScroll.ButtonIndex == MouseButton.WheelUp)
            {
                if (isCtrl)
                    _selectedObject.Scale *= 1.1f;
                else if (isShift)
                    _selectedObject.GlobalPosition += new Vector3(0, 0.25f, 0);
                else
                    _selectedObject.RotateY(Mathf.DegToRad(15.0f));

                GetViewport().SetInputAsHandled();
            }
            else if (mbScroll.ButtonIndex == MouseButton.WheelDown)
            {
                if (isCtrl)
                    _selectedObject.Scale *= 0.9f;
                else if (isShift)
                    _selectedObject.GlobalPosition -= new Vector3(0, 0.25f, 0);
                else
                    _selectedObject.RotateY(Mathf.DegToRad(-15.0f));

                GetViewport().SetInputAsHandled();
            }
        }

        // Mouse Drag Rotation
        if (_selectedObject != null && @event is InputEventMouseMotion mm && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            float rotSpeed = 0.5f;
            _selectedObject.RotateY(Mathf.DegToRad(mm.Relative.X * rotSpeed));
        }

        // Select on Click
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            if (_hud == null || _hud.CurrentTool == MainHUDController.BuildTool.Selection)
            {
                InteractableObject clickedObj = CheckInteractableRaycast();
                if (clickedObj != _selectedObject)
                {
                    if (_selectedObject != null) _selectedObject.SetSelected(false);
                    _selectedObject = clickedObj;
                    if (_selectedObject != null) _selectedObject.SetSelected(true);
                }
            }
        }
    }

    private void HandleAttackInput(InputEventMouseButton attackBtn, bool isRPGMode)
    {
        if (attackBtn.Pressed)
        {
            bool isRangerMatch = CurrentModelId.ToLower() == "ranger";

            if (isRPGMode && isRangerMatch && !_isChargingAttack)
            {
                if (_archerySystem != null && _archerySystem.StartCharge())
                {
                    _isChargingAttack = true;
                    _attackHoldTimer = 0f;
                }
            }
            else if (isRPGMode && !isRangerMatch && !_isChargingAttack)
            {
                // Hold-to-charge melee attacks (same as CombatMelee path)
                if (_meleeSystem != null && _meleeSystem.StartCharge())
                {
                    _isChargingAttack = true;
                    _attackHoldTimer = 0f;
                }
            }
            else if ((CurrentState == PlayerState.CombatMelee || CurrentState == PlayerState.CombatArcher) && !_isChargingAttack)
            {
                if (CurrentState == PlayerState.CombatMelee && _meleeSystem != null)
                {
                    if (_meleeSystem.StartCharge())
                    {
                        _isChargingAttack = true;
                        _attackHoldTimer = 0f;
                    }
                }
                else if (CurrentState == PlayerState.CombatArcher && _archerySystem != null)
                {
                    if (_archerySystem.StartCharge())
                    {
                        _isChargingAttack = true;
                        _attackHoldTimer = 0f;
                    }
                }
            }
        }
        else // Released
        {
            if (_isChargingAttack)
            {
                _isChargingAttack = false;
                float finalHoldTime = _attackHoldTimer;
                _attackHoldTimer = 0f;

                bool isRangerRelease = CurrentModelId.ToLower() == "ranger";
                bool isRPGMelee = isRPGMode && !isRangerRelease && _meleeSystem != null;

                if ((CurrentState == PlayerState.CombatMelee || isRPGMelee) && _meleeSystem != null)
                    _meleeSystem.ExecuteAttack(finalHoldTime);
                else if ((CurrentState == PlayerState.CombatArcher || (isRPGMode && isRangerRelease)) && _archerySystem != null)
                    _archerySystem.ExecuteAttack(finalHoldTime);

                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void HandleBuildModeInput(double delta)
    {
        HandleBodyMovement(delta);

        if (_hud != null && _hud.CurrentTool == MainHUDController.BuildTool.Selection)
        {
            if (_selectedObject != null)
            {
                _archerySystem?.SetPrompt(true, $"SELECTED: {_selectedObject.ObjectName} | X: DELETE | C: REPOSITION");
            }
            else
            {
                InteractableObject hoverObj = CheckInteractableRaycast();
                if (hoverObj != null)
                {
                    _archerySystem?.SetPrompt(true, $"CLICK TO SELECT {hoverObj.ObjectName}");
                }
            }
        }
    }

    private void HandleCombatInput(double delta)
    {
        if (_camera == null || _archerySystem == null) return;
        if (_archerySystem.CurrentStage == Archery.DrawStage.Executing) return;

        // Face the target or camera direction
        Vector3 targetDir;
        if (_archerySystem.CurrentTarget != null)
        {
            targetDir = (_archerySystem.CurrentTarget.GlobalPosition - GlobalPosition).Normalized();
        }
        else
        {
            targetDir = -_camera.GlobalTransform.Basis.Z;
        }
        targetDir.Y = 0;

        if (targetDir.LengthSquared() > 0.01f)
        {
            float targetAngle = Mathf.Atan2(-targetDir.X, -targetDir.Z) + Mathf.Pi;
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, 10.0f * (float)delta), 0);
        }

    }

}
