using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zooscape.Application.Config;
using Zooscape.Domain.Enums;
using Zooscape.Domain.ExtensionMethods;
using Zooscape.Domain.Interfaces;
using Zooscape.Domain.Models;
using Zooscape.Domain.Utilities;
using Zooscape.Domain.ValueObjects;
using Zooscape.MapGenerator;

[assembly: InternalsVisibleTo("ZooscapeTests")]

namespace Zooscape.Application.Services;

public class GameStateService : IGameStateService
{
    private readonly ILogger<GameStateService> _logger;
    private readonly World _world;
    private readonly GameSettings _gameSettings;
    private readonly IPowerUpService _powerUpService;
    private readonly IObstacleService _obstacleService;
    private readonly GlobalSeededRandomizer _globalRandomizer;
    private readonly Zookeepers _zookeepersConfig;
    private readonly List<GridCoords> _objectSpawnCells;

    public ConcurrentDictionary<string, (Guid BotId, string Nickname)> BotIds { get; set; } = [];
    public List<string> Visualisers { get; set; } = [];
    public IWorld World => _world;
    public Dictionary<Guid, IZookeeper> Zookeepers => _world.Zookeepers;
    public Dictionary<Guid, IAnimal> Animals => _world.Animals.ToDictionary();
    public Dictionary<int, List<GridCoords>> PelletsToRespawn { get; } =
        new Dictionary<int, List<GridCoords>>();
    public bool IsReady => _world.IsReady;
    public int TickCounter { get; set; }

    private int _ticksUntilNextPowerUpSpawn;
    private int _ticksUntilNextObstacleSpawn;
    private int _ticksUntilNextZookeeperSpawn;

    public GameStateService(
        IOptions<GameSettings> options,
        ILogger<GameStateService> logger,
        IPowerUpService powerUpService,
        IObstacleService obstacleService,
        IAnimalFactory animalFactory,
        GlobalSeededRandomizer globalRandomizer
    )
    {
        _logger = logger;
        _gameSettings = options.Value;
        _powerUpService = powerUpService;
        _obstacleService = obstacleService;
        _globalRandomizer = globalRandomizer;

        var mapConfig = _gameSettings.WorldMap.Split(':');
        var mapString = mapConfig[0] switch
        {
            "file" => File.ReadAllText(mapConfig[1]),
            "string" => mapConfig[1],
            "generate" => GenerateMap(mapConfig[1], _globalRandomizer.Next()),
            _ => throw new ArgumentException(
                "Error reading world map",
                nameof(options.Value.WorldMap)
            ),
        };

        _world = new World(
            mapString,
            _gameSettings.NumberOfBots,
            _gameSettings.CommandQueueSize,
            animalFactory
        );

        _objectSpawnCells = Enumerable
            .Range(0, _world.Cells.GetLength(0))
            .SelectMany(row =>
                Enumerable
                    .Range(0, _world.Cells.GetLength(1))
                    .Where(col =>
                        _world.Cells[col, row] is CellContents.Empty or CellContents.Pellet
                    )
                    .Select(col => new GridCoords(col, row))
            )
            .ToList();

        _ticksUntilNextPowerUpSpawn = _powerUpService.GetTimeToNextPowerUp();
        _ticksUntilNextObstacleSpawn = _obstacleService.GetTimeToNextObstacle();
        _ticksUntilNextZookeeperSpawn = GetTicksUntilNextZookeeper();
    }

    private static string GenerateMap(string values, int seed)
    {
        var parts = values.Split('|');
        if (parts.Length < 4)
        {
            throw new ArgumentException($"Invalid generator format: {values}", nameof(values));
        }
        if (seed == 0)
        {
            throw new ArgumentException($"Invalid seed: {seed}", nameof(seed));
        }

        int size;
        int teleports;
        double smoothness;
        double openness;

        try
        {
            size = int.Parse(parts[0], CultureInfo.InvariantCulture);
            teleports = int.Parse(parts[1], CultureInfo.InvariantCulture);
            smoothness = double.Parse(parts[2], CultureInfo.InvariantCulture);
            openness = double.Parse(parts[3], CultureInfo.InvariantCulture);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                $"Invalid generator format, non number value given: {values}",
                nameof(values),
                exception
            );
        }
        if (int.IsEvenInteger(size))
        {
            throw new ArgumentException(
                $"Invalid generator format, given size ({size}) is not odd.",
                nameof(values)
            );
        }

        var map = new Map(
            size: size,
            smoothness: smoothness,
            openness: openness,
            teleports: teleports,
            seed: seed
        );

        return map.ToString();
    }

    public Result<IAnimal> AddAnimal(Guid botId, string nickname)
    {
        var newAnimal = _world.AddAnimal(botId, nickname);

        if (newAnimal.IsSuccess)
        {
            _logger.LogInformation(
                "Animal ({Nickname}) added to game world.",
                newAnimal.Value?.Nickname
            );
        }
        else
        {
            return new ResultError("Error adding animal.", newAnimal.Error);
        }

        return newAnimal;
    }

    public Result<IZookeeper> AddZookeeper()
    {
        var zookeeper = _world.AddZookeeper(Guid.NewGuid());

        if (zookeeper.IsSuccess)
        {
            _logger.LogInformation(
                "Zookeeper ({Nickname}) added to game world.",
                zookeeper.Value?.Nickname
            );
        }
        else
        {
            _logger.LogError("Error adding zookeeper: {Error}", zookeeper.Error?.ToString());
        }

        return zookeeper;
    }

    public Result<int> EnqueueCommand(Guid botId, BotCommand command)
    {
        if (!_world.Animals.TryGetValue(botId, out var animal))
            return new ResultError("Bot not found.");

        var queueSize = animal.AddCommand(new AnimalCommand(botId, command.Action));

        if (!queueSize.IsSuccess)
            return new ResultError("Error enqueueing command for bot.", queueSize.Error);

        return queueSize.Value;
    }

    public GridCoords MoveAnimal(IAnimal animal)
    {
        var newLocation = animal.Location + animal.CurrentDirection;

        // Wrap around if the map allows it
        newLocation.X = (newLocation.X + World.Width) % World.Width;
        newLocation.Y = (newLocation.Y + World.Height) % World.Height;

        if (!World.GetCellContents(newLocation).IsTraversable())
        {
            newLocation = animal.Location;
        }

        animal.SetLocation(newLocation);

        return newLocation;
    }

    public GridCoords MoveZookeeper(IZookeeper zookeeper)
    {
        var newLocation = zookeeper.Location + zookeeper.CurrentDirection;

        if (
            !World.IsPointInBounds(newLocation)
            || !World.GetCellContents(newLocation).IsTraversable()
        )
        {
            newLocation = zookeeper.Location;
        }

        zookeeper.SetLocation(newLocation);

        return newLocation;
    }

    public int GetTicksUntilPelletRespawn(int tick)
    {
        var pelletRespawn = _gameSettings.PelletRespawn.SpawnInterval;
        return tick
            + (int)
                _globalRandomizer.NormalNextDouble(
                    pelletRespawn.Mean,
                    pelletRespawn.StdDev,
                    pelletRespawn.Min,
                    pelletRespawn.Max
                );
    }

    private GridCoords? PickPowerUpCoords()
    {
        return _globalRandomizer.GetRandomElement(_objectSpawnCells, IsValidPowerUpSpawnPoint, 10);
    }

    private GridCoords? PickObstacleCoords()
    {
        return _globalRandomizer.GetRandomElement(_objectSpawnCells, IsValidObstacleSpawnPoint, 10);
    }

    private bool IsWithinDistanceOfAnimal(GridCoords coords, int distance)
    {
        var distSquared = distance * distance;
        return Animals.Any(animal =>
            animal.Value.Location.EuclideanDistanceSquared(coords) <= distSquared
        );
    }

    private bool IsWithinDistanceOfCellContents(
        GridCoords coords,
        CellContents contents,
        int distance
    )
    {
        var beginX = Math.Max(coords.X - distance, 0);
        var endX = Math.Min(coords.X + distance, World.Width - 1);
        var beginY = Math.Max(coords.Y - distance, 0);
        var endY = Math.Min(coords.Y + distance, World.Height - 1);
        for (int x = beginX; x <= endX; x++)
        {
            for (int y = beginY; y <= endY; y++)
            {
                if (World.GetCellContents(x, y).Equals(contents))
                    return true;
            }
        }
        return false;
    }

    private bool IsValidPowerUpSpawnPoint(GridCoords coords)
    {
        CellContents currentContents = World.GetCellContents(coords);

        if (currentContents != CellContents.Empty && currentContents != CellContents.Pellet)
            return false;

        if (IsWithinDistanceOfAnimal(coords, _powerUpService.DistanceFromPlayers))
            return false;

        CellContents[] powerUpCellContents =
        [
            CellContents.PowerPellet,
            CellContents.Scavenger,
            CellContents.ChameleonCloak,
            CellContents.BigMooseJuice,
        ];

        foreach (var contents in powerUpCellContents)
        {
            if (
                IsWithinDistanceOfCellContents(
                    coords,
                    contents,
                    _powerUpService.DistanceFromOtherPowerUps
                )
            )
                return false;
        }

        return true;
    }

    private bool IsValidObstacleSpawnPoint(GridCoords coords)
    {
        List<CellContents> validContents = [CellContents.Empty, CellContents.Pellet];

        List<CellContents> obstacleCellContents = [];

        List<bool> conditions =
        [
            validContents.Contains(World.GetCellContents(coords)),
            !IsWithinDistanceOfAnimal(coords, _obstacleService.DistanceFromPlayers),
            !IsWithinDistanceOfCellContents(
                coords,
                CellContents.AnimalSpawn,
                _obstacleService.DistanceFromPlayerSpawnPoints
            ),
        ];

        conditions.AddRange(
            obstacleCellContents.Select(cellContents =>
                !IsWithinDistanceOfCellContents(
                    coords,
                    cellContents,
                    _obstacleService.DistanceFromOtherObstacles
                )
            )
        );

        return conditions.All(condition => condition);
    }

    private void ProcessPowerUpSpawning()
    {
        if (--_ticksUntilNextPowerUpSpawn > 0)
        {
            return;
        }

        var spawnPoint = Helpers.TrackExecutionTime(
            "PickPowerUpCoords",
            () => PickPowerUpCoords(),
            out var _
        );

        if (spawnPoint == null)
        {
            _logger.LogInformation($"Spawning power up failed.");
            _ticksUntilNextPowerUpSpawn = _powerUpService.GetTimeToNextPowerUp();
            return;
        }
        var powerUp = _powerUpService.SpawnPowerUp();
        _logger.LogInformation(
            "Spawning {PowerUp} power up at {SpawnPoint}",
            powerUp.ToName(),
            spawnPoint
        );
        _world.SetCellContents(spawnPoint, powerUp.ToCellContents());

        _ticksUntilNextPowerUpSpawn = _powerUpService.GetTimeToNextPowerUp();
    }

    private void ProcessObstacleSpawning()
    {
        if (--_ticksUntilNextObstacleSpawn > 0)
        {
            return;
        }

        var spawnPoint = Helpers.TrackExecutionTime(
            "PickObstacleCoords",
            () => PickObstacleCoords(),
            out var _
        );

        if (spawnPoint == null)
        {
            _logger.LogInformation("Spawning obstacle failed.");
            _ticksUntilNextObstacleSpawn = _obstacleService.GetTimeToNextObstacle();
            return;
        }
        // TODO: Spawn obstacle.

        _ticksUntilNextObstacleSpawn = _obstacleService.GetTimeToNextObstacle();
    }

    private int GetTicksUntilNextZookeeper()
    {
        var zookeepersSpawn = _gameSettings.Zookeepers.SpawnInterval;
        return (int)
            _globalRandomizer.NormalNextDouble(
                zookeepersSpawn.Mean,
                zookeepersSpawn.StdDev,
                zookeepersSpawn.Min,
                zookeepersSpawn.Max
            );
    }

    private void ProcessZookeeperSpawning()
    {
        if (--_ticksUntilNextZookeeperSpawn > 0)
        {
            return;
        }

        AddZookeeper();
        _ticksUntilNextZookeeperSpawn = GetTicksUntilNextZookeeper();
    }

    private void ProcessPelletSpawning()
    {
        if (!PelletsToRespawn.ContainsKey(TickCounter))
        {
            return;
        }
        var pellets = PelletsToRespawn[TickCounter];
        _logger.LogInformation("Respawning {PelletsCount} pellets", pellets.Count);

        foreach (var pellet in pellets)
        {
            CellContents cellContents = World.GetCellContents(pellet);
            if (
                cellContents == CellContents.Empty
                && Animals.All(animal => animal.Value.Location != pellet)
            )
            {
                World.SetCellContents(pellet, CellContents.Pellet);
            }
        }
        PelletsToRespawn.Remove(TickCounter);
    }

    public void ProcessSpawning()
    {
        ProcessPowerUpSpawning();
        ProcessObstacleSpawning();
        if (PelletsToRespawn.Count != 0)
            ProcessPelletSpawning();
        if (Zookeepers.Count >= _gameSettings.Zookeepers.Max)
            return;

        ProcessZookeeperSpawning();
    }

    public int GetPowerPelletScore()
    {
        return _gameSettings.PointsPerPellet
            * _powerUpService.GetPowerUpValue(PowerUpType.PowerPellet);
    }

    #region Power Up Processing

    public void ProcessPowerUps(IAnimal animal)
    {
        if (animal.ActivePowerUp is null)
        {
            return;
        }

        switch (animal.ActivePowerUp.Type)
        {
            case PowerUpType.Scavenger:
                ProcessScavenger(animal);
                break;
            case PowerUpType.ChameleonCloak:
                ProcessChameleonCloak(animal);
                break;
            case PowerUpType.BigMooseJuice:
                break;
            default:
                var powerUpType = animal.ActivePowerUp.Type;
                DeactivatePowerUp(animal);
                throw new ArgumentException(
                    $"Power up type {powerUpType.ToName()} is not processable."
                );
        }

        animal.ActivePowerUp.TicksRemaining--;
        if (animal.ActivePowerUp.TicksRemaining <= 0)
        {
            DeactivatePowerUp(animal);
        }
    }

    public void ActivatePowerUp(IAnimal animal)
    {
        if (animal.HeldPowerUp is null)
        {
            _logger.LogWarning(
                "Animal ({Nickname}) tried to activate power up, but is not holding one",
                animal.Nickname
            );
            return;
        }

        var powerUp = (PowerUpType)animal.HeldPowerUp;

        _logger.LogInformation(
            "Animal ({Animal}) used {PowerUp}",
            animal.Nickname,
            powerUp.ToName()
        );

        animal.ActivePowerUp = _powerUpService.GetActivePowerUp(powerUp);
        animal.HeldPowerUp = null;
        animal.PowerUpsUsed++;
    }

    private static void DeactivatePowerUp(IAnimal animal)
    {
        animal.ActivePowerUp = null;
    }

    private static void ProcessChameleonCloak(IAnimal animal)
    {
        animal.IsViable = false;
    }

    private void ProcessScavenger(IAnimal animal)
    {
        var value = (int)animal.ActivePowerUp!.Value;
        _logger.LogInformation(
            "Animal {Nickname} is scavenging in a {X}x{Y} area",
            animal.Nickname,
            (value * 2) + 1,
            (value * 2) + 1
        );

        var startX = Math.Max(0, animal.Location.X - value);
        var endX = Math.Min(_world.Width - 1, animal.Location.X + value);
        var startY = Math.Max(0, animal.Location.Y - value);
        var endY = Math.Min(_world.Height - 1, animal.Location.Y + value);

        int pelletsCollected = 0;
        int powerPelletsCollected = 0;
        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                var contents = _world.GetCellContents(x, y);
                if (contents is CellContents.Pellet)
                {
                    animal.AddToScore(
                        _gameSettings.PointsPerPellet,
                        _gameSettings.PowerUps.Types[PowerUpType.BigMooseJuice.ToName()].Value
                    );
                    pelletsCollected++;
                }
                else if (contents is CellContents.PowerPellet)
                {
                    animal.AddToScore(
                        GetPowerPelletScore(),
                        _gameSettings.PowerUps.Types[PowerUpType.BigMooseJuice.ToName()].Value
                    );
                    powerPelletsCollected++;
                }
            }
        }

        _logger.LogInformation(
            "Animal {Nickname} picked up {Pellets} pellets and {PowerPellets} power pellets",
            animal.Nickname,
            pelletsCollected,
            powerPelletsCollected
        );
    }

    #endregion

    #region Wrappers for testing private methods

    protected internal static string TestGenerateMap(string values, int seed)
    {
        return GenerateMap(values, seed);
    }

    protected internal bool TestIsValidPowerUpSpawnPoint(GridCoords coords)
    {
        return IsValidPowerUpSpawnPoint(coords);
    }

    protected internal bool TestIsWithinDistanceOfAnimal(GridCoords coords, int distance)
    {
        return IsWithinDistanceOfAnimal(coords, distance);
    }

    protected internal bool TestIsWithinDistanceOfCellContents(
        GridCoords coords,
        CellContents contents,
        int distance
    )
    {
        return IsWithinDistanceOfCellContents(coords, contents, distance);
    }

    #endregion
}
