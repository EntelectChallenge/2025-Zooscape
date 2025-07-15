using System;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using Zooscape.Application.Config;
using Zooscape.Application.Services;
using Zooscape.Domain.Enums;
using Zooscape.Domain.Utilities;
using Zooscape.Domain.ValueObjects;
using ZooscapeTests.Mocks;

namespace ZooscapeTests;

public class GameStateServiceTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private GameStateService gameStateService;
    private TestMocks.MockPowerUpService powerUpService;

    private const string mapConfig = """
        11011
        14241
        05360
        14241
        11011
        """;

    public GameStateServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        var gameSettings = new GameSettings();
        gameSettings.WorldMap = $"string:{mapConfig}";
        powerUpService = new TestMocks.MockPowerUpService();
        gameStateService = new GameStateService(
            Options.Create(gameSettings),
            NullLogger<GameStateService>.Instance,
            powerUpService,
            new TestMocks.MockObstacleService(),
            new MockAnimalFactory(),
            new GlobalSeededRandomizer(1234)
        );
    }

    [Theory]
    [InlineData("21|2|0.5|0.5", null, "")]
    [InlineData("51|11|1|1", null, "")]
    [InlineData("21|6|0.1|0.1", null, "")]
    [InlineData(
        "20|6|0.1|0.1",
        typeof(ArgumentException),
        "Invalid generator format, given size (20) is not odd. (Parameter 'values')"
    )]
    public void GenerateMapTest(string mapParams, Type expectedException, string expectedMessage)
    {
        // Arrange
        string fileContents = "";
        string map = "";
        Exception? ex = null;

        if (expectedException is null)
        {
            fileContents = File.ReadAllText(
                $"GeneratedMapTestFiles/{string.Join('-', mapParams.Split('|'))}.txt"
            );
        }

        // Act
        if (expectedException is not null)
        {
            ex = Record.Exception(() => GameStateService.TestGenerateMap(mapParams, 123));
        }
        else
        {
            map = GameStateService.TestGenerateMap(mapParams, 123);
        }

        // Assert
        Assert.Multiple(() =>
        {
            if (expectedException is not null)
            {
                Assert.NotNull(ex);
                Assert.IsType(expectedException, ex);
                Assert.Equal(expectedMessage, ex.Message);
            }
            else
            {
                Assert.Equal(fileContents, map);
            }
        });
    }

    [Theory]
    [InlineData(0, 0, 20, 20, 10, false)]
    [InlineData(0, 0, 10, 10, 20, true)]
    [InlineData(0, 10, 0, 10, 1, true)]
    [InlineData(10, 10, 0, 0, 20, true)]
    [InlineData(0, 10, 0, 0, 10, true)]
    [InlineData(10, 0, 0, 0, 10, true)]
    public void IsWithinDistanceOfAnimalTest(
        int animalX,
        int animalY,
        int coordsX,
        int coordsY,
        int distance,
        bool expectedResult
    )
    {
        var animalGuid = Guid.NewGuid();
        gameStateService.AddAnimal(animalGuid, Helpers.GenerateRandomName());
        gameStateService.Animals[animalGuid].SetLocation(new GridCoords(animalX, animalY));
        GridCoords coords = new(coordsX, coordsY);
        var result = gameStateService.TestIsWithinDistanceOfAnimal(coords, distance);
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData(0, 0, CellContents.Pellet, 3, true)]
    [InlineData(2, 2, CellContents.ChameleonCloak, 1, true)]
    [InlineData(2, 2, CellContents.PowerPellet, 1, true)]
    [InlineData(2, 2, CellContents.Wall, 1, false)]
    public void IsWithinDistanceOfCellContentsTest(
        int x,
        int y,
        CellContents contents,
        int distance,
        bool expectedResult
    )
    {
        var coords = new GridCoords(x, y);
        var result = gameStateService.TestIsWithinDistanceOfCellContents(
            coords,
            contents,
            distance
        );
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData(0, 0, 4, 4, 0, 0, false)]
    [InlineData(0, 2, 4, 4, 1, 0, false)]
    [InlineData(0, 2, 2, 2, 0, 2, false)]
    [InlineData(0, 2, 2, 2, 0, 1, true)]
    [InlineData(2, 4, 2, 2, 1, 1, true)]
    public void IsValidPowerUpSpawnPointTest(
        int x,
        int y,
        int animalX,
        int animalY,
        int distanceFromPowerUps,
        int distanceFromAnimals,
        bool expectedResult
    )
    {
        var animalGuid = Guid.NewGuid();
        gameStateService.AddAnimal(animalGuid, Helpers.GenerateRandomName());
        gameStateService.Animals[animalGuid].SetLocation(new GridCoords(animalX, animalY));

        powerUpService.DistanceFromOtherPowerUps = distanceFromPowerUps;
        powerUpService.DistanceFromPlayers = distanceFromAnimals;

        GridCoords coords = new(x, y);
        var result = gameStateService.TestIsValidPowerUpSpawnPoint(coords);
        Assert.Equal(expectedResult, result);
    }
}
