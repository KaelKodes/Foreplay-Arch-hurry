using Godot;
using System;

namespace Archery;

public partial class PlayerController
{
    private void EnterVehicle(GolfCart cart)
    {
        _currentCart = cart;
        _currentCart.Enter(this);
        CurrentState = PlayerState.DriveMode;
        Visible = false;

        if (_camera != null)
        {
            _camera.SetTarget(_currentCart, true);
        }
        if (_archerySystem != null) _archerySystem.SetPrompt(false);
    }

    private void ExitVehicle()
    {
        if (_currentCart == null) return;

        _currentCart.Exit();
        CurrentState = PlayerState.WalkMode;
        Visible = true;

        GlobalPosition = _currentCart.GlobalPosition + _currentCart.Transform.Basis.X * 2.0f;

        if (_camera != null)
        {
            _camera.SetTarget(this, true);
        }
        _currentCart = null;
    }

    private void HandleVehicleDetection()
    {
        GolfCart nearestCart = PlayerInteraction.FindNearestCart(GetTree(), GlobalPosition, 3.0f);

        if (nearestCart != null)
        {
            if (_archerySystem != null) _archerySystem.SetPrompt(true, "PRESS E TO DRIVE");
            if (Input.IsKeyPressed(Key.E))
            {
                EnterVehicle(nearestCart);
            }
        }
    }
}
