using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Morale;
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
            WallWeight = 2f,
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
            PathMaxClearance = 6,
            PathClearancePenalty = 4,
            FaceMarchMinDistance = 8f,
            WheelFlankSpeed = 1f,
            ReformTurnCos = 0.5f,
            EngageLeash = 2.0f,
            FrontArcCos = 0.5f,
            SelfDefenseRange = 1.6f,
            AdvanceContactGap = 1.5f,
            RetreatDistance = 20f,
            FollowDistance = 4f,
            BehaviorIntervalTicks = 30,
            RetargetDistance = 2f,
        };

        /// <summary>Mirrors the MoraleDefinition defaults (Morale_Default).</summary>
        public static MoraleSettings Morale => new MoraleSettings
        {
            Enabled = 1,
            FieldIntervalTicks = 3,
            Diffusion = 1.5f,
            Decay = 0.3f,
            DeathPanic = 1f,
            FlankHitPanic = 0.3f,
            FlankArcCos = 0.5f,
            KillCourage = 0.5f,
            RoutPanicPerSecond = 0.3f,
            LeaderRadius = 8f,
            LeaderCalm = 0.5f,
            PanicDrain = 5f,
            FormationSteadiness = 0.7f,
            CalmPanic = 0.02f,
            Recovery = 0.02f,
            RoutThreshold = 0.25f,
            FleeSenseRange = 8f,
            EdgeMargin = 1f,
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
