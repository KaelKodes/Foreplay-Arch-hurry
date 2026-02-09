using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Archery;

public partial class NetworkManager
{
    private Node _worldObjectsContainer;
    private MultiplayerSpawner _objectSpawner;

    public async void LevelLoaded(Node root)
    {
        string levelPath = root.GetPath().ToString();
        if (_isLevelLoaded && _currentLevelPath == levelPath)
        {
            GD.Print($"NetworkManager: Level {root.Name} already initialized, skipping.");
            return;
        }

        GD.Print($"NetworkManager: Level {root.Name} loaded.");
        _isLevelLoaded = true;
        _currentLevelPath = levelPath;

        _spawnedPlayerIds.Clear();

        GD.Print($"NetworkManager: Level Loaded ({root.Name}). Waiting for physics bake...");

        await ToSignal(GetTree(), "physics_frame");
        await ToSignal(GetTree(), "physics_frame");
        await ToSignal(GetTree(), "physics_frame");

        SetupObjectSpawner();
        SetupTerrainSpawner();

        if (Multiplayer.IsServer())
        {
            if (GetTree().CurrentScene.Name == "Lobby")
            {
                GD.Print("NetworkManager: Lobby loaded. Waiting for players to join...");
                LobbyManager.Instance.AddPlayer(1, "Host");
                return;
            }

            var lobbyData = LobbyManager.Instance.GetPlayers().FirstOrDefault(p => p.Id == 1);
            MobaTeam hostTeam = lobbyData?.Team ?? SelectedTeam;
            string hostClass = lobbyData?.ClassName ?? "Ranger";
            SpawnPlayer(1, hostTeam, hostClass);
        }
        else
        {
            if (GetTree().CurrentScene.Name == "Lobby")
            {
                GD.Print("NetworkManager: Client Lobby loaded.");
                return;
            }

            GD.Print($"NetworkManager: Client Ready. Sending Spawn Request...");
            var lobbyData = LobbyManager.Instance.GetPlayers().FirstOrDefault(p => p.Id == Multiplayer.GetUniqueId());
            MobaTeam clientTeam = lobbyData?.Team ?? SelectedTeam;
            string clientClass = lobbyData?.ClassName ?? "Ranger";
            RpcId(1, nameof(NotifyClientReady), (int)clientTeam, clientClass);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void NotifyClientReady(int teamInt, string className)
    {
        if (!Multiplayer.IsServer()) return;

        MobaTeam team = (MobaTeam)teamInt;
        long senderId = Multiplayer.GetRemoteSenderId();
        GD.Print($"NetworkManager: Received ClientReady from ID {senderId} with Team {team}. Spawning...");

        if (_players.ContainsKey(senderId))
        {
            GD.Print($"NetworkManager: Player {senderId} already exists. Ignoring duplicate spawn request.");
            return;
        }

        SpawnPlayer(senderId, team, className);
        SyncTerrainToClient(senderId);
        SyncWorldObjectsToClient(senderId);
    }

    public void SyncWorldObjectsToClient(long clientId)
    {
        if (_worldObjectsContainer == null) return;

        GD.Print($"NetworkManager: Syncing {_worldObjectsContainer.GetChildCount()} world objects to client {clientId}...");

        foreach (Node child in _worldObjectsContainer.GetChildren())
        {
            if (child is InteractableObject io)
            {
                string resourcePath = io.ModelPath;
                if (string.IsNullOrEmpty(resourcePath) && !string.IsNullOrEmpty(io.SceneFilePath))
                {
                    resourcePath = io.SceneFilePath;
                }
                if (string.IsNullOrEmpty(resourcePath)) continue;

                string species = "";
                if (io is Monsters monster)
                {
                    species = monster.Species;
                }

                RpcId((int)clientId, nameof(NetSpawnExistingObject), resourcePath, io.GlobalPosition, io.GlobalRotation, io.Scale, species, io.Name);
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void NetSpawnExistingObject(string resourcePath, Vector3 position, Vector3 rotation, Vector3 scale, string species, string nodeName)
    {
        GD.Print($"NetworkManager: Client spawning existing object {nodeName} from {resourcePath}");

        if (_worldObjectsContainer == null)
        {
            _worldObjectsContainer = GetTree().CurrentScene.GetNodeOrNull("WorldObjects");
            if (_worldObjectsContainer == null)
            {
                _worldObjectsContainer = new Node3D();
                _worldObjectsContainer.Name = "WorldObjects";
                GetTree().CurrentScene.AddChild(_worldObjectsContainer);
            }
        }

        if (_worldObjectsContainer.HasNode(nodeName))
        {
            GD.Print($"NetworkManager: Object {nodeName} already exists, skipping.");
            return;
        }

        Node obj = null;

        if (resourcePath.EndsWith(".gltf") || resourcePath.EndsWith(".fbx") || resourcePath.EndsWith(".glb"))
        {
            if (!ResourceLoader.Exists(resourcePath)) return;
            var scene = GD.Load<PackedScene>(resourcePath);
            if (scene == null) return;

            var model = scene.Instantiate();
            var wrapper = new InteractableObject();
            wrapper.Name = nodeName;
            wrapper.ObjectName = System.IO.Path.GetFileNameWithoutExtension(resourcePath);
            wrapper.ModelPath = resourcePath;
            wrapper.AddChild(model);
            wrapper.AddDynamicCollision();
            obj = wrapper;
        }
        else
        {
            string actualPath = resourcePath;
            int colonIndex = resourcePath.LastIndexOf(':');
            if (colonIndex > 4) actualPath = resourcePath.Substring(0, colonIndex);

            if (!ResourceLoader.Exists(actualPath)) return;
            var scene = GD.Load<PackedScene>(actualPath);
            if (scene == null) return;

            var instance = scene.Instantiate();
            instance.Name = nodeName;

            if (instance is InteractableObject io)
            {
                io.ObjectName = System.IO.Path.GetFileNameWithoutExtension(actualPath);
                io.ModelPath = resourcePath;
                if (io is Monsters monster && !string.IsNullOrEmpty(species)) monster.Species = species;
            }
            obj = instance;
        }

        if (obj is Node3D n3d)
        {
            n3d.Position = position;
            n3d.Rotation = rotation;
            n3d.Scale = scale;
        }

        _worldObjectsContainer.AddChild(obj);
    }

    public void SpawnPlayer(long id, MobaTeam team, string className = "Warrior")
    {
        if (!_isLevelLoaded) return;
        if (_spawnedPlayerIds.Contains(id)) return;

        if (PlayerScene == null) return;

        _spawnedPlayerIds.Add(id);

        var player = PlayerScene.Instantiate<PlayerController>();
        player.Name = id.ToString();
        player.Team = team;
        player.SynchronizedModel = className;
        player.SetMultiplayerAuthority((int)id);

        int playerIndex = Multiplayer.GetPeers().Length;
        player.PlayerIndex = playerIndex;

        Vector3 spawnPos = Vector3.Zero;
        Vector3 spawnRot = Vector3.Zero;

        Node3D spawnPoint = null;
        string teamSpawnName = $"SpawnPoint_{team.ToString()}";
        spawnPoint = GetTree().CurrentScene.FindChild(teamSpawnName, true, false) as Node3D;

        if (spawnPoint == null)
        {
            string[] spawnNames = { "SpawnPoint", "TeeBox", "VisualTee", "Tee" };
            foreach (var name in spawnNames)
            {
                spawnPoint = GetTree().CurrentScene.FindChild(name, true, false) as Node3D;
                if (spawnPoint != null) break;
            }
        }

        if (spawnPoint != null)
        {
            float rngX = (float)GD.RandRange(-1.5, 1.5);
            float rngZ = (float)GD.RandRange(-1.5, 1.5);
            spawnPos = spawnPoint.GlobalPosition + new Vector3(rngX, 2.0f, rngZ);
            spawnRot = spawnPoint.GlobalRotation;
        }
        else if (MobaGameManager.Instance != null)
        {
            MobaTeam finalTeam = (team == MobaTeam.None) ? MobaTeam.Red : team;
            Vector3 teamSpawn = finalTeam == MobaTeam.Red ? MobaGameManager.Instance.RedSpawnPos : MobaGameManager.Instance.BlueSpawnPos;
            if (teamSpawn != Vector3.Zero) spawnPos = teamSpawn + new Vector3(id % 3 * 2f, 2.0f, (id / 3) * 2f);
        }

        player.Position = spawnPos;
        player.Rotation = spawnRot;

        Node root = GetTree().CurrentScene;
        var playersNode = root.GetNodeOrNull("Players") ?? root;
        playersNode.AddChild(player, true);

        _players[id] = player;
        player.Rpc(nameof(PlayerController.NetTeleport), spawnPos, player.RotationDegrees);
        player.Rpc(nameof(PlayerController.NetSetPlayerIndex), playerIndex);
    }

    public void SetupObjectSpawner()
    {
        _worldObjectsContainer = GetTree().CurrentScene.GetNodeOrNull("WorldObjects");
        if (_worldObjectsContainer == null)
        {
            _worldObjectsContainer = new Node3D();
            _worldObjectsContainer.Name = "WorldObjects";
            GetTree().CurrentScene.AddChild(_worldObjectsContainer);
        }

        if (_objectSpawner != null) { _objectSpawner.QueueFree(); _objectSpawner = null; }

        _objectSpawner = new MultiplayerSpawner();
        _objectSpawner.Name = "ObjectSpawner";
        AddChild(_objectSpawner);
        _objectSpawner.SpawnPath = _worldObjectsContainer.GetPath();
        _objectSpawner.SpawnFunction = new Callable(this, nameof(SpawnNetworkObject));

        GD.Print($"NetworkManager: ObjectSpawner setup complete. Target: {_objectSpawner.SpawnPath}");
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RequestSpawnObject(string resourcePath, Vector3 position, Vector3 rotation, Vector3 scale)
    {
        if (!Multiplayer.IsServer()) return;

        if (string.IsNullOrEmpty(resourcePath)) return;

        var data = new Godot.Collections.Dictionary
        {
            { "path", resourcePath },
            { "pos", position },
            { "rot", rotation },
            { "scale", scale }
        };

        _objectSpawner.Spawn(data);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
    public void RequestDeleteObject(string nodeName)
    {
        if (!Multiplayer.IsServer()) return;

        if (_worldObjectsContainer != null && _worldObjectsContainer.HasNode(nodeName))
        {
            var node = _worldObjectsContainer.GetNode(nodeName);
            node.QueueFree();
        }
    }

    private Node SpawnNetworkObject(Godot.Collections.Dictionary data)
    {
        string type = data.ContainsKey("type") ? (string)data["type"] : "object";
        Vector3 pos = (Vector3)data["pos"];
        Vector3 rot = (Vector3)data["rot"];
        Vector3 scale = (Vector3)data["scale"];

        Node obj = null;

        if (type == "terrain")
        {
            var pointsVariant = (Godot.Collections.Array<Vector3>)data["points"];
            float elevation = (float)data["elev"];
            int tType = (int)data["tType"];

            Vector3[] pointsToUse = new Vector3[pointsVariant.Count];
            for (int i = 0; i < pointsVariant.Count; i++) pointsToUse[i] = pointsVariant[i];

            var bakedNode = new SurveyedTerrain();
            bakedNode.Name = $"Terrain_{tType}_{Time.GetTicksMsec()}";
            bakedNode.Points = pointsToUse;
            bakedNode.TerrainType = tType;

            Vector3 centroid = Vector3.Zero;
            foreach (var p in pointsToUse) centroid += p;
            if (pointsToUse.Length > 0) centroid /= pointsToUse.Length;

            Vector2[] localPoly = new Vector2[pointsToUse.Length];
            for (int i = 0; i < pointsToUse.Length; i++)
                localPoly[i] = new Vector2(pointsToUse[i].X - centroid.X, pointsToUse[i].Z - centroid.Z);

            bakedNode.Polygon = localPoly;
            bakedNode.Mode = CsgPolygon3D.ModeEnum.Depth;
            bakedNode.RotationDegrees = new Vector3(90, 0, 0);
            bakedNode.UseCollision = true;

            var mat = new StandardMaterial3D();
            switch (tType)
            {
                case 0: mat.AlbedoColor = new Color(0.2f, 0.5f, 0.2f); break;
                case 1: mat.AlbedoColor = new Color(0.15f, 0.3f, 0.15f); break;
                case 2: mat.AlbedoColor = new Color(0.08f, 0.2f, 0.08f); break;
                case 3: mat.AlbedoColor = new Color(0.1f, 0.6f, 0.1f); break;
                case 4: mat.AlbedoColor = new Color(0.9f, 0.8f, 0.5f); break;
                case 5: mat.AlbedoColor = new Color(0.1f, 0.3f, 0.8f); break;
            }
            bakedNode.MaterialOverride = mat;

            if (elevation >= 0)
            {
                bakedNode.Operation = CsgShape3D.OperationEnum.Union;
                bakedNode.Depth = 0.1f + elevation;
                pos = new Vector3(centroid.X, 0.1f, centroid.Z);
            }
            else
            {
                float depth = Mathf.Abs(elevation);
                bakedNode.Operation = CsgShape3D.OperationEnum.Subtraction;
                bakedNode.Depth = depth;
                pos = new Vector3(centroid.X, 0.1f - depth, centroid.Z);
            }

            obj = bakedNode;
        }
        else
        {
            string path = (string)data["path"];
            if (path.EndsWith(".gltf") || path.EndsWith(".fbx") || path.EndsWith(".glb"))
            {
                if (!ResourceLoader.Exists(path)) return null;
                var scene = GD.Load<PackedScene>(path);
                if (scene == null) return null;
                var model = scene.Instantiate();
                var wrapper = new InteractableObject();
                wrapper.Name = System.IO.Path.GetFileNameWithoutExtension(path) + "_" + Time.GetTicksMsec();
                wrapper.ObjectName = System.IO.Path.GetFileNameWithoutExtension(path);
                wrapper.ModelPath = path;
                wrapper.AddChild(model);
                wrapper.AddDynamicCollision();
                obj = wrapper;
            }
            else
            {
                string actualPath = path;
                string species = "";
                int colonIndex = path.LastIndexOf(':');
                if (colonIndex > 4)
                {
                    actualPath = path.Substring(0, colonIndex);
                    species = path.Substring(colonIndex + 1);
                }

                if (data.ContainsKey("species") && !string.IsNullOrEmpty((string)data["species"]))
                    species = (string)data["species"];

                if (!ResourceLoader.Exists(actualPath)) return null;
                var scene = GD.Load<PackedScene>(actualPath);
                if (scene != null)
                {
                    var instance = scene.Instantiate();
                    instance.Name = System.IO.Path.GetFileNameWithoutExtension(actualPath) + "_" + Time.GetTicksMsec();
                    if (instance is InteractableObject io)
                    {
                        io.ObjectName = System.IO.Path.GetFileNameWithoutExtension(actualPath);
                        if (string.IsNullOrEmpty(io.ModelPath)) io.ModelPath = path;
                        if (io is Monsters monster && !string.IsNullOrEmpty(species)) monster.Species = species;
                    }
                    obj = instance;
                }
            }
        }

        if (obj is Node3D n3d)
        {
            n3d.Position = pos;
            n3d.Rotation = rot;
            n3d.Scale = scale;
        }

        return obj;
    }
}
