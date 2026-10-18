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
    /// <summary>
    /// Builds combat setups from the default ScriptableObject values (preset "Mittel"): weapon 0 = one-hand + shield,
    /// weapon 1 = two-hand (same values the duel scene builder writes into new assets).
    /// </summary>
    static class TestCombat
    {
        public const int OneHand = 0, TwoHand = 1;

        public static CombatSetup Setup(Action<CombatTuningDefinition> tune = null)
        {
            var created = new List<Object>();
            try
            {
                var oneHand = ScriptableObject.CreateInstance<WeaponDefinition>();
                created.Add(oneHand);
                for (int d = 0; d < AttackDirection.Count; d++)
                {
                    var attack = ScriptableObject.CreateInstance<AttackDefinition>();
                    attack.ApplyDefaults(d);
                    oneHand.attacks[d] = attack;
                    created.Add(attack);
                }
                var twoHand = ScriptableObject.CreateInstance<WeaponDefinition>();
                created.Add(twoHand);
                twoHand.ApplyTwoHandDefaults();
                for (int d = 0; d < AttackDirection.Count; d++)
                {
                    var attack = ScriptableObject.CreateInstance<AttackDefinition>();
                    attack.ApplyTwoHandDefaults(d);
                    twoHand.attacks[d] = attack;
                    created.Add(attack);
                }
                twoHand.specialAttack = ScriptableObject.CreateInstance<AttackDefinition>();
                twoHand.specialAttack.ApplyLeapDefaults();
                created.Add(twoHand.specialAttack);

                var tuning = ScriptableObject.CreateInstance<CombatTuningDefinition>();
                created.Add(tuning);
                tune?.Invoke(tuning);
                return CombatDatabase.Build(new[] { oneHand, twoHand }, tuning, null, Allocator.Persistent);
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
