using System;
using Unity.Collections;
using Unity.Mathematics;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;

namespace Shieldbreak.Sim.Combat.Archetypes
{
    /// <summary>Archetypes (CLAUDE.md 3.5, Schicht B): each has its own special move.</summary>
    public static class WeaponArchetype
    {
        /// <summary>Shield on the arm, 120° block; special: shield bash (breaks a raised block).</summary>
        public const int OneHandShield = 0;
        /// <summary>Both hands on the weapon, 90° block, heavy block pressure; special: leaping overhead (an attack with a lunge).</summary>
        public const int TwoHand = 1;
    }

    /// <summary>
    /// One weapon (Schicht C instance of an archetype). Attacks live in the shared attack table; each weapon points
    /// at four entries, one per AttackDirection.
    /// </summary>
    public struct WeaponData
    {
        public int Archetype;
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
        /// <summary>Index of the first of four AttackData entries (Overhead, Left, Right, Thrust), followed by the special attack for archetypes that have one.</summary>
        public int FirstAttack;

        public bool HasSpecialAttack => Archetype == WeaponArchetype.TwoHand;

        /// <summary>Attack table index for a direction; AttackDirection.Special maps to the special attack if the archetype has one.</summary>
        public int AttackIndex(int direction) =>
            FirstAttack + (direction == AttackDirection.Special && HasSpecialAttack ? AttackDirection.Special : math.clamp(direction, 0, AttackDirection.Count - 1));
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
        /// <summary>Forward lunge (root motion, CLAUDE.md: only for attack lunges) between two pose times; adds to the hit energy.</summary>
        public float LungeDistance;
        public float LungeStartPose;
        public float LungeEndPose;
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
                int needed = AttackDirection.Count + (weapon.HasSpecialAttack ? 1 : 0);
                if (weapon.FirstAttack < 0 || weapon.FirstAttack + needed > Attacks.Length)
                    throw new ArgumentException($"Weapon {w}: needs {needed} attacks in the table.");
            }
            for (int a = 0; a < Attacks.Length; a++)
            {
                var attack = Attacks[a];
                attack.Timing.Validate();
                if (attack.KeyCount < 2 || attack.FirstKey < 0 || attack.FirstKey + attack.KeyCount > Keys.Length)
                    throw new ArgumentException($"Attack {a}: key range out of bounds.");
                if (attack.SocketCount < 1 || attack.BladeLength <= 0f) throw new ArgumentException($"Attack {a}: blade needs length and sockets.");
                if (attack.MaxTargets < 1 || attack.MaxTargets > 4) throw new ArgumentException($"Attack {a}: MaxTargets must be 1..4.");
                if (attack.LungeDistance < 0f || (attack.LungeDistance > 0f && !(attack.LungeStartPose >= 0f && attack.LungeStartPose < attack.LungeEndPose && attack.LungeEndPose <= 1f)))
                    throw new ArgumentException($"Attack {a}: lunge needs 0 <= start < end <= 1.");
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
