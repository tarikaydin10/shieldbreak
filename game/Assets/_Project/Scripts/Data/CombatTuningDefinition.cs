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
    /// <summary>Grammar rules and feel regulators (CLAUDE.md 3.5). One asset per preset (Schwer / Mittel / Tödlich).</summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Combat Tuning", fileName = "CombatTuning")]
    public class CombatTuningDefinition : ScriptableObject
    {
        [Header("Resources")]
        [Min(1f)] public float healthMax = 100f;
        [Min(1f)] public float staminaMax = 100f;
        [Min(0f)] public float staminaRegenPerSecond = 20f;
        [Tooltip("Stamina starts to regenerate this long after it was last spent (ms).")]
        [Min(0f)] public float staminaRegenDelayMs = 750f;
        [Min(0f)] public float attackStaminaCost = 10f;
        [Min(0f)] public float feintStaminaCost = 6f;
        [Tooltip("Stamina a blocker loses per joule of blocked effective energy.")]
        [Min(0f)] public float blockStaminaPerJoule = 0.08f;

        [Header("Timing windows (ms)")]
        [Tooltip("A block raised at most this long before contact is perfect (costs nothing, staggers the attacker).")]
        [Min(0f)] public float perfectBlockMs = 250f;
        [Min(0f)] public float inputBufferMs = 150f;
        [Range(0f, 1f)] public float feintWindow = 0.6f;
        [Range(0f, 1f)] public float morphWindow = 0.4f;
        [Min(0f)] public float guardBreakStunMs = 1000f;
        [Min(0f)] public float hitStunMs = 300f;
        [Tooltip("Stun of an attacker whose swing met a perfect block: the riposte window.")]
        [Min(0f)] public float perfectBlockStunMs = 750f;

        [Header("Lethality")]
        [Tooltip("Health lost per joule above the zone threshold. Preset Mittel: about 3 torso or 2 head hits.")]
        [Min(0f)] public float damagePerJoule = 0.085f;
        [Tooltip("Armour as a threshold in joules: legs, torso, head.")]
        public Vector3 zoneThreshold = new Vector3(15f, 30f, 20f);
        public Vector3 zoneMultiplier = new Vector3(0.8f, 1f, 1.6f);
        [Tooltip("Energy kept per additional target of a side cut.")]
        [Range(0f, 1f)] public float multiHitEnergyKeep = 0.6f;

        [Header("Feel (presentation only, not part of the sim)")]
        [Tooltip("Hitstop for a weak and a full-energy impact (ms); scales with effective energy.")]
        public Vector2 hitstopMs = new Vector2(40f, 80f);
        [Tooltip("Effective energy (J) that gives the maximum hitstop.")]
        [Min(1f)] public float hitstopFullEnergy = 500f;

        [Header("Groups (1vX)")]
        [Tooltip("Bots that may swing at the same target at once; the rest hold back and threaten (0 = unlimited).")]
        [Min(0)] public int maxAttackersPerTarget = 2;
        [Tooltip("Extra distance waiting bots keep (m).")]
        [Min(0f)] public float waitingDistance = 1.2f;

        [Header("Sprint, step, special")]
        [Min(1f)] public float sprintSpeedScale = 1.6f;
        [Min(0f)] public float sprintStaminaPerSecond = 12f;
        [Tooltip("Dodge step duration (ms) and distance (m).")]
        [Min(17f)] public float stepMs = 300f;
        [Min(0.1f)] public float stepDistance = 2f;
        [Min(0f)] public float stepStaminaCost = 12f;
        [Tooltip("Shield bash: windup, active and recovery (ms).")]
        [Min(17f)] public float specialWindupMs = 250f;
        [Min(17f)] public float specialActiveMs = 100f;
        [Min(0f)] public float specialRecoveryMs = 250f;
        [Min(0.3f)] public float specialRange = 1.3f;
        [Range(10f, 180f)] public float specialArcDeg = 70f;
        [Min(0f)] public float specialStaminaCost = 18f;
        [Tooltip("Stun and damage when the bash hits a body instead of a block.")]
        [Min(0f)] public float specialStunMs = 800f;
        [Min(0f)] public float specialDamage = 6f;

        [Header("Jump")]
        [Tooltip("Airborne time (ms); legs cannot be hit while airborne, nothing else can be done.")]
        [Min(17f)] public float jumpMs = 450f;
        [Tooltip("Landing recovery (ms).")]
        [Min(0f)] public float jumpLandMs = 120f;
        [Min(0f)] public float jumpStaminaCost = 10f;
        [Tooltip("Apex height (m), presentation only.")]
        [Min(0f)] public float jumpHeight = 0.6f;

        [Header("Weapon switch, lunge")]
        [Tooltip("Drawing another weapon from the loadout (CLAUDE.md: costs a recovery).")]
        [Min(17f)] public float weaponSwitchMs = 450f;
        [Tooltip("An attack lunge stops this far short of the enemy in front (m).")]
        [Min(0f)] public float lungeStopGap = 0.45f;

        [Header("Knockback (stagger step)")]
        [Tooltip("Push speed per joule of effective energy on a hit (m/s per J). 500 J -> 3 m/s -> about 30 cm.")]
        [Min(0f)] public float knockbackPerJoule = 0.006f;
        [Min(0f)] public float knockbackMax = 4.5f;
        [Tooltip("Share of the push for a blocker, and for an attacker bounced by a perfect block.")]
        [Range(0f, 1f)] public float blockKnockbackScale = 0.5f;
        [Tooltip("Push of the shield bash (m/s). Small enough that the quick follow-up thrust still reaches.")]
        [Min(0f)] public float bashKnockback = 1.5f;
        [Tooltip("Knockback kept per tick; 0.85 fades out in about 0.3 s.")]
        [Range(0f, 0.98f)] public float knockbackDecay = 0.85f;

        [Header("Height")]
        [Tooltip("Energy of a blow gained per metre the attacker stands above the target (0.25 = 25 %), lost per metre below.")]
        [Min(0f)] public float heightEnergyPerMeter = 0.25f;
        [Tooltip("The most height can add to or take off a blow.")]
        [Range(0f, 0.9f)] public float heightEnergyLimit = 0.3f;

        [Header("Forest")]
        [Tooltip("Chance per metre below the crowns that a tree stops an arrow.")]
        [Range(0f, 1f)] public float forestArrowStopPerMeter = 0.1f;
        [Tooltip("Above this height over the ground an arrow flies over the forest (m).")]
        [Min(0f)] public float forestCrownHeight = 7f;

        [Header("Cavalry")]
        [Tooltip("Mass of horse and rider behind a charge (kg); the energy is ½·m·v² of the closing speed.")]
        [Min(1f)] public float horseMass = 500f;
        [Tooltip("Below this closing speed (m/s) a horse pushes, it runs nobody down.")]
        [Min(0.1f)] public float trampleMinSpeed = 3f;
        [Tooltip("The horse's chest is this far (m) ahead of the rider's centre.")]
        [Min(0f)] public float trampleReach = 0.9f;
        [Tooltip("Share of the charge energy that strikes a man run down (blunt). 500 kg at a gallop: about 40 damage.")]
        [Min(0f)] public float trampleEnergyShare = 0.025f;
        [Tooltip("Speed the horse keeps per man it runs down.")]
        [Range(0f, 1f)] public float trampleSpeedKeep = 0.75f;
        [Tooltip("A horse strikes the same man again at the earliest after this (ms).")]
        [Min(17f)] public float trampleCooldownMs = 500f;
        [Tooltip("Share of the charge energy a braced spear puts into the rider, times its point factor. At a gallop deadly, at a trot a wound.")]
        [Min(0f)] public float braceEnergyShare = 0.02f;
        [Tooltip("Speed a horse keeps when it refuses a shield wall.")]
        [Range(0f, 1f)] public float shieldWallSpeedKeep = 0.15f;
        [Tooltip("The rider of a refusing horse is shaken this long (ms).")]
        [Min(0f)] public float refuseStunMs = 600f;
        [Tooltip("Below this closing speed (m/s) a couched lance does not pierce.")]
        [Min(0.1f)] public float couchMinSpeed = 3f;
        [Tooltip("Rider bots lower the lance this far (m) from their target.")]
        [Min(1f)] public float couchRange = 14f;
        [Tooltip("Rider bots aim this far (m) behind their target, so they ride through.")]
        [Min(0f)] public float rideThroughDistance = 12f;
        [Tooltip("After a pass rider bots ride on this far (m) before they turn.")]
        [Min(1f)] public float rideOutDistance = 25f;
        [Tooltip("Soldier bots notice a rider charging at them from this far (m).")]
        [Min(1f)] public float riderWatchRange = 20f;

        [Header("Movement in combat")]
        [Range(0f, 1f)] public float attackSpeedScale = 0.5f;
        [Range(0f, 1f)] public float blockSpeedScale = 0.6f;
        [Range(0f, 1f)] public float stunSpeedScale = 0.3f;

        public CombatTuning ToTuning() => new CombatTuning
        {
            HealthMax = healthMax,
            StaminaMax = staminaMax,
            StaminaRegenPerSecond = staminaRegenPerSecond,
            StaminaRegenDelayTicks = AttackTiming.MsToTicks(staminaRegenDelayMs),
            AttackStaminaCost = attackStaminaCost,
            FeintStaminaCost = feintStaminaCost,
            BlockStaminaPerJoule = blockStaminaPerJoule,
            PerfectBlockTicks = AttackTiming.MsToTicks(perfectBlockMs),
            InputBufferTicks = AttackTiming.MsToTicks(inputBufferMs),
            FeintWindow = feintWindow,
            MorphWindow = morphWindow,
            GuardBreakStunTicks = AttackTiming.MsToTicks(guardBreakStunMs),
            HitStunTicks = AttackTiming.MsToTicks(hitStunMs),
            PerfectBlockStunTicks = AttackTiming.MsToTicks(perfectBlockStunMs),
            DamagePerJoule = damagePerJoule,
            ZoneThreshold = new float3(zoneThreshold.x, zoneThreshold.y, zoneThreshold.z),
            ZoneMultiplier = new float3(zoneMultiplier.x, zoneMultiplier.y, zoneMultiplier.z),
            AttackSpeedScale = attackSpeedScale,
            BlockSpeedScale = blockSpeedScale,
            StunSpeedScale = stunSpeedScale,
            MultiHitEnergyKeep = multiHitEnergyKeep,
            MaxAttackersPerTarget = maxAttackersPerTarget,
            WaitingDistance = waitingDistance,
            SprintSpeedScale = sprintSpeedScale,
            SprintStaminaPerSecond = sprintStaminaPerSecond,
            StepTicks = AttackTiming.MsToTicks(stepMs),
            StepDistance = stepDistance,
            StepStaminaCost = stepStaminaCost,
            SpecialWindupTicks = AttackTiming.MsToTicks(specialWindupMs),
            SpecialActiveTicks = AttackTiming.MsToTicks(specialActiveMs),
            SpecialRecoveryTicks = specialRecoveryMs <= 0f ? 0 : AttackTiming.MsToTicks(specialRecoveryMs),
            SpecialRange = specialRange,
            SpecialArcDeg = specialArcDeg,
            SpecialStaminaCost = specialStaminaCost,
            SpecialStunTicks = AttackTiming.MsToTicks(specialStunMs),
            SpecialDamage = specialDamage,
            WeaponSwitchTicks = AttackTiming.MsToTicks(weaponSwitchMs),
            LungeStopGap = lungeStopGap,
            JumpTicks = AttackTiming.MsToTicks(jumpMs),
            JumpLandTicks = jumpLandMs <= 0f ? 0 : AttackTiming.MsToTicks(jumpLandMs),
            JumpStaminaCost = jumpStaminaCost,
            JumpHeight = jumpHeight,
            KnockbackPerJoule = knockbackPerJoule,
            KnockbackMax = knockbackMax,
            BlockKnockbackScale = blockKnockbackScale,
            BashKnockback = bashKnockback,
            KnockbackDecay = knockbackDecay,
            HeightEnergyPerMeter = heightEnergyPerMeter,
            HeightEnergyLimit = heightEnergyLimit,
            ForestArrowStopPerMeter = forestArrowStopPerMeter,
            ForestCrownHeight = forestCrownHeight,
            HorseMass = horseMass,
            TrampleMinSpeed = trampleMinSpeed,
            TrampleReach = trampleReach,
            TrampleEnergyShare = trampleEnergyShare,
            TrampleSpeedKeep = trampleSpeedKeep,
            TrampleCooldownTicks = AttackTiming.MsToTicks(trampleCooldownMs),
            BraceEnergyShare = braceEnergyShare,
            ShieldWallSpeedKeep = shieldWallSpeedKeep,
            RefuseStunTicks = refuseStunMs <= 0f ? 0 : AttackTiming.MsToTicks(refuseStunMs),
            CouchMinSpeed = couchMinSpeed,
            CouchRange = couchRange,
            RideThroughDistance = rideThroughDistance,
            RideOutDistance = rideOutDistance,
            RiderWatchRange = riderWatchRange,
        };
    }
}
