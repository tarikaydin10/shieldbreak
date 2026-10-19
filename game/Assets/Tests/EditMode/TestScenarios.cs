using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>Shared sim setups for tests. Values mirror the AgentMovementDefinition defaults.</summary>
    static class TestScenarios
    {
        public const float AgentRadius = 0.3f;

        public static AgentMoveSettings Movement => new AgentMoveSettings
        {
            MaxSpeed = 1.6f,
            MaxAcceleration = 4f,
            SlowRadius = 1.5f,
            StopRadius = 0.1f,
            SeparationRadius = 0.8f,
            SeparationWeight = 1.5f,
            MaxTurnRateDeg = 540f,
        };

        /// <summary>Mirrors the FormationDefinition defaults.</summary>
        public static FormationSettings Formations => new FormationSettings
        {
            Line = new FormationSpacing { Lateral = 1.1f, Depth = 1.3f },
            ShieldWall = new FormationSpacing { Lateral = 0.8f, Depth = 1.0f },
            Wedge = new FormationSpacing { Lateral = 1.0f, Depth = 1.1f },
            Circle = new FormationSpacing { Lateral = 1.0f, Depth = 1.2f },
            Loose = new FormationSpacing { Lateral = 2.2f, Depth = 2.4f },
            MarchSpeed = 1.2f,
            CohesionSlack = 0.6f,
            CohesionRange = 1.5f,
            MinMarchFactor = 0.2f,
            FaceSlotDistance = 1.0f,
            LossReformIntervalTicks = 60,
        };

        public static SimConfig Config(ulong seed, int size, int maxAgents) => new SimConfig
        {
            Seed = seed,
            InputDelayTicks = 6,
            GridWidth = size,
            GridHeight = size,
            MaxAgents = maxAgents,
            Movement = Movement,
        };

        /// <summary>Spawns agents at positions drawn from the world's own SimRandom, so the seed shapes the scenario.</summary>
        public static void SpawnRandom(SimWorld world, int count, float margin = 1f)
        {
            float2 min = world.Config.GridOrigin + margin;
            float2 max = world.Config.GridOrigin + new float2(world.Config.GridWidth, world.Config.GridHeight) - margin;
            for (int i = 0; i < count; i++)
                world.SpawnAgent(i % 2, world.Random.NextFloat2(min, max), AgentRadius);
        }
    }
}
