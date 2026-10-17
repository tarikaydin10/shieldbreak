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
        };
    }
}
