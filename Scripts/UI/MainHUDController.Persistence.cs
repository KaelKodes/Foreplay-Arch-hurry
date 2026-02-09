using Godot;
using System;

namespace Archery;

public partial class MainHUDController
{
    private void SetupFileDialogs()
    {
        _saveDialog = new FileDialog();
        _saveDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
        _saveDialog.Access = FileDialog.AccessEnum.Userdata;
        _saveDialog.Filters = new string[] { "*.json" };
        _saveDialog.CurrentDir = "user://courses";
        _saveDialog.FileSelected += OnSaveFileSelected;
        AddChild(_saveDialog);

        _loadDialog = new FileDialog();
        _loadDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        _loadDialog.Access = FileDialog.AccessEnum.Userdata;
        _loadDialog.Filters = new string[] { "*.json" };
        _loadDialog.CurrentDir = "user://courses";
        _loadDialog.FileSelected += OnLoadFileSelected;
        AddChild(_loadDialog);
    }

    public void ShowSaveLoadMenu()
    {
        if (_saveDialog == null) SetupFileDialogs();

        var popup = new PopupMenu();
        popup.AddItem("Save Course");
        popup.AddItem("Load Course");
        popup.IdPressed += (id) =>
        {
            if (id == 0) _saveDialog.PopupCentered(new Vector2I(600, 400));
            else _loadDialog.PopupCentered(new Vector2I(600, 400));
        };

        AddChild(popup);
        popup.PopupCentered(new Vector2I(200, 100));
    }

    private void OnSaveFileSelected(string path)
    {
        string filename = System.IO.Path.GetFileNameWithoutExtension(path);
        var terrain = GetTree().GetFirstNodeInGroup("terrain") as HeightmapTerrain;
        var root = GetTree().CurrentScene;

        CoursePersistenceManager.Instance.SaveCourse(filename, terrain, root);

        if (_archerySystem != null)
        {
            _archerySystem.SetPrompt(true, $"SAVED: {filename}");
            GetTree().CreateTimer(2.0f).Connect("timeout", Callable.From(() => _archerySystem.SetPrompt(false)));
        }
    }

    private void OnLoadFileSelected(string path)
    {
        string filename = System.IO.Path.GetFileNameWithoutExtension(path);
        var terrain = GetTree().GetFirstNodeInGroup("terrain") as HeightmapTerrain;
        var root = GetTree().CurrentScene;

        bool success = CoursePersistenceManager.Instance.LoadCourse(filename, terrain, root);

        if (_archerySystem != null)
        {
            _archerySystem.SetPrompt(success ? true : true, success ? $"LOADED: {filename}" : $"FAILED TO LOAD: {filename}");
            GetTree().CreateTimer(2.0f).Connect("timeout", Callable.From(() => _archerySystem.SetPrompt(false)));
        }
    }
}
