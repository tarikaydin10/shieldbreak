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
        /// <summary>Dodge step: a short fixed-speed move (Direction holds the StepDirection).</summary>
        public const int Stepping = 6;
        /// <summary>Non-attack archetype special (one-hand + shield: shield bash). Attack specials use Windup with AttackDirection.Special.</summary>
        public const int Special = 7;
        /// <summary>Drawing another weapon from the loadout; costs about one recovery.</summary>
        public const int Switching = 8;
        /// <summary>Airborne: keeps running, cannot block or attack, legs cannot be hit; lands into a short recovery.</summary>
        public const int Jumping = 9;
    }

    public static class StepDirection
    {
        public const int Forward = 0;
        public const int Back = 1;
        public const int Left = 2;
        public const int Right = 3;
    }

    /// <summary>Attack directions are geometry (CLAUDE.md 3.5): overhead hits the head zone, side cuts several targets, thrust reaches far.</summary>
    public static class AttackDirection
    {
        public const int Overhead = 0;
        public const int Left = 1;
        public const int Right = 2;
        public const int Thrust = 3;
        public const int Count = 4;
        /// <summary>Attack slot of an archetype special that is an attack (two-hander: leaping overhead). Not a player direction.</summary>
        public const int Special = 4;
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
        /// <summary>Weapon switch; BufferedDirection holds the loadout slot.</summary>
        public const int Switch = 3;
    }

    /// <summary>
    /// Combat state of one agent, parallel to AgentData (same index). Hashed raw: the ulong comes first and every other
    /// field is 4 bytes, 184 bytes in total without padding (checked by a test).
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
        /// <summary>Facing wanted while idle at OrderTarget (formation slot), zero = face the walking direction.</summary>
        public float2 OrderFacing;
        /// <summary>1 while sprinting (players via PlayerMove, bots when closing distance).</summary>
        public int Sprinting;
        /// <summary>Weapon ids in the loadout slots (CLAUDE.md: 3-4 slots), -1 = empty. WeaponId is the drawn one.</summary>
        public int4 Loadout;
        public int ActiveSlot;
        /// <summary>Weapon put away by the last switch (presentation draws it during Switching).</summary>
        public int PreviousWeaponId;
        /// <summary>Stagger push from hits and blocks (m/s); moves the body directly and decays every tick.</summary>
        public float2 Knockback;

        // Cavalry
        /// <summary>Last tick a horse ran this agent down; a horse strikes the same man once, not every tick it touches him.</summary>
        public int LastTrampledTick;
        /// <summary>Rider bot: bit 0 = riding out to turn (1) or charging (0); the bits above count the passes.</summary>
        public int RidePhase;
        /// <summary>Rider bot: where it rides out to before it turns for the next pass.</summary>
        public float2 RidePoint;
        /// <summary>Soldier bot: the rider it saw charging at it, -1 = none.</summary>
        public int BotRider;
        /// <summary>Soldier bot: its answer to that rider once it reacted (BotRiderAnswer).</summary>
        public int BotRiderPlan;
    }

    /// <summary>What a soldier does about a rider charging at him (decided once, after his reaction time).</summary>
    public static class BotRiderAnswer
    {
        public const int Undecided = 0;
        /// <summary>Shield up, or the spear braced: the block of a polearm against a rider is the brace.</summary>
        public const int Block = 1;
        /// <summary>A step aside just before the horse arrives.</summary>
        public const int Step = 2;
        /// <summary>Failed to answer: he stands there.</summary>
        public const int None = 3;
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
        public const int Step = 7;
        public const int SpecialStart = 8;
        public const int WeaponSwitch = 9;
        public const int Jump = 10;
        /// <summary>Morale broke: Target starts to flee (Sim/Morale).</summary>
        public const int Rout = 11;
        /// <summary>A fleeing soldier left the field at the map edge.</summary>
        public const int Fled = 12;
        /// <summary>An arrow left the bow (Target is whom it was meant for, -1 = nobody).</summary>
        public const int Shot = 13;
        /// <summary>A horse ran Target down (Attacker = the rider); a Hit or Blocked event with the damage follows.</summary>
        public const int Trample = 14;
        /// <summary>A rider ran onto a braced spear (Attacker = the spearman, Target = the rider).</summary>
        public const int Brace = 15;
        /// <summary>A horse refused a shield wall and stopped (Attacker = the rider, Target = the wall soldier).</summary>
        public const int Refuse = 16;
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

        /// <summary>1vX attack tokens: bots that may swing at the same target at once (0 = unlimited).</summary>
        public int MaxAttackersPerTarget;
        /// <summary>Extra distance waiting bots keep while another bot holds the token (m).</summary>
        public float WaitingDistance;

        public float SprintSpeedScale;
        public float SprintStaminaPerSecond;

        public int StepTicks;
        public float StepDistance;
        public float StepStaminaCost;

        public int SpecialWindupTicks;
        public int SpecialActiveTicks;
        public int SpecialRecoveryTicks;
        public float SpecialRange;
        public float SpecialArcDeg;
        public float SpecialStaminaCost;
        /// <summary>Stun and damage when the bash meets a body instead of a raised block.</summary>
        public int SpecialStunTicks;
        public float SpecialDamage;

        /// <summary>Duration of drawing another weapon (about one recovery).</summary>
        public int WeaponSwitchTicks;
        /// <summary>An attack lunge stops this far (m) short of the enemy in front instead of running through it.</summary>
        public float LungeStopGap;

        /// <summary>Stagger push per joule of effective energy on a hit (m/s per J), capped at KnockbackMax.</summary>
        public float KnockbackPerJoule;
        public float KnockbackMax;
        /// <summary>Share of the push a blocker (and a perfect-blocked attacker) gets.</summary>
        public float BlockKnockbackScale;
        /// <summary>Push of the shield bash (m/s).</summary>
        public float BashKnockback;
        /// <summary>Knockback kept per tick (0..1).</summary>
        public float KnockbackDecay;
        /// <summary>Energy gained per metre the attacker stands above the target, lost per metre below.</summary>
        public float HeightEnergyPerMeter;
        /// <summary>The most height can add to or take off a blow (0.3 = 30 %).</summary>
        public float HeightEnergyLimit;
        /// <summary>Chance per metre below the crowns that a tree stops an arrow.</summary>
        public float ForestArrowStopPerMeter;
        /// <summary>Above this height over the ground (m) an arrow flies over the forest.</summary>
        public float ForestCrownHeight;

        /// <summary>Jump: airborne ticks, landing recovery ticks, stamina cost, apex height (presentation).</summary>
        public int JumpTicks;
        public int JumpLandTicks;
        public float JumpStaminaCost;
        public float JumpHeight;

        /// <summary>Cavalry: mass of horse and rider (kg) behind a charge, E = ½·m·v² from the closing speed.</summary>
        public float HorseMass;
        /// <summary>Below this closing speed (m/s) a horse pushes, it does not run anybody down.</summary>
        public float TrampleMinSpeed;
        /// <summary>The horse's chest is this far (m) ahead of the rider's centre: contact at the sum of both radii and this.</summary>
        public float TrampleReach;
        /// <summary>Share of the charge energy that strikes the man run down (blunt).</summary>
        public float TrampleEnergyShare;
        /// <summary>Speed the horse keeps per man it runs down (0..1).</summary>
        public float TrampleSpeedKeep;
        public int TrampleCooldownTicks;
        /// <summary>Share of the charge energy a braced spear puts into the rider (times the spear's point factor).</summary>
        public float BraceEnergyShare;
        /// <summary>Speed a horse keeps when it refuses a shield wall (0..1); the rider is shaken for RefuseStunTicks.</summary>
        public float ShieldWallSpeedKeep;
        public int RefuseStunTicks;
        /// <summary>Below this closing speed (m/s) a couched lance does not pierce.</summary>
        public float CouchMinSpeed;
        /// <summary>Rider bot: lowers the lance this far (m) from its target.</summary>
        public float CouchRange;
        /// <summary>Rider bot: aims this far (m) behind its target, so it rides through instead of stopping at him.</summary>
        public float RideThroughDistance;
        /// <summary>Rider bot: after a pass it rides on this far (m) before it turns for the next.</summary>
        public float RideOutDistance;
        /// <summary>Soldier bot: notices a rider charging at it from this far (m).</summary>
        public float RiderWatchRange;

        public void Validate()
        {
            if (HealthMax <= 0f || StaminaMax <= 0f) throw new ArgumentException("HealthMax and StaminaMax must be positive.");
            if (FeintWindow < 0f || FeintWindow > 1f || MorphWindow < 0f || MorphWindow > 1f) throw new ArgumentException("Feint/morph windows are fractions of the windup.");
            if (PerfectBlockTicks < 0 || InputBufferTicks < 0) throw new ArgumentException("Tick windows must be >= 0.");
            if (DamagePerJoule < 0f) throw new ArgumentException("DamagePerJoule must be >= 0.");
            if (StepTicks < 1 || SpecialWindupTicks < 1 || SpecialActiveTicks < 1 || SpecialRecoveryTicks < 0) throw new ArgumentException("Step and special need positive tick lengths.");
            if (SprintSpeedScale < 1f) throw new ArgumentException("SprintSpeedScale must be >= 1.");
            if (WeaponSwitchTicks < 1) throw new ArgumentException("WeaponSwitchTicks must be >= 1.");
            if (JumpTicks < 1 || JumpLandTicks < 0) throw new ArgumentException("Jump needs positive airborne ticks.");
            if (HorseMass <= 0f || TrampleMinSpeed <= 0f || CouchMinSpeed <= 0f) throw new ArgumentException("HorseMass, TrampleMinSpeed and CouchMinSpeed must be positive.");
            if (TrampleSpeedKeep < 0f || TrampleSpeedKeep > 1f || ShieldWallSpeedKeep < 0f || ShieldWallSpeedKeep > 1f) throw new ArgumentException("TrampleSpeedKeep and ShieldWallSpeedKeep are shares (0..1).");
            if (TrampleReach < 0f || TrampleEnergyShare < 0f || BraceEnergyShare < 0f || TrampleCooldownTicks < 1 || RefuseStunTicks < 0) throw new ArgumentException("Cavalry: reach and shares >= 0, cooldown >= 1 tick.");
            if (KnockbackDecay < 0f || KnockbackDecay >= 1f || KnockbackPerJoule < 0f || KnockbackMax < 0f) throw new ArgumentException("Knockback: decay in [0, 1), other values >= 0.");
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
        /// <summary>Chance to dodge a seen attack with a back step (checked after perfect block and block).</summary>
        public float StepChance;
        /// <summary>Chance per decision to use the archetype special (shield bash on a raised block, two-hand leap).</summary>
        public float SpecialChance;
        /// <summary>Shooting: the aim point misses by up to this share of the distance (fewer at higher levels).</summary>
        public float AimErrorPerMeter;
    }
}
