using System;
using System.Collections.Generic;
using Shieldbreak.Sim.Combat.Archetypes;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>Turns the ScriptableObjects into the sim's CombatSetup tables.</summary>
    public static class CombatDatabase
    {
        /// <summary>Weapon ids in the sim are the indices in <paramref name="weapons"/>; bot level n is profile n.</summary>
        public static CombatSetup Build(IReadOnlyList<WeaponDefinition> weapons, CombatTuningDefinition tuning, BotRosterDefinition roster, Allocator allocator)
        {
            var levels = roster != null ? roster.levels : BotRosterDefinition.DefaultLevels();
            int attackCount = 0;
            int keyCount = 0;
            foreach (var w in weapons)
            {
                if (w.attacks == null || w.attacks.Length != AttackDirection.Count) throw new InvalidOperationException($"{w.name}: needs exactly four attacks.");
                foreach (var a in w.attacks)
                {
                    if (a == null) throw new InvalidOperationException($"{w.name}: missing attack.");
                    keyCount += a.keys.Length;
                }
                attackCount += AttackDirection.Count;
                if (w.HasSpecialAttack)
                {
                    if (w.specialAttack == null) throw new InvalidOperationException($"{w.name}: the {w.archetype} archetype needs a special attack.");
                    keyCount += w.specialAttack.keys.Length;
                    attackCount++;
                }
            }

            var setup = new CombatSetup(weapons.Count, attackCount, keyCount, levels.Length + 1, allocator) { Tuning = tuning.ToTuning() };
            int attackIndex = 0, keyIndex = 0;
            for (int w = 0; w < weapons.Count; w++)
            {
                var def = weapons[w];
                setup.Weapons[w] = new WeaponData
                {
                    Archetype = (int)def.archetype,
                    Mass = def.mass,
                    EdgeFactor = def.edgeFactor,
                    PointFactor = def.pointFactor,
                    BlockArcDeg = def.blockArcDeg,
                    BlockPressure = def.blockPressure,
                    FirstAttack = attackIndex,
                };
                var table = def.HasSpecialAttack ? new List<AttackDefinition>(def.attacks) { def.specialAttack } : new List<AttackDefinition>(def.attacks);
                foreach (var a in table)
                {
                    using (var keys = a.ToKeys(Allocator.Temp))
                        for (int k = 0; k < keys.Length; k++) setup.Keys[keyIndex + k] = keys[k];
                    setup.Attacks[attackIndex++] = a.ToAttackData(keyIndex);
                    keyIndex += a.keys.Length;
                }
            }
            for (int l = 0; l < levels.Length; l++) setup.BotProfiles[l + 1] = BotRosterDefinition.ToProfile(levels[l]);
            setup.Validate();
            return setup;
        }
    }
}
