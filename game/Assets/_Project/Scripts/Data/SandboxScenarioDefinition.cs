using System;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// Sandbox: flat map with spawn groups facing a point. A group is a formation (M2: takes FormationOrders) or a
    /// loose block (M0: move orders per soldier, positions jittered by the world's own SimRandom). Spawning is
    /// scenario setup and runs identically on every peer.
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Sandbox Scenario", fileName = "SandboxScenario")]
    public class SandboxScenarioDefinition : ScriptableObject, ISimScenario
    {
        [Serializable]
        public class SpawnGroup
        {
            public string name = "Gruppe";
            [Tooltip("0 = Team A (rot), 1 = Team B (blau).")]
            [Range(0, 1)] public int team;
            [Min(1)] public int count = 10;
            [Tooltip("Soldiers per rank (width of the block).")]
            [Min(1)] public int files = 5;
            [Tooltip("Centre of the front rank (map coordinates in m).")]
            public Vector2 front;
            [Tooltip("Point the block faces at spawn.")]
            public Vector2 faceTowards;
            [Tooltip("Spawn as a formation that takes formation orders (at most 256 soldiers). Off: loose block, soldiers are moved one by one.")]
            public bool asFormation = true;
            public FormationType formationType = FormationType.Line;
            [Tooltip("Player id that may order this formation; in the sandbox player 0 commands both teams.")]
            [Min(0)] public int owner;
        }

        [Tooltip("Seed of the SimWorld; same seed, same battle.")]
        public int seed = 1;
        [Tooltip("Lockstep input delay in ticks (60 per second).")]
        [Min(0)] public int inputDelayTicks = 6;
        [Tooltip("Map size in 1 m cells.")]
        [Min(8)] public int mapWidth = 96;
        [Min(8)] public int mapHeight = 96;
        [Min(0.05f)] public float agentRadius = 0.3f;
        [Min(0.5f)] public float spawnSpacing = 1.1f;
        [Tooltip("Random offset per soldier at spawn (m).")]
        [Min(0f)] public float spawnJitter = 0.15f;
        [Tooltip("Formation spacing and march tuning. Empty = defaults of FormationDefinition.")]
        public FormationDefinition formation;
        public SpawnGroup[] groups = Array.Empty<SpawnGroup>();

        public int TotalAgents
        {
            get
            {
                int n = 0;
                foreach (var g in groups) n += g.count;
                return n;
            }
        }

        public int FormationCount
        {
            get
            {
                int n = 0;
                foreach (var g in groups) if (IsFormation(g)) n++;
                return n;
            }
        }

        static bool IsFormation(SpawnGroup g) => g.asFormation && g.count <= FormationLimits.MaxMembers;

        public SimConfig ToConfig(AgentMoveSettings movement) => new SimConfig
        {
            Seed = (ulong)(uint)seed,
            InputDelayTicks = inputDelayTicks,
            GridWidth = mapWidth,
            GridHeight = mapHeight,
            MaxAgents = TotalAgents,
            Movement = movement,
            MaxFormations = FormationCount,
            Formations = BuildFormationSettings(),
        };

        FormationSettings BuildFormationSettings()
        {
            if (formation != null) return formation.ToSettings();
            var defaults = CreateInstance<FormationDefinition>();
            try
            {
                return defaults.ToSettings();
            }
            finally
            {
                if (Application.isPlaying) Destroy(defaults);
                else DestroyImmediate(defaults);
            }
        }

        public SimWorld CreateWorld(AgentMoveSettings movement, out int playerAgent)
        {
            var world = new SimWorld(ToConfig(movement));
            Spawn(world);
            playerAgent = -1;
            return world;
        }

        /// <summary>Spawns all groups in list order; agent ids are contiguous per group.</summary>
        public void Spawn(SimWorld world)
        {
            foreach (var g in groups)
            {
                float2 f = math.normalizesafe((float2)(g.faceTowards - g.front), new float2(1f, 0f));
                if (g.asFormation && !IsFormation(g))
                    Debug.LogWarning($"{name}: group '{g.name}' has {g.count} soldiers, more than a formation holds ({FormationLimits.MaxMembers}); spawned loose.");
                if (IsFormation(g))
                {
                    world.SpawnFormation(g.team, g.owner, g.formationType, g.files, g.count, g.front, f, agentRadius);
                    continue;
                }
                float2 right = new float2(f.y, -f.x);
                for (int k = 0; k < g.count; k++)
                {
                    int file = k % g.files;
                    int rank = k / g.files;
                    float lateral = (file - (math.min(g.files, g.count) - 1) * 0.5f) * spawnSpacing;
                    float2 p = (float2)g.front + right * lateral - f * (rank * spawnSpacing);
                    p += world.Random.NextFloat2(-spawnJitter, spawnJitter);
                    world.SpawnAgent(g.team, p, agentRadius, f);
                }
            }
        }
    }
}
