using System;
using Unity.Collections;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;

namespace Shieldbreak.Sim.Combat.Archetypes
{
    /// <summary>
    /// One weapon (Schicht C instance of an archetype). Attacks live in the shared attack table; each weapon points
    /// at four entries, one per AttackDirection.
    /// </summary>
    public struct WeaponData
    {
        /// <summary>kg; enters E = ½·m·v².</summary>
        public float Mass;
        /// <summary>Penetration factor for motion across the blade (cuts).</summary>
        public float EdgeFactor;
        /// <summary>Penetration factor for motion along the blade (thrusts).</summary>
        public float PointFactor;
        /// <summary>Front arc the block covers, in degrees (shield ~120, weapon ~90).</summary>
        public float BlockArcDeg;
        /// <summary>Multiplier on the stamina a blocker loses against this weapon (two-handers press harder).</summary>
        public float BlockPressure;
        /// <summary>Index of the first of four AttackData entries (Overhead, Left, Right, Thrust).</summary>
        public int FirstAttack;
    }

    public struct AttackData
    {
        public AttackTiming Timing;
        public int FirstKey;
        public int KeyCount;
        public float BladeLength;
        public int SocketCount;
        /// <summary>Targets one swing can hit (overhead 1, side cuts several in a row).</summary>
        public int MaxTargets;
    }

    /// <summary>
    /// Everything combat needs that is not per-agent state: weapons, attacks, path keys, rules and bot profiles.
    /// Built once from ScriptableObjects (CombatDatabase in the Data assembly) and copied into the SimWorld.
    /// </summary>
    public sealed class CombatSetup : IDisposable
    {
        public NativeArray<WeaponData> Weapons;
        public NativeArray<AttackData> Attacks;
        public NativeArray<WeaponPathKey> Keys;
        public NativeArray<BotProfile> BotProfiles;
        public CombatTuning Tuning;

        public CombatSetup(int weapons, int attacks, int keys, int botProfiles, Allocator allocator)
        {
            Weapons = new NativeArray<WeaponData>(weapons, allocator);
            Attacks = new NativeArray<AttackData>(attacks, allocator);
            Keys = new NativeArray<WeaponPathKey>(keys, allocator);
            BotProfiles = new NativeArray<BotProfile>(botProfiles, allocator);
        }

        public void Validate()
        {
            Tuning.Validate();
            for (int w = 0; w < Weapons.Length; w++)
            {
                var weapon = Weapons[w];
                if (weapon.Mass <= 0f) throw new ArgumentException($"Weapon {w}: mass must be positive.");
                if (weapon.FirstAttack < 0 || weapon.FirstAttack + AttackDirection.Count > Attacks.Length)
                    throw new ArgumentException($"Weapon {w}: needs four attacks in the table.");
            }
            for (int a = 0; a < Attacks.Length; a++)
            {
                var attack = Attacks[a];
                attack.Timing.Validate();
                if (attack.KeyCount < 2 || attack.FirstKey < 0 || attack.FirstKey + attack.KeyCount > Keys.Length)
                    throw new ArgumentException($"Attack {a}: key range out of bounds.");
                if (attack.SocketCount < 1 || attack.BladeLength <= 0f) throw new ArgumentException($"Attack {a}: blade needs length and sockets.");
                if (attack.MaxTargets < 1 || attack.MaxTargets > 4) throw new ArgumentException($"Attack {a}: MaxTargets must be 1..4.");
            }
            // Profile 0 means "no bot"; its values are never read
            for (int p = 1; p < BotProfiles.Length; p++)
            {
                var b = BotProfiles[p];
                if (b.ReactionMinTicks < 0 || b.ReactionMaxTicks < b.ReactionMinTicks) throw new ArgumentException($"Bot profile {p}: bad reaction range.");
                if (b.DecisionIntervalTicks < 1 || b.EngageRange <= 0f) throw new ArgumentException($"Bot profile {p}: bad decision interval or range.");
            }
        }

        public void Dispose()
        {
            if (Weapons.IsCreated) Weapons.Dispose();
            if (Attacks.IsCreated) Attacks.Dispose();
            if (Keys.IsCreated) Keys.Dispose();
            if (BotProfiles.IsCreated) BotProfiles.Dispose();
        }
    }
}
