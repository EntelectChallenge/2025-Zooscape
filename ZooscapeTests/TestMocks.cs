using Zooscape.Application.Services;
using Zooscape.Domain.Enums;
using Zooscape.Domain.Models;
using Zooscape.Domain.Utilities;

namespace ZooscapeTests;

public class TestMocks
{
    public class MockPowerUpService : IPowerUpService
    {
        public int DistanceFromPlayers { get; set; }
        public int DistanceFromOtherPowerUps { get; set; }

        public int GetTimeToNextPowerUp()
        {
            return int.MaxValue;
        }

        public PowerUpType SpawnPowerUp()
        {
            return PowerUpType.ChameleonCloak;
        }

        public ActivePowerUp GetActivePowerUp(PowerUpType type)
        {
            return new ActivePowerUp()
            {
                Type = type,
                TicksRemaining = 0,
                Value = 0,
            };
        }

        public int GetPowerUpValue(PowerUpType type)
        {
            return 0;
        }
    }

    public class MockObstacleService : IObstacleService
    {
        public int DistanceFromPlayers { get; }
        public int DistanceFromPlayerSpawnPoints { get; }
        public int DistanceFromOtherObstacles { get; }

        public int GetTimeToNextObstacle()
        {
            return int.MaxValue;
        }
    }
}
