using System;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// M2 company battle: the player's hero (player 0) leads his own formations against bot formations, with combat
    /// and morale. The player orders his formations through the radial menu (and later the command view); the enemy
    /// formations start with a behavior (no commander AI before M3). Spawning is scenario setup and runs identically
    /// on every peer.
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Company Scenario", fileName = "CompanyScenario")]
    public class CompanyScenarioDefinition : ScriptableObject, ISimScenario
    {
        [Serializable]
        public class Unit
        {
            public string name = "Formation";
            [Tooltip("0 = the player's side (player 0 commands it), 1 = the enemy (player 1, no human: keeps its start behavior).")]
            [Range(0, 1)] public int team;
            public FormationType formationType = FormationType.Line;
            [Min(1)] public int count = 12;
            [Tooltip("Soldiers per rank.")]
            [Min(1)] public int files = 6;
            [Range(1, 5)] public int botLevel = 3;
            [Tooltip("Weapon id: index in weapons (0 = sword and shield, 1 = two-hander).")]
            [Min(0)] public int weapon;
            [Tooltip("Centre of the front rank (map coordinates in m).")]
            public Vector2 front;
            [Tooltip("Point the formation faces at the start.")]
            public Vector2 faceTowards;
            [Tooltip("What it does from the start until it gets an order.")]
            public FormationBehavior startBehavior = FormationBehavior.Hold;
        }

        public int seed = 1;
        [Tooltip("Ticks between pressing a key and the sim acting on it. Singleplayer: 1 (windup visible within 50 ms).")]
        [Min(0)] public int inputDelayTicks = 1;
        [Tooltip("Map size in 1 m cells (square).")]
        [Min(16)] public int mapSize = 96;
        [Min(0.05f)] public float agentRadius = 0.3f;

        [Header("Combat and morale")]
        [Tooltip("Index = weapon id in the sim. 0 = one-hand + shield, 1 = two-hand.")]
        public WeaponDefinition[] weapons = Array.Empty<WeaponDefinition>();
        [Tooltip("Feel preset: damage, stamina, timing, hitstop.")]
        public CombatTuningDefinition combatTuning;
        [Tooltip("Bot levels; empty = defaults.")]
        public BotRosterDefinition roster;
        [Tooltip("Formation spacing, march and fighting in formation. Empty = defaults.")]
        public FormationDefinition formation;
        [Tooltip("Panic, morale and rout; empty = nobody breaks.")]
        public MoraleDefinition morale;

        [Header("Hero (player 0, team 0)")]
        public Vector2 heroPosition = new Vector2(48f, 30f);
        public Vector2 heroFacesTowards = new Vector2(48f, 70f);
        [Tooltip("Weapon ids in the hero's slots; the first is drawn at the start.")]
        public int[] heroLoadout = { 0, 1 };
        [Tooltip("The hero runs and turns like the duel fighters (an elite soldier); line soldiers march at 1.")]
        [Min(0.1f)] public float heroSpeedScale = 1.6f;
        [Min(0.1f)] public float heroAccelScale = 4f;

        [Header("Formations")]
        public Unit[] units = Array.Empty<Unit>();

        public const ushort PlayerId = 0, EnemyId = 1;

        /// <summary>
        /// The M2 starting layout on a 96 m map: the hero between his line and his shield wall, two enemy formations
        /// 36 m away that advance at once (contact after about half a minute, time for the first orders).
        /// </summary>
        public void ApplyDefaultLayout()
        {
            mapSize = 96;
            heroPosition = new Vector2(48f, 30f);
            heroFacesTowards = new Vector2(48f, 70f);
            units = new[]
            {
                new Unit { name = "Linie", team = 0, formationType = FormationType.Line, count = 12, files = 6, front = new Vector2(40f, 30f), faceTowards = new Vector2(40f, 70f) },
                new Unit { name = "Schildwall", team = 0, formationType = FormationType.ShieldWall, count = 12, files = 6, front = new Vector2(56f, 30f), faceTowards = new Vector2(56f, 70f) },
                new Unit { name = "Feind Linie", team = 1, formationType = FormationType.Line, count = 12, files = 6, front = new Vector2(41f, 66f), faceTowards = new Vector2(41f, 30f), startBehavior = FormationBehavior.Advance },
                new Unit { name = "Feind Keil", team = 1, formationType = FormationType.Wedge, count = 12, files = 7, front = new Vector2(55f, 66f), faceTowards = new Vector2(55f, 30f), startBehavior = FormationBehavior.Advance },
            };
        }

        public int TotalAgents
        {
            get
            {
                int n = 1;
                foreach (var u in units) n += Mathf.Min(u.count, FormationLimits.MaxMembers);
                return n;
            }
        }

        public SimConfig ToConfig(AgentMoveSettings movement) => new SimConfig
        {
            Seed = (ulong)(uint)seed,
            InputDelayTicks = inputDelayTicks,
            GridWidth = mapSize,
            GridHeight = mapSize,
            MaxAgents = TotalAgents,
            Movement = movement,
            MaxFormations = units.Length,
            Formations = BuildFormationSettings(),
            Morale = morale != null ? morale.ToSettings() : default,
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

        /// <summary>Hero slots: valid weapon ids from heroLoadout, the rest empty; falls back to weapon 0.</summary>
        public int4 HeroLoadout()
        {
            var slots = new int4(-1);
            int n = 0;
            if (heroLoadout != null)
                foreach (int w in heroLoadout)
                    if (w >= 0 && w < weapons.Length && n < 4) slots[n++] = w;
            if (n == 0) slots.x = 0;
            return slots;
        }

        public SimWorld CreateWorld(AgentMoveSettings movement, out int playerAgent)
        {
            if (weapons.Length == 0 || combatTuning == null) throw new InvalidOperationException($"{name}: needs weapons and a combat tuning.");
            SimWorld world;
            using (var setup = CombatDatabase.Build(weapons, combatTuning, roster, Allocator.Temp))
                world = new SimWorld(ToConfig(movement), setup);
            playerAgent = Spawn(world);
            return world;
        }

        /// <summary>Spawns the hero (agent 0), then every unit in list order; returns the hero's agent id.</summary>
        public int Spawn(SimWorld world)
        {
            float2 heroFacing = math.normalizesafe((float2)(heroFacesTowards - heroPosition), new float2(0f, 1f));
            int hero = world.SpawnAgent(0, heroPosition, agentRadius, heroFacing, HeroLoadout().x);
            world.SetAgentMotion(hero, heroSpeedScale, heroAccelScale);
            world.SetLoadout(hero, HeroLoadout());
            world.AssignPlayerAgent(PlayerId, hero);

            foreach (var u in units)
            {
                int team = math.clamp(u.team, 0, 1);
                ushort owner = team == 0 ? PlayerId : EnemyId;
                float2 facing = math.normalizesafe((float2)(u.faceTowards - u.front), new float2(0f, team == 0 ? 1f : -1f));
                int weapon = math.clamp(u.weapon, 0, world.CombatSetup.Weapons.Length - 1);
                int level = math.clamp(u.botLevel, 1, world.CombatSetup.BotProfiles.Length - 1);
                int count = math.clamp(u.count, 1, FormationLimits.MaxMembers);
                int f = world.SpawnFormation(team, owner, u.formationType, u.files, count, u.front, facing, agentRadius, weapon, level);
                // Setup, not an input: every peer applies the same start behavior before tick 0
                if (u.startBehavior != FormationBehavior.Hold) world.ApplyFormationOrder(FormationOrder.SetBehavior(f, u.startBehavior), owner);
            }
            return hero;
        }
    }
}
