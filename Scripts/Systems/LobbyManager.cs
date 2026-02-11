using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Archery;

namespace Archery;

public class LobbyPlayerData
{
    public long Id { get; set; }
    public string Name { get; set; }
    public MobaTeam Team { get; set; } = MobaTeam.None;
    public string ClassName { get; set; } = "Ranger";
    public bool IsReady { get; set; } = false;
    public bool IsBot { get; set; } = false;
    public BotDifficulty BotDifficultyLevel { get; set; } = BotDifficulty.Beginner;
}

public partial class LobbyManager : Node
{
    private static LobbyManager _instance;
    public static LobbyManager Instance => _instance;

    [Signal] public delegate void PlayerListUpdatedEventHandler();

    private List<LobbyPlayerData> _players = new();
    private List<LobbyPlayerData> _bots = new();

    public override void _Ready()
    {
        _instance = this;
        GD.Print("[LobbyManager] Initialized.");
    }

    public List<LobbyPlayerData> GetPlayers()
    {
        var all = new List<LobbyPlayerData>(_players);
        all.AddRange(_bots);
        return all;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void AddPlayer(long id, string name)
    {
        if (!Multiplayer.IsServer())
        {
            RpcId(1, nameof(AddPlayer), id, name);
            return;
        }

        if (_players.Any(p => p.Id == id)) return;

        var player = new LobbyPlayerData { Id = id, Name = name, ClassName = "Ranger" };

        // Auto-assign team
        int redCount = GetPlayers().Count(p => p.Team == MobaTeam.Red);
        int blueCount = GetPlayers().Count(p => p.Team == MobaTeam.Blue);
        player.Team = redCount <= blueCount ? MobaTeam.Red : MobaTeam.Blue;

        _players.Add(player);

        GD.Print($"[LobbyManager] Player {name} ({id}) joined.");
        BroadcastState();
    }

    public void RemovePlayer(long id)
    {
        if (!Multiplayer.IsServer()) return;
        _players.RemoveAll(p => p.Id == id);
        BroadcastState();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void SwitchTeam(long playerId)
    {
        if (!Multiplayer.IsServer())
        {
            RpcId(1, nameof(SwitchTeam), playerId);
            return;
        }

        var player = GetPlayers().FirstOrDefault(p => p.Id == playerId);
        if (player == null) return;

        player.Team = player.Team == MobaTeam.Red ? MobaTeam.Blue : MobaTeam.Red;
        BroadcastState();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void CycleClass(long playerId)
    {
        if (!Multiplayer.IsServer())
        {
            RpcId(1, nameof(CycleClass), playerId);
            return;
        }

        var player = GetPlayers().FirstOrDefault(p => p.Id == playerId);
        if (player == null) return;

        var currentModel = CharacterRegistry.Instance.GetModel(player.ClassName);
        var nextModel = CharacterRegistry.Instance.GetNextModel(currentModel?.Id ?? "Ranger");
        player.ClassName = nextModel.Id;

        BroadcastState();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void AddBot(MobaTeam team, string className = "Warrior")
    {
        if (!Multiplayer.IsServer())
        {
            RpcId(1, nameof(AddBot), (int)team, className);
            return;
        }

        long botId = 1000 + _bots.Count;
        var bot = new LobbyPlayerData
        {
            Id = botId,
            Name = $"Bot {botId}",
            Team = team,
            ClassName = className,
            IsBot = true,
            BotDifficultyLevel = BotDifficulty.Beginner
        };
        _bots.Add(bot);
        BroadcastState();
    }

    private void BroadcastState()
    {
        if (!Multiplayer.IsServer()) return;
        Rpc(nameof(SyncLobbyState), SerializeLobbyState());
        EmitSignal(SignalName.PlayerListUpdated);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority)]
    private void SyncLobbyState(string stateJson)
    {
        var json = new Json();
        if (json.Parse(stateJson) != Error.Ok)
        {
            GD.PrintErr($"[LobbyManager] Failed to parse state JSON: {stateJson}");
            return;
        }

        var data = json.Data.AsGodotArray();
        _players.Clear();
        _bots.Clear();

        foreach (var item in data)
        {
            var d = item.AsGodotDictionary();
            var p = new LobbyPlayerData
            {
                Id = (long)d["id"],
                Name = (string)d["name"],
                Team = (MobaTeam)(int)d["team"],
                ClassName = (string)d["class"],
                IsBot = (bool)d["isBot"]
            };

            if (p.IsBot) _bots.Add(p);
            else _players.Add(p);
        }

        EmitSignal(SignalName.PlayerListUpdated);
    }

    private string SerializeLobbyState()
    {
        var list = new Godot.Collections.Array();
        foreach (var p in GetPlayers())
        {
            var d = new Godot.Collections.Dictionary();
            d["id"] = p.Id;
            d["name"] = p.Name;
            d["team"] = (int)p.Team;
            d["class"] = p.ClassName;
            d["isBot"] = p.IsBot;
            list.Add(d);
        }
        return Json.Stringify(list);
    }

    public void ClearLobby()
    {
        if (Multiplayer.IsServer())
        {
            _players.Clear();
            _bots.Clear();
            BroadcastState();
        }
    }
}
