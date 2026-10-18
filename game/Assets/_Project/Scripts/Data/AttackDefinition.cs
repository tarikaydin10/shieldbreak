using System;
using Shieldbreak.Sim.Combat.Archetypes;
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
        [Tooltip("Targets one swing can hit: overhead and thrust 1, side cuts several in a row.")]
        [Range(1, 4)] public int maxTargets = 1;

        [Header("Lunge (root motion only for attack lunges)")]
        [Tooltip("Forward distance the attacker travels during the attack (m); its speed adds to the hit energy.")]
        [Min(0f)] public float lungeDistance;
        [Range(0f, 1f)] public float lungeStartPose = 0.35f;
        [Range(0f, 1f)] public float lungeEndPose = 0.72f;

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

        static Key K(float t, Vector3 hand, Vector3 blade, Ease ease, float power) =>
            new Key { time = t, hand = hand, blade = blade, easeToNext = ease, power = power };

        static readonly Vector3 GuardHand = new Vector3(0.25f, 1.10f, 0.35f);
        static readonly Vector3 GuardBlade = new Vector3(-0.2f, 0.7f, 0.7f);

        /// <summary>
        /// Cuts follow through: the contact pose lies slightly past the body centre, so the target is crossed in the
        /// middle of the release where the blade is fastest.
        /// Starting values for each direction (one-hand weapon, preset "Mittel"). Silhouettes follow the game feel
        /// spec: overhead = weapon above the head, side = wide backswing, thrust = pulled back at the hip.
        /// </summary>
        public void ApplyDefaults(int direction)
        {
            releaseStartPose = 0.55f;
            recoveryStartPose = 0.75f;
            switch (direction)
            {
                case AttackDirection.Left:
                    windupMs = 380f; releaseMs = 150f; recoveryMs = 220f; maxTargets = 3;
                    keys = new[]
                    {
                        K(0.00f, GuardHand, GuardBlade, Ease.InOut, 2.5f),
                        K(0.55f, new Vector3(-0.30f, 1.45f, 0.00f), new Vector3(-1f, 0.30f, 0f), Ease.InOut, 2f),
                        K(0.70f, new Vector3(-0.10f, 1.35f, 0.45f), new Vector3(0.423f, 0.05f, 0.906f), Ease.Out, 2f),
                        K(0.85f, new Vector3(0.20f, 1.30f, 0.30f), new Vector3(0.906f, 0f, 0.423f), Ease.InOut, 2f),
                        K(1.00f, GuardHand, GuardBlade, Ease.Linear, 1f),
                    };
                    break;
                case AttackDirection.Right:
                    windupMs = 380f; releaseMs = 150f; recoveryMs = 220f; maxTargets = 3;
                    keys = new[]
                    {
                        K(0.00f, GuardHand, GuardBlade, Ease.InOut, 2.5f),
                        K(0.55f, new Vector3(0.30f, 1.45f, 0.00f), new Vector3(1f, 0.30f, 0f), Ease.InOut, 2f),
                        K(0.70f, new Vector3(0.10f, 1.35f, 0.45f), new Vector3(-0.423f, 0.05f, 0.906f), Ease.Out, 2f),
                        K(0.85f, new Vector3(-0.20f, 1.30f, 0.30f), new Vector3(-0.906f, 0f, 0.423f), Ease.InOut, 2f),
                        K(1.00f, GuardHand, GuardBlade, Ease.Linear, 1f),
                    };
                    break;
                case AttackDirection.Thrust:
                    windupMs = 350f; releaseMs = 120f; recoveryMs = 220f; maxTargets = 1;
                    keys = new[]
                    {
                        K(0.00f, GuardHand, GuardBlade, Ease.InOut, 2.5f),
                        K(0.55f, new Vector3(0.30f, 1.15f, -0.15f), new Vector3(0f, 0.05f, 1f), Ease.InOut, 2f),
                        K(0.70f, new Vector3(0.15f, 1.35f, 0.60f), new Vector3(0f, 0.02f, 1f), Ease.Out, 2f),
                        K(0.85f, new Vector3(0.15f, 1.33f, 0.65f), new Vector3(0f, -0.05f, 1f), Ease.InOut, 2f),
                        K(1.00f, GuardHand, GuardBlade, Ease.Linear, 1f),
                    };
                    break;
                default:
                    windupMs = 400f; releaseMs = 150f; recoveryMs = 200f; maxTargets = 1;
                    keys = new[]
                    {
                        K(0.00f, GuardHand, GuardBlade, Ease.InOut, 2.5f),
                        K(0.55f, new Vector3(0.15f, 1.95f, -0.05f), new Vector3(0f, 0.95f, -0.3f), Ease.InOut, 2f),
                        K(0.70f, new Vector3(0.10f, 1.35f, 0.55f), new Vector3(0f, 0f, 1f), Ease.Out, 2f),
                        K(0.85f, new Vector3(0.10f, 1.05f, 0.50f), new Vector3(0f, -0.35f, 0.94f), Ease.InOut, 2f),
                        K(1.00f, GuardHand, GuardBlade, Ease.Linear, 1f),
                    };
                    break;
            }
        }

        static readonly Vector3 TwoHandGuardHand = new Vector3(0.12f, 1.05f, 0.35f);
        static readonly Vector3 TwoHandGuardBlade = new Vector3(-0.1f, 0.8f, 0.6f);

        /// <summary>
        /// Two-hand weapon: the one-hand paths with the grip pulled to the body centre (both hands on the hilt), a
        /// longer blade, slower phases and a long recovery. Overhead and thrust pull the hands 15 cm back so the longer
        /// blade still crosses the target at full speed; the wide side cuts get a slower release. Values from a sweep
        /// against a standing target at 1.25 m (DECISIONS.md), preset "Mittel".
        /// </summary>
        public void ApplyTwoHandDefaults(int direction)
        {
            ApplyDefaults(direction);
            bool side = direction == AttackDirection.Left || direction == AttackDirection.Right;
            windupMs *= 1.15f;
            releaseMs *= side ? 1.3f : 1.15f;
            recoveryMs *= 1.5f;
            bladeLength = 1.05f;
            foreach (var k in keys) k.hand.x *= 0.5f;
            if (!side)
                for (int i = 1; i < keys.Length - 1; i++) keys[i].hand.z -= 0.15f;
            keys[0].hand = keys[keys.Length - 1].hand = TwoHandGuardHand;
            keys[0].blade = keys[keys.Length - 1].blade = TwoHandGuardBlade;
            lungeDistance = 0f;
        }

        /// <summary>
        /// Two-hand special: leaping overhead. Weapon far back over the head, then a lunge from late windup into the
        /// release; the body's speed adds to the blade (energy bonus from geometry, not a flag).
        /// </summary>
        public void ApplyLeapDefaults()
        {
            ApplyTwoHandDefaults(AttackDirection.Overhead);
            windupMs = 520f; releaseMs = 170f; recoveryMs = 650f; maxTargets = 1;
            keys[1].hand = new Vector3(0.05f, 2.05f, -0.35f);
            keys[1].blade = new Vector3(0f, 0.85f, -0.53f);
            // Most of the lunge happens in the windup; the part in the release adds the energy bonus
            lungeDistance = 1.4f;
            lungeStartPose = 0.25f;
            lungeEndPose = 0.62f;
        }

        public AttackData ToAttackData(int firstKey) => new AttackData
        {
            Timing = ToTiming(),
            FirstKey = firstKey,
            KeyCount = keys.Length,
            BladeLength = bladeLength,
            SocketCount = socketCount,
            MaxTargets = maxTargets,
            LungeDistance = lungeDistance,
            LungeStartPose = lungeStartPose,
            LungeEndPose = lungeEndPose,
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
