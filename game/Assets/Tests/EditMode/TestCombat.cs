using System;
using System.Collections.Generic;
using Shieldbreak.Data;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shieldbreak.Tests
{
    /// <summary>Builds combat setups from the default ScriptableObject values (one-hand weapon, preset "Mittel").</summary>
    static class TestCombat
    {
        public static CombatSetup Setup(Action<CombatTuningDefinition> tune = null)
        {
            var created = new List<Object>();
            try
            {
                var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                created.Add(weapon);
                for (int d = 0; d < AttackDirection.Count; d++)
                {
                    var attack = ScriptableObject.CreateInstance<AttackDefinition>();
                    attack.ApplyDefaults(d);
                    weapon.attacks[d] = attack;
                    created.Add(attack);
                }
                var tuning = ScriptableObject.CreateInstance<CombatTuningDefinition>();
                created.Add(tuning);
                tune?.Invoke(tuning);
                return CombatDatabase.Build(new[] { weapon }, tuning, null, Allocator.Persistent);
            }
            finally
            {
                foreach (var o in created) Object.DestroyImmediate(o);
            }
        }

        public static SimWorld World(CombatSetup setup, int maxAgents, int size = 32, ulong seed = 1) =>
            new SimWorld(TestScenarios.Config(seed, size, maxAgents), setup);
    }

    /// <summary>Enqueues commands for exact ticks with per-player sequence numbers.</summary>
    sealed class InputDriver
    {
        readonly SimWorld world;
        readonly int[] sequence = new int[SimConstants.MaxPlayers];

        public InputDriver(SimWorld world) => this.world = world;

        public void At(int tick, ushort player, InputCommandType type, int x, int y = 0) =>
            world.Enqueue(new InputCommand { Tick = tick, PlayerId = player, Sequence = sequence[player]++, Type = type, IntArgs = new int4(x, y, 0, 0) });

        /// <summary>Steps until the world reaches <paramref name="tick"/>, collecting events.</summary>
        public void RunUntil(int tick, List<CombatEvent> log = null)
        {
            while (world.Tick < tick)
            {
                world.Step();
                if (log != null) foreach (var e in world.LastEvents) log.Add(e);
            }
        }
    }
}
