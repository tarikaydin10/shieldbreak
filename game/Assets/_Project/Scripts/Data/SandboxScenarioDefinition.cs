using System;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// Sandbox: flat map with spawn groups facing a point. A group is a formation (M2: takes FormationOrders) or a
    /// loose block (M0: move orders per soldier, positions jittered by the world's own SimRandom). With weapons and a
    /// combat tuning the soldiers fight as bots of their group's level. Spawning is scenario setup and runs
    /// identically on every peer.
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
            [Tooltip("Bot level 1-5 the soldiers fight with; 0 = they do not fight. Needs weapons and a combat tuning.")]
            [Range(0, 5)] public int botLevel = 3;
            [Tooltip("Weapon id: index in the scenario's weapons (0 = sword and shield, 1 = two-hander).")]
            [Min(0)] public int weapon;
        }

        [Serializable]
        public class Obstacle
        {
            public string name = "Hindernis";
            [Tooltip("Lower-left corner (map coordinates in m).")]
            public Vector2 min;
            [Tooltip("Upper-right corner (map coordinates in m).")]
            public Vector2 max;
            [Tooltip("255 = impassable (wall, rock); 2-254 = passable but slow to path through (brush, mud).")]
            [Range(2, 255)] public int cost = 255;
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

        [Header("Combat (no weapons or no tuning: the soldiers only march)")]
        [Tooltip("Index = weapon id in the sim. 0 = one-hand + shield, 1 = two-hand.")]
        public WeaponDefinition[] weapons = Array.Empty<WeaponDefinition>();
        [Tooltip("Feel preset: damage, stamina, timing.")]
        public CombatTuningDefinition combatTuning;
        [Tooltip("Bot levels; empty = defaults.")]
        public BotRosterDefinition roster;

        public bool HasCombat => weapons != null && weapons.Length > 0 && combatTuning != null;
        public SpawnGroup[] groups = Array.Empty<SpawnGroup>();
        [Tooltip("Blocked or rough rectangles on the map; formations path around them.")]
        public Obstacle[] obstacles = Array.Empty<Obstacle>();

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
            SimWorld world;
            if (HasCombat)
            {
                using var setup = CombatDatabase.Build(weapons, combatTuning, roster, Allocator.Temp);
                world = new SimWorld(ToConfig(movement), setup);
            }
            else world = new SimWorld(ToConfig(movement));
            Spawn(world);
            playerAgent = -1;
            return world;
        }

        /// <summary>Sets the obstacles, then spawns all groups in list order; agent ids are contiguous per group.</summary>
        public void Spawn(SimWorld world)
        {
            var layout = world.Grid.Layout;
            foreach (var o in obstacles)
                world.SetNavCost(layout.WorldToCell(o.min), layout.WorldToCell((float2)o.max - 1e-3f), (byte)math.clamp(o.cost, 2, 255));

            foreach (var g in groups)
            {
                float2 f = math.normalizesafe((float2)(g.faceTowards - g.front), new float2(1f, 0f));
                if (g.asFormation && !IsFormation(g))
                    Debug.LogWarning($"{name}: group '{g.name}' has {g.count} soldiers, more than a formation holds ({FormationLimits.MaxMembers}); spawned loose.");
                int weapon = world.HasCombat ? math.clamp(g.weapon, 0, world.CombatSetup.Weapons.Length - 1) : 0;
                int level = world.HasCombat ? math.clamp(g.botLevel, 0, world.CombatSetup.BotProfiles.Length - 1) : 0;
                if (IsFormation(g))
                {
                    world.SpawnFormation(g.team, g.owner, g.formationType, g.files, g.count, g.front, f, agentRadius, weapon, level);
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
                    world.SpawnAgent(g.team, p, agentRadius, f, weapon, level);
                }
            }
        }
    }
}
