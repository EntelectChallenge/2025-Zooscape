using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;

public partial class GameManager : Node3D
{
    [ExportGroup("Scene References")]
    [Export]
    PackedScene AnimalScene { get; set; }

    [Export]
    PackedScene ZookeeperScene { get; set; }

    [Export]
    PackedScene PelletScene { get; set; }

    [Export]
    PackedScene PowerPelletScene { get; set; }

    [Export]
    PackedScene ChameleonPelletScene { get; set; }

    [Export]
    PackedScene ScavengerPelletScene { get; set; }

    [Export]
    PackedScene BigMoosePelletScene { get; set; }

    [Export]
    PackedScene WallScene { get; set; }

    [Export]
    PackedScene AnimalSpawnScene { get; set; }

    [Export]
    PackedScene ZookeeperSpawnScene { get; set; }

    private Camera3D Camera { get; set; }

    private HubConnection Connection;
    private bool GameEngineConnected = false;

    private List<AnimalReference> Animals;
    private List<ZookeeperReference> Zookeepers;
    private List<(int, Cell, Node3D)> Pellets;

    private TickState State;
    private int StateIndex = 0;
    private bool PlayForward = true;
    private bool Initialized = false;

    public override void _Ready()
    {
        State = new TickState { WorldStates = new List<GameState>() };
        Animals = new List<AnimalReference>();
        Zookeepers = new List<ZookeeperReference>();
        Pellets = new List<(int, Cell, Node3D)>();

        Camera = GetNode<Camera3D>("Camera3D");

        ManageTimedEvents();
        ManageUiInteractions();
        ManageUiDisplay();

        if (!GameSettings.ReadFromLogs)
        {
            ManageHubConnection();
        }
        else
        {
            var splashPanel = GetNode<PanelContainer>("UI/SplashPanel");
            splashPanel.Visible = false;

            ReadStateFromLogs();
        }

        base._Ready();
    }

    private void ManageTimedEvents()
    {
        var timer = GetNode<Timer>("Timers/TickTimer");
        timer.Start(1 / GameSettings.GameSpeed);
        timer.Timeout += ManageTickEvents;
    }

    private void CheckGameEngineConnection()
    {
        var timer = GetNode<Timer>("Timers/SplashTimer");
        timer.Start(5);
        timer.Timeout += ResetVisualiser;
    }

    private void ResetVisualiser()
    {
        if (!GameEngineConnected)
        {
            var scene = ResourceLoader.Load<PackedScene>("res://scenes/menus/main.tscn").Instantiate();
            NotificationManager.ShowNotification(this, "Game engine link failed!", false);
            NavigationManager.NavigateToScene(scene, this);
        }
    }

    private void ManageTickEvents()
    {
        ManageUiDisplay();
        VisualiseWorld();
    }

    private void ManageUiInteractions()
    {
        var replayButton = GetNode<Button>(
            "UI/HBoxContainer/VBoxContainer/HBoxContainer/ReplayButton"
        );
        var rewindButton = GetNode<Button>(
            "UI/HBoxContainer/VBoxContainer/HBoxContainer/RewindButton"
        );
        var forwardButton = GetNode<Button>(
            "UI/HBoxContainer/VBoxContainer/HBoxContainer/ForwardButton"
        );

        replayButton.Pressed += OnReplayButtonPressed;
        rewindButton.Pressed += OnRewindButtonPressed;
        forwardButton.Pressed += OnForwardButtonPressed;
    }

    private void ManageUiDisplay()
    {
        var tickNumber = GetNode<Label>("UI/HBoxContainer/VBoxContainer/TickNumber");

        if (StateIndex == 0 && !Initialized)
        {
            var scoreboard = GetNode<VBoxContainer>("UI/HBoxContainer/Scoreboard");
            var scoreboardLineItem = GetNode<HBoxContainer>("UI/HBoxContainer/Scoreboard/LineItem");

            foreach (var animal in Animals)
            {
                var lineItem = scoreboardLineItem.Duplicate();
                scoreboard.AddChild(lineItem);
                animal.ScoreBoardLineItem = lineItem;
            }
        }

        // foreach (var animal in Animals)
        // {
        // 	var lineItem = animal.ScoreBoardLineItem;
        // 	var name = lineItem.GetNode<Label>("PanelContainer/Name");
        // 	var score = lineItem.GetNode<Label>("PanelContainer/Score");
        // 	var captured = lineItem.GetNode<Label>("PanelContainer/Captured");
        // 	var distance = lineItem.GetNode<Label>("PanelContainer/Distance");

        // 	name.Text = animal.AnimalDetail.NickName;
        // 	score.Text = animal.AnimalDetail.Score.ToString();
        // 	captured.Text = animal.AnimalDetail.CapturedCounter.ToString();
        // 	distance.Text = animal.AnimalDetail.DistanceCovered.ToString();
        // }

        tickNumber.Text = $"Tick: {StateIndex}";
    }

    private void OnReplayButtonPressed()
    {
        foreach (var animal in Animals)
        {
            RemoveChild(animal.NodeReference);
        }

        foreach (var zookeeper in Zookeepers)
        {
            RemoveChild(zookeeper.NodeReference);
        }

        foreach (var pellet in Pellets)
        {
            RemoveChild(pellet.Item3);
        }

        Animals = new List<AnimalReference>();
        Zookeepers = new List<ZookeeperReference>();
        Pellets = new List<(int, Cell, Node3D)>();

        StateIndex = 0;
        PlayForward = true;
        Initialized = false;
        OnForwardButtonPressed();
    }

    private void OnRewindButtonPressed()
    {
        if (!PlayForward)
        {
            return;
        }

        StateIndex--;
        PlayForward = false;
        ManageTickEvents();
    }

    private void OnForwardButtonPressed()
    {
        if (PlayForward)
        {
            return;
        }

        StateIndex++;
        PlayForward = true;
        ManageTickEvents();
    }

    private void ReadStateFromLogs()
    {
        var content = File.ReadLines(GameSettings.LogsLocation);

        foreach (var line in content)
        {
            var world = JsonConvert.DeserializeObject<GameState>(line);
            State.WorldStates.Add(world);
        }
    }

    private void ManageHubConnection()
    {
        var splashPanel = GetNode<PanelContainer>("UI/SplashPanel");

        Connection = new HubConnectionBuilder()
            .WithUrl(new Uri("http://localhost:5000/bothub"))
            .Build();

        if (Connection == null)
        {
            throw new Exception("Connection doesn't exist!");
        }

        if (Connection?.State != HubConnectionState.Connected)
        {
            Connection.On<string>(
                "Connect",
                (connectionId) =>
                {
                    if (Connection.ConnectionId == connectionId)
                    {
                        Connection.InvokeAsync("RegisterVisualiser");
                        NotificationManager.ShowNotification(this, "Game engine linked successfully!");
                        GameEngineConnected = true;
                        splashPanel.Visible = false;
                    }
                }
            );

            Connection.On<GameState>(
                "GameState",
                (world) =>
                {
                    State.WorldStates.Add(world);
                }
            );

            Connection.StartAsync();
            CheckGameEngineConnection();
        }
    }

    private void BackToMenu()
    {
        var menuScene = ResourceLoader.Load<PackedScene>("res://scenes/menus/main.tscn").Instantiate();
        NavigationManager.NavigateToScene(menuScene, this);
    }

    private void VisualiseWorld()
    {
        if (State == null || StateIndex >= State.WorldStates.Count || StateIndex < 0)
        {
            return;
        }

        if (StateIndex == 0)
        {
            var world = State.WorldStates[StateIndex];
            var mapBase = GetNode<CsgBox3D>("MapBase");

            var x = world.Cells.Max(c => c.X) + 1;
            var y = world.Cells.Max(c => c.Y) + 1;

            mapBase.GlobalPosition = new Vector3(x / 2, -1, y / 2);
            mapBase.Size = new Vector3(x, 1, y);

            Camera.GlobalPosition = new Vector3(
                Camera.GlobalPosition.X,
                Camera.GlobalPosition.Y,
                y / 2
            );
        }

        for (int i = 0; i < Zookeepers.Count(); i++)
        {
            var zookeeper = Zookeepers[i];
            var zookeeperToRemove = State.WorldStates[StateIndex].Zookeepers.SingleOrDefault(b => b.Id == zookeeper.ConnectionId);

            if (zookeeperToRemove == null)
            {
                RemoveChild(zookeeper.NodeReference);
                Zookeepers.Remove(zookeeper);
            }
        }

        foreach (var zookeeper in State.WorldStates[StateIndex].Zookeepers)
        {
            if (StateIndex == 0 && PlayForward && !Initialized)
            {
                var obj = (Node3D)ZookeeperScene.Instantiate();
                var nameLabel = obj.GetNode<Label>("ZookeeperName/SubViewport/CenterContainer/Label");
                nameLabel.Text = zookeeper.NickName;
                GD.Print($"zookeeper: {zookeeper.NickName}");

                AddChild(obj);
                obj.GlobalPosition = new Vector3(zookeeper.X, 0f, zookeeper.Y);
                Zookeepers.Add(
                    new ZookeeperReference
                    {
                        ConnectionId = zookeeper.Id,
                        NodeReference = obj,
                        ZookeeperDetail = zookeeper,
                    }
                );
                continue;
            }

            var additionalZookeeper = Zookeepers.SingleOrDefault(b => b.ConnectionId == zookeeper.Id);

            if (additionalZookeeper == null)
            {
                GD.Print($"zookeeperId: {zookeeper.Id}");
                var obj = (Node3D)ZookeeperScene.Instantiate();
                var nameLabel = obj.GetNode<Label>("ZookeeperName/SubViewport/CenterContainer/Label");
                nameLabel.Text = zookeeper.NickName;
                GD.Print($"zookeeper: {zookeeper.NickName}");

                AddChild(obj);
                obj.GlobalPosition = new Vector3(zookeeper.X, 0f, zookeeper.Y);
                Zookeepers.Add(
                    new ZookeeperReference
                    {
                        ConnectionId = zookeeper.Id,
                        NodeReference = obj,
                        ZookeeperDetail = zookeeper,
                    }
                );

                additionalZookeeper = Zookeepers.SingleOrDefault(b => b.ConnectionId == zookeeper.Id);
            }

            additionalZookeeper.ZookeeperDetail = zookeeper;
        }

        foreach (var animal in State.WorldStates[StateIndex].Animals)
        {
            Node3D obj = null;

            if (StateIndex == 0 && PlayForward && !Initialized)
            {
                obj = (Node3D)AnimalScene.Instantiate();
                var nameLabel = obj.GetNode<Label>("AnimalLabels/SubViewport/VBoxContainer/Name");
                nameLabel.Text = animal.NickName;

                AddChild(obj);
                obj.GlobalPosition = new Vector3(animal.X, 0f, animal.Y);
                Animals.Add(
                    new AnimalReference
                    {
                        ConnectionId = animal.Id,
                        NodeReference = obj,
                        AnimalDetail = animal,
                    }
                );
                continue;
            }

            var bot = Animals.SingleOrDefault(b => b.ConnectionId == animal.Id);
            bot.AnimalDetail = animal;

            var activePowerUp = bot.NodeReference.GetNode<Label>("AnimalLabels/SubViewport/VBoxContainer/ActivePowerUp");
            var heldPowerUp = bot.NodeReference.GetNode<Label>("AnimalLabels/SubViewport/VBoxContainer/HeldPowerUp");

            activePowerUp.Text = animal.ActivePowerUp != null ? $"Active - {animal.ActivePowerUp.Type.ToString()}" : "";
            heldPowerUp.Text = animal.HeldPowerUp != null ? $"Held - {animal.HeldPowerUp.ToString()}" : "";
        }

        foreach (var cell in State.WorldStates[StateIndex].Cells)
        {
            Node3D obj = null;

            if ((StateIndex == 0 || !PlayForward) && !Initialized)
            {
                switch (cell.Content)
                {
                    case CellContent.Wall:
                        obj = (Node3D)WallScene.Instantiate();
                        break;
                    case CellContent.AnimalSpawn:
                        obj = (Node3D)AnimalSpawnScene.Instantiate();
                        break;
                    case CellContent.ZookeeperSpawn:
                        obj = (Node3D)ZookeeperSpawnScene.Instantiate();
                        break;
                }
            }

            switch (cell.Content)
            {
                case CellContent.Pellet:
                    obj = ManagePellet(cell, PelletScene);
                    break;
                case CellContent.PowerPellet:
                    obj = ManagePellet(cell, PowerPelletScene);
                    break;
                case CellContent.ChameleonCloak:
                    obj = ManagePellet(cell, ChameleonPelletScene);
                    break;
                case CellContent.Scavenger:
                    obj = ManagePellet(cell, ScavengerPelletScene);
                    break;
                case CellContent.BigMooseJuice:
                    obj = ManagePellet(cell, BigMoosePelletScene);
                    break;
            }

            if (cell.Content == CellContent.Empty)
            {
                IEnumerable<(int,Cell, Node3D)> orderedPellets;

                if (PlayForward)
                {
                    orderedPellets = Pellets.OrderBy(p => p.Item1);
                }
                else
                {
                    orderedPellets = Pellets.OrderByDescending(p => p.Item1);
                }

                var pellet = orderedPellets.FirstOrDefault(p =>
                    p.Item2.X == cell.X && p.Item2.Y == cell.Y
                );

                if (pellet.Item2 != null)
                {
                    // TODO: Check performance on this function.
                    RemoveChild(pellet.Item3);
                    Pellets.Remove(pellet);
                }
            }

            if (obj == null)
            {
                continue;
            }

            AddChild(obj);
            obj.GlobalPosition = new Vector3(cell.X, 0f, cell.Y);
        }

        if (StateIndex == 0 && !Initialized)
        {
            Initialized = true;
        }

        if (PlayForward)
        {
            StateIndex++;
        }
        else
        {
            StateIndex--;
        }
    }

    public override void _Process(double delta) { }

    public override void _PhysicsProcess(double delta)
    {
        foreach (var zookeeper in Zookeepers)
        {
            MoveCharacter(
                zookeeper.NodeReference,
                new Vector3(zookeeper.ZookeeperDetail.SpawnX, 0f, zookeeper.ZookeeperDetail.SpawnY),
                new Vector3(zookeeper.ZookeeperDetail.X, 0f, zookeeper.ZookeeperDetail.Y),
                delta
            );
        }

        foreach (var bot in Animals)
        {
            MoveCharacter(
                bot.NodeReference,
                new Vector3(bot.AnimalDetail.SpawnX, 0f, bot.AnimalDetail.SpawnY),
                new Vector3(bot.AnimalDetail.X, 0f, bot.AnimalDetail.Y),
                delta
            );
        }

        base._PhysicsProcess(delta);
    }

    private Node3D ManagePellet(Cell cell, PackedScene scene)
    {
        Node3D obj = null;
        
        if (!Pellets.Any(p => p.Item2.X == cell.X && p.Item2.Y == cell.Y && p.Item2.Content == cell.Content))
        {
            obj = (Node3D)scene.Instantiate();
            Pellets.Add((Pellets.Count() + 1, cell, obj));
        }
        
        return obj;
    }

    private void MoveCharacter(
        Node3D node,
        Vector3 spawnPosition,
        Vector3 targetPosition,
        double delta
    )
    {
        if (node == null)
        {
            return;
        }

        // if (node.GlobalPosition != targetPosition)
        // {
        // 	node.LookAt(targetPosition);
        // }

        var distance = node.GlobalPosition.DistanceTo(targetPosition);

        if (targetPosition.X == spawnPosition.X && targetPosition.Z == spawnPosition.Z || distance > 2)
        {
            node.GlobalPosition = targetPosition;
            return;
        }

        node.GlobalPosition = node.GlobalPosition.MoveToward(
            targetPosition,
            (float)delta * GameSettings.GameSpeed
        );
    }
}
