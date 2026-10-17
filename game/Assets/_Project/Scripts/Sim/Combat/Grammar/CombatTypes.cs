using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Shieldbreak.Sim.Combat.Grammar
{
    public static class CombatState
    {
        public const int Idle = 0;
        public const int Windup = 1;
        public const int Release = 2;
        public const int Recovery = 3;
        public const int Stunned = 4;
        public const int Dead = 5;
    }

    /// <summary>Attack directions are geometry (CLAUDE.md 3.5): overhead hits the head zone, side cuts several targets, thrust reaches far.</summary>
    public static class AttackDirection
    {
        public const int Overhead = 0;
        public const int Left = 1;
        public const int Right = 2;
        public const int Thrust = 3;
        public const int Count = 4;
    }

    public static class BodyZone
    {
        public const int Legs = 0;
        public const int Torso = 1;
        public const int Head = 2;
        public const int Count = 3;
    }

    public static class BufferedAction
    {
        public const int None = 0;
        public const int Attack = 1;
        public const int Block = 2;
    }

    /// <summary>
    /// Combat state of one agent, parallel to AgentData (same index). Hashed raw: the ulong comes first and every other
    /// field is 4 bytes; Reserved fills the tail, so the 120 bytes contain no padding (checked by a test).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CombatData
    {
        /// <summary>Per-agent xorshift state for bot decisions (seeded from world seed and id).</summary>
        public ulong RngState;
        public int State;
        public int Direction;
        /// <summary>Ticks since the current state began (for attacks: since the windup started).</summary>
        public int StateTick;
        /// <summary>Length of a Stunned state in ticks.</summary>
        public int StunTicks;
        public int WeaponId;
        public int BlockHeld;
        public int BlockPressedTick;
        public int LastStaminaUseTick;
        public float Stamina;
        public float Health;
        public int Buffered;
        public int BufferedDirection;
        public int BufferedTick;
        /// <summary>Agents already hit by the current swing (up to 4), -1 = empty.</summary>
        public int4 HitIds;
        public int HitCount;

        // Bot brain (0 = no bot: player or idle soldier)
        public int BotProfile;
        public int BotTarget;
        public int BotNextDecisionTick;
        public int BotThreatStartTick;
        public int BotReactAtTick;
        public int BotPlannedBlockTick;
        public int BotDefenseDecidedFor;
        /// <summary>Where the agent goes when it has no enemy to engage (set by move orders).</summary>
        public float2 OrderTarget;
        /// <summary>Explicit tail padding: the ulong makes the struct 8-byte aligned (116 -> 120 bytes); implicit padding would hash garbage.</summary>
        public int Reserved;
    }

    public static class CombatEventType
    {
        public const int Hit = 0;
        public const int Blocked = 1;
        public const int PerfectBlock = 2;
        public const int GuardBreak = 3;
        public const int Death = 4;
        public const int Feint = 5;
        public const int AttackStart = 6;
    }

    /// <summary>What happened in a tick, for presentation (hitstop, sounds, effects) and tests. Not part of the hash.</summary>
    public struct CombatEvent
    {
        public int Type;
        public int Attacker;
        public int Target;
        public int Zone;
        public int Direction;
        public float Energy;
        public float Damage;
        public float3 Point;
    }

    /// <summary>
    /// Grammar-wide rules (Schicht A) and the damage scale (Schicht C feel regulators). All values come from the
    /// CombatTuningDefinition ScriptableObject.
    /// </summary>
    public struct CombatTuning
    {
        public float HealthMax;
        public float StaminaMax;
        public float StaminaRegenPerSecond;
        public int StaminaRegenDelayTicks;
        public float AttackStaminaCost;
        public float FeintStaminaCost;
        /// <summary>Stamina lost per joule of blocked effective energy (times the attacker weapon's BlockPressure).</summary>
        public float BlockStaminaPerJoule;
        /// <summary>A block pressed at most this many ticks before contact is perfect.</summary>
        public int PerfectBlockTicks;
        public int InputBufferTicks;
        /// <summary>Feint allowed in this fraction of the windup.</summary>
        public float FeintWindow;
        /// <summary>Direction change (morph) allowed in this fraction of the windup.</summary>
        public float MorphWindow;
        public int GuardBreakStunTicks;
        public int HitStunTicks;
        /// <summary>Stun of an attacker whose swing met a perfect block (riposte window).</summary>
        public int PerfectBlockStunTicks;
        public float DamagePerJoule;
        /// <summary>Armour threshold per zone in joules (legs, torso, head): energy below it does no damage.</summary>
        public float3 ZoneThreshold;
        public float3 ZoneMultiplier;
        /// <summary>Speed multipliers while attacking, blocking and stunned.</summary>
        public float AttackSpeedScale;
        public float BlockSpeedScale;
        public float StunSpeedScale;
        /// <summary>Energy lost per additional target of a multi-target swing (0..1 kept).</summary>
        public float MultiHitEnergyKeep;

        public void Validate()
        {
            if (HealthMax <= 0f || StaminaMax <= 0f) throw new ArgumentException("HealthMax and StaminaMax must be positive.");
            if (FeintWindow < 0f || FeintWindow > 1f || MorphWindow < 0f || MorphWindow > 1f) throw new ArgumentException("Feint/morph windows are fractions of the windup.");
            if (PerfectBlockTicks < 0 || InputBufferTicks < 0) throw new ArgumentException("Tick windows must be >= 0.");
            if (DamagePerJoule < 0f) throw new ArgumentException("DamagePerJoule must be >= 0.");
        }
    }

    /// <summary>
    /// Bot skill (CLAUDE.md 3.5): bots play the same grammar; levels differ only in reaction time, error rates and
    /// allowed techniques, never in stats.
    /// </summary>
    public struct BotProfile
    {
        public int ReactionMinTicks;
        public int ReactionMaxTicks;
        /// <summary>Chance to block a seen attack.</summary>
        public float BlockChance;
        /// <summary>Chance to time the block as a perfect block (checked before BlockChance).</summary>
        public float PerfectBlockChance;
        public float FeintChance;
        /// <summary>Chance per decision to start an attack when in range and not threatened.</summary>
        public float AttackChance;
        /// <summary>Attack chance when the enemy is open (recovering or stunned).</summary>
        public float OpeningAttackChance;
        public int DecisionIntervalTicks;
        /// <summary>Distance the bot keeps to its enemy (m).</summary>
        public float EngageRange;
        public float AcquireRange;
        /// <summary>1 = keeps the block raised whenever it is not attacking (level 1).</summary>
        public int HoldBlock;
    }
}
