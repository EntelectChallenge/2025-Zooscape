using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using Zooscape.Application.Config;
using Zooscape.Application.Services;
using Zooscape.Domain.Enums;
using Zooscape.Domain.Models;
using Zooscape.Domain.Utilities;
using Zooscape.Domain.ValueObjects;
using ZooscapeTests.Mocks;

namespace ZooscapeTests;

public class GameplayTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public GameplayTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(ReadTestCaseFiles))]
    public async Task GameplayTest(TestData testData)
    {
        // Arrange
        var gameSettings = new GameSettings
        {
            WorldMap = $"string:{testData.Input.Map}",
            TickDuration = 10,
            MaxTicks = testData.Input.Ticks,
            NumberOfBots = testData.Input.AnimalIds.Length,
            PointsPerPellet = 10,
            ScoreLossPercentage = 50,
            ScoreStreak =
                testData.Input.ScoreStreak
                ?? new Zooscape.Application.Config.ScoreStreak
                {
                    Max = 1,
                    MultiplierGrowthFactor = 0,
                    ResetGrace = 0,
                },
            PowerUps =
                testData.Input.PowerUps
                ?? new PowerUps
                {
                    Types = new Dictionary<string, PowerUpParameters>()
                    {
                        {
                            "BigMooseJuice",
                            new PowerUpParameters
                            {
                                RarityWeight = 4,
                                Value = 3,
                                Duration = 5,
                            }
                        },
                    },
                },
            Zookeepers =
            {
                Max = testData.Input.Zookeepers,
                SpawnInterval = new SpawnIntervals
                {
                    Mean = 2,
                    StdDev = 1,
                    Min = 1,
                    Max = 3,
                },
            },
        };

        var logSettings = new GameLogsConfiguration
        {
            PushLogsToS3 = false,
            FullLogsEnabled = false,
            DiffLogsEnabled = false,
        };

        var gameSettingsOptions = Options.Create(gameSettings);
        var logSettingsOptions = Options.Create(logSettings);
        var powerUpService = new TestMocks.MockPowerUpService();
        var obstacleService = new TestMocks.MockObstacleService();
        var globalSeededRandomiser = new GlobalSeededRandomizer(1234);
        var animalFactory = new AnimalFactory(gameSettingsOptions);

        var gameState = new GameStateService(
            gameSettingsOptions,
            NullLogger<GameStateService>.Instance,
            powerUpService,
            obstacleService,
            animalFactory,
            globalSeededRandomiser
        );

        foreach (var botId in testData.Input.AnimalIds)
        {
            gameState.AddAnimal(botId, "");
        }

        if (testData.Input.Zookeepers > 0)
        {
            gameState.AddZookeeper();
        }

        var workerService = new WorkerService(
            logger: NullLogger<WorkerService>.Instance,
            gameSettingsOptions: gameSettingsOptions,
            gameLogsConfigOptions: logSettingsOptions,
            rng: new GlobalSeededRandomizer(123),
            zookeeperService: new ZookeeperService(gameSettingsOptions),
            gameStateService: gameState,
            signalREventDispatcher: new MockSignalREventDispatcher(t =>
            {
                _testOutputHelper.WriteLine($"Tick Counter: {t}");
                if (testData.Input.Actions.TryGetValue(t, out var actions))
                {
                    foreach (var botActionTuple in actions)
                    {
                        gameState.EnqueueCommand(
                            botActionTuple.BotId,
                            new BotCommand() { Action = botActionTuple.Action }
                        );
                        _testOutputHelper.WriteLine(
                            $"\t{botActionTuple.BotId} -> {botActionTuple.Action}"
                        );
                    }
                }
            }),
            cloudEventDispatcher: new MockCloudEventDispatcher(),
            logStateEventDispatcher: new MockLogStateEventDisptcher(),
            logDiffStateEventDispatcher: new MockLogStateEventDisptcher(),
            applicationLifetime: new MockApplicationLifetime()
        );

        // Act
        await workerService.GameLoop(new CancellationToken());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.Equal(testData.ExpectedState.TickCounter, gameState.TickCounter);
            Assert.Equal(testData.ExpectedState.ZookeeperCount, gameState.Zookeepers.Count);
            foreach (var botId in testData.ExpectedState.Animals.Keys)
            {
                Assert.True(
                    testData.ExpectedState.Animals[botId].Location
                        == gameState.Animals[botId].Location,
                    $"Location: Expected {testData.ExpectedState.Animals[botId].Location}. Actual {gameState.Animals[botId].Location}"
                );
                Assert.True(
                    testData.ExpectedState.Animals[botId].CurrentDirection
                        == gameState.Animals[botId].CurrentDirection,
                    $"CurrentDirection: Expected {testData.ExpectedState.Animals[botId].CurrentDirection}. Actual {gameState.Animals[botId].CurrentDirection}"
                );
                Assert.True(
                    testData.ExpectedState.Animals[botId].Score == gameState.Animals[botId].Score,
                    $"Score: Expected {testData.ExpectedState.Animals[botId].Score}. Actual {gameState.Animals[botId].Score}"
                );
                Assert.True(
                    testData.ExpectedState.Animals[botId].TicksOnSpawn
                        == gameState.Animals[botId].TicksOnSpawn,
                    $"TicksOnSpawn: Expected {testData.ExpectedState.Animals[botId].TicksOnSpawn}. Actual {gameState.Animals[botId].TicksOnSpawn}"
                );
                Assert.True(
                    testData.ExpectedState.Animals[botId].CapturedCounter
                        == gameState.Animals[botId].CapturedCounter,
                    $"CapturedCounter: Expected {testData.ExpectedState.Animals[botId].CapturedCounter}. Actual {gameState.Animals[botId].CapturedCounter}"
                );
                Assert.True(
                    testData.ExpectedState.Animals[botId].DistanceCovered
                        == gameState.Animals[botId].DistanceCovered,
                    $"DistanceCovered: Expected {testData.ExpectedState.Animals[botId].DistanceCovered}. Actual {gameState.Animals[botId].DistanceCovered}"
                );
                Assert.True(
                    testData.ExpectedState.Animals[botId].IsViable
                        == gameState.Animals[botId].IsViable,
                    $"IsViable: Expected {testData.ExpectedState.Animals[botId].IsViable}. Actual {gameState.Animals[botId].IsViable}"
                );
            }
        });
    }

    public class TestData
    {
        public string TestCaseName { get; set; }
        public InputForTest Input { get; set; }
        public ExpectedState ExpectedState { get; set; }

        public override string ToString()
        {
            return TestCaseName;
        }
    }

    public class FileData
    {
        public InputFromFile Input { get; set; }
        public ExpectedState ExpectedState { get; set; }

        public TestData ToTestData(string testCaseName)
        {
            return new TestData()
            {
                TestCaseName = testCaseName,
                Input = new InputForTest()
                {
                    Map = string.Join(Environment.NewLine, Input.Map),
                    AnimalIds = Input.AnimalIds,
                    Zookeepers = Input.Zookeepers,
                    Ticks = Input.Ticks,
                    Actions = Input.Actions,
                    ScoreStreak = Input.ScoreStreak,
                    PowerUps = Input.PowerUps,
                },
                ExpectedState = ExpectedState,
            };
        }
    }

    public class Input
    {
        public Guid[] AnimalIds { get; set; }
        public int Zookeepers { get; set; }
        public int Ticks { get; set; }
        public Zooscape.Application.Config.ScoreStreak ScoreStreak { get; set; }
        public Dictionary<int, List<BotActionTuple>> Actions { get; set; }

        public PowerUps PowerUps { get; set; }
    }

    public class InputFromFile : Input
    {
        public string[] Map { get; set; }
    }

    public class InputForTest : Input
    {
        public string Map { get; set; }
    }

    public class ExpectedState
    {
        public Dictionary<Guid, AnimalState> Animals { get; set; }
        public int TickCounter { get; set; }
        public int ZookeeperCount { get; set; }
    }

    public class BotActionTuple
    {
        public Guid BotId { get; set; }
        public BotAction Action { get; set; }

        public BotActionTuple(Guid botId, BotAction action)
        {
            BotId = botId;
            Action = action;
        }
    }

    public class AnimalState
    {
        public GridCoords Location { get; set; }
        public Direction CurrentDirection { get; set; }
        public int Score { get; set; }
        public int TicksOnSpawn { get; set; }
        public int CapturedCounter { get; set; }
        public int DistanceCovered { get; set; }
        public bool IsViable { get; set; }
    }

    public static IEnumerable<object[]> ReadTestCaseFiles()
    {
        foreach (string file in Directory.EnumerateFiles("GameplayTestFiles", "*.json"))
        {
            FileData fileData = ReadJsonFile<FileData>(file);
            yield return new object[] { fileData.ToTestData(Path.GetFileName(file)) };
        }
    }

    public static T ReadJsonFile<T>(string filePath)
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };

        string json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, options)!;
    }
}
