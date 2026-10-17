using System;
using Shieldbreak.Sim.Agents;
using Shieldbreak.Sim.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// M1 duel arena: the player's hero against one or more bots, or two bots against each other (spectator).
    /// Feel presets (CombatTuningDefinition) and bot levels are switched at runtime by rebuilding the world.
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Duel Scenario", fileName = "DuelScenario")]
    public class DuelScenarioDefinition : ScriptableObject, ISimScenario
    {
        public int seed = 1;
        [Tooltip("Ticks between pressing a key and the sim acting on it. Singleplayer: 1 (CLAUDE.md: windup visible within 50 ms).")]
        [Min(0)] public int inputDelayTicks = 1;
        [Min(8)] public int arenaSize = 32;

        [Header("Combat")]
        [Tooltip("Index = weapon id in the sim. 0 = one-hand + shield.")]
        public WeaponDefinition[] weapons = Array.Empty<WeaponDefinition>();
        [Tooltip("Feel presets, e.g. Schwer / Mittel / Tödlich.")]
        public CombatTuningDefinition[] presets = Array.Empty<CombatTuningDefinition>();
        public int activePreset = 1;
        public BotRosterDefinition roster;

        [Header("Fighters")]
        [Range(1, 5)] public int botLevel = 3;
        [Range(1, 6)] public int opponents = 1;
        [Tooltip("Hero is played by a bot of this level instead of the player (watch two bots fight).")]
        public bool spectator;
        [Range(1, 5)] public int spectatorHeroLevel = 5;
        [Tooltip("Movement multipliers for every fighter (same for hero and bots: the AI never cheats).")]
        [Min(0.1f)] public float fighterSpeedScale = 1.6f;
        [Min(0.1f)] public float fighterAccelScale = 4f;
        [Min(1f)] public float startDistance = 6f;

        public CombatTuningDefinition ActivePreset => presets.Length == 0 ? null : presets[Mathf.Clamp(activePreset, 0, presets.Length - 1)];

        public SimWorld CreateWorld(AgentMoveSettings movement, out int playerAgent)
        {
            if (weapons.Length == 0 || ActivePreset == null) throw new InvalidOperationException($"{name}: needs weapons and at least one preset.");
            var config = new SimConfig
            {
                Seed = (ulong)(uint)seed,
                InputDelayTicks = inputDelayTicks,
                GridWidth = arenaSize,
                GridHeight = arenaSize,
                MaxAgents = 1 + opponents,
                Movement = movement,
            };
            SimWorld world;
            using (var setup = CombatDatabase.Build(weapons, ActivePreset, roster, Allocator.Temp))
                world = new SimWorld(config, setup);

            float2 centre = new float2(arenaSize * 0.5f, arenaSize * 0.5f);
            float2 heroPos = centre - new float2(0f, startDistance * 0.5f);
            int hero = world.SpawnAgent(0, heroPos, 0.3f, new float2(0f, 1f), 0, spectator ? spectatorHeroLevel : 0);
            world.SetAgentMotion(hero, fighterSpeedScale, fighterAccelScale);
            for (int i = 0; i < opponents; i++)
            {
                float lateral = (i - (opponents - 1) * 0.5f) * 2f;
                int bot = world.SpawnAgent(1, centre + new float2(lateral, startDistance * 0.5f), 0.3f, new float2(0f, -1f), 0, botLevel);
                world.SetAgentMotion(bot, fighterSpeedScale, fighterAccelScale);
            }
            playerAgent = spectator ? -1 : hero;
            if (!spectator) world.AssignPlayerAgent(0, hero);
            return world;
        }
    }
}
