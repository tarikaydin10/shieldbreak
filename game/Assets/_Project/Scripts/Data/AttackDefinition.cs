using System;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Combat.Resolvers;
using Unity.Collections;
using UnityEngine;

namespace Shieldbreak.Data
{
    /// <summary>
    /// One attack (Schicht C): phase durations, weapon path keys in the attacker's local frame and blade sockets.
    /// Grammar timing and SwingTrace both read from here, so what the player sees and what the sim hits match
    /// (CLAUDE.md 3.5). Defaults are the one-hand overhead cut from docs/animation/overhead-hieb.md, preset "Mittel".
    /// </summary>
    [CreateAssetMenu(menuName = "Shieldbreak/Attack", fileName = "Attack")]
    public class AttackDefinition : ScriptableObject
    {
        [Serializable]
        public class Key
        {
            [Range(0f, 1f)] public float time;
            [Tooltip("Grip position in the attacker's frame (x right, y up, z forward, m).")]
            public Vector3 hand;
            [Tooltip("Direction from grip to blade tip.")]
            public Vector3 blade;
            public Ease easeToNext = Ease.InOut;
            [Min(1f)] public float power = 2f;
        }

        [Header("Phases (ms)")]
        [Min(17f)] public float windupMs = 400f;
        [Min(17f)] public float releaseMs = 150f;
        [Min(17f)] public float recoveryMs = 200f;
        [Tooltip("Pose time at the end of the windup / start of the release.")]
        [Range(0.05f, 0.9f)] public float releaseStartPose = 0.55f;
        [Tooltip("Pose time at the end of the release / start of the recovery.")]
        [Range(0.1f, 0.95f)] public float recoveryStartPose = 0.75f;

        [Header("Blade")]
        [Min(0.1f)] public float bladeLength = 0.8f;
        [Tooltip("Sockets along the blade for SwingTrace (CLAUDE.md: 3-4).")]
        [Range(2, 6)] public int socketCount = 4;

        [Header("Weapon path")]
        public Key[] keys =
        {
            // P0 idle guard: weapon diagonal in front of the body
            new Key { time = 0.00f, hand = new Vector3(0.25f, 1.10f, 0.35f), blade = new Vector3(-0.2f, 0.7f, 0.7f), easeToNext = Ease.InOut, power = 2.5f },
            // P1 windup end: weapon vertical above the head, tip tilted slightly back
            new Key { time = 0.55f, hand = new Vector3(0.15f, 1.95f, -0.05f), blade = new Vector3(0f, 0.95f, -0.3f), easeToNext = Ease.InOut, power = 2f },
            // P2 contact: blade horizontal at the target's chest, arm extended
            new Key { time = 0.70f, hand = new Vector3(0.10f, 1.35f, 0.55f), blade = new Vector3(0f, 0f, 1f), easeToNext = Ease.Out, power = 2f },
            // P3 overshoot: 30 cm lower, recovery begins
            new Key { time = 0.85f, hand = new Vector3(0.10f, 1.05f, 0.50f), blade = new Vector3(0f, -0.35f, 0.94f), easeToNext = Ease.InOut, power = 2f },
            // P0' back to idle guard
            new Key { time = 1.00f, hand = new Vector3(0.25f, 1.10f, 0.35f), blade = new Vector3(-0.2f, 0.7f, 0.7f), easeToNext = Ease.Linear, power = 1f },
        };

        public AttackTiming ToTiming()
        {
            var timing = new AttackTiming
            {
                WindupTicks = AttackTiming.MsToTicks(windupMs),
                ReleaseTicks = AttackTiming.MsToTicks(releaseMs),
                RecoveryTicks = AttackTiming.MsToTicks(recoveryMs),
                ReleaseStartPose = releaseStartPose,
                RecoveryStartPose = recoveryStartPose,
            };
            timing.Validate();
            return timing;
        }

        public NativeArray<WeaponPathKey> ToKeys(Allocator allocator)
        {
            if (keys == null || keys.Length < 2) throw new InvalidOperationException($"{name}: a weapon path needs at least two keys.");
            var result = new NativeArray<WeaponPathKey>(keys.Length, allocator);
            for (int i = 0; i < keys.Length; i++)
            {
                if (i > 0 && keys[i].time < keys[i - 1].time) throw new InvalidOperationException($"{name}: keys must be sorted by time.");
                result[i] = new WeaponPathKey
                {
                    Time = keys[i].time,
                    Hand = keys[i].hand,
                    Blade = keys[i].blade,
                    EaseToNext = keys[i].easeToNext,
                    Power = keys[i].power,
                };
            }
            return result;
        }
    }
}
