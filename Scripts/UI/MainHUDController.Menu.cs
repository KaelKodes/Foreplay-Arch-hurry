using Godot;
using System;

namespace Archery;

public partial class MainHUDController
{
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") || (@event is InputEventKey k && k.Pressed && k.Keycode == Key.Escape))
        {
            TogglePauseMenu();
        }
    }

    private void TogglePauseMenu()
    {
        SetPauseMenuVisible(!_pauseMenu.Visible);
    }

    private void SetPauseMenuVisible(bool visible)
    {
        if (_pauseMenu == null) return;
        _pauseMenu.Visible = visible;

        if (visible)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            // Update Host button visibility
            _hostBtn.Visible = (Multiplayer.MultiplayerPeer == null);
        }
        else
        {
            if (_player != null) Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private void OnHostMidGamePressed()
    {
        GD.Print("MainHUD: Hosting from active session...");
        NetworkManager.Instance.HostActiveGame();
        _hostBtn.Visible = false;
        SetPauseMenuVisible(false);
    }

    private void OnExitToMenuPressed()
    {
        GD.Print("MainHUD: Exiting to Main Menu...");
        // NetworkManager handles scene change AND peer cleanup
        NetworkManager.Instance.ReturnToMainMenu();
    }
}
