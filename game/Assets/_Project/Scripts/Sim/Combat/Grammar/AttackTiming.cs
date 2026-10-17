using System;
using Unity.Mathematics;

namespace Shieldbreak.Sim.Combat.Grammar
{
    public enum AttackPhase : byte
    {
        Windup = 0,
        Release = 1,
        Recovery = 2,
        Done = 3,
    }

    /// <summary>
    /// Phase lengths of one attack in ticks plus where the phase boundaries sit on the normalized pose timeline
    /// (CLAUDE.md 3.5: Windup → Release → Recovery). Tick lengths come from the weapon (Schicht C) and may change with
    /// tuning; the pose timeline stays fixed, so the same poses play faster or slower.
    /// </summary>
    public struct AttackTiming
    {
        public int WindupTicks;
        public int ReleaseTicks;
        public int RecoveryTicks;
        /// <summary>Pose time where Release starts (end of Windup), e.g. 0.55.</summary>
        public float ReleaseStartPose;
        /// <summary>Pose time where Recovery starts (end of Release), e.g. 0.75.</summary>
        public float RecoveryStartPose;

        public int TotalTicks => WindupTicks + ReleaseTicks + RecoveryTicks;

        public void Validate()
        {
            if (WindupTicks <= 0 || ReleaseTicks <= 0 || RecoveryTicks <= 0) throw new ArgumentException("Every phase needs at least one tick.");
            if (!(ReleaseStartPose > 0f && ReleaseStartPose < RecoveryStartPose && RecoveryStartPose < 1f))
                throw new ArgumentException("Require 0 < ReleaseStartPose < RecoveryStartPose < 1.");
        }

        /// <summary>Phase at <paramref name="elapsedTicks"/> since the attack started (tick 0 = first windup tick).</summary>
        public AttackPhase PhaseAt(int elapsedTicks)
        {
            if (elapsedTicks < WindupTicks) return AttackPhase.Windup;
            if (elapsedTicks < WindupTicks + ReleaseTicks) return AttackPhase.Release;
            if (elapsedTicks < TotalTicks) return AttackPhase.Recovery;
            return AttackPhase.Done;
        }

        /// <summary>
        /// Normalized pose time in [0, 1] for a (possibly fractional) tick count since the start. Piecewise linear per
        /// phase, so each phase covers its own span of the pose timeline regardless of its tick length.
        /// </summary>
        public float PoseTime(float elapsedTicks)
        {
            float w = WindupTicks, r = ReleaseTicks, c = RecoveryTicks;
            if (elapsedTicks <= 0f) return 0f;
            if (elapsedTicks < w) return elapsedTicks / w * ReleaseStartPose;
            if (elapsedTicks < w + r) return ReleaseStartPose + (elapsedTicks - w) / r * (RecoveryStartPose - ReleaseStartPose);
            if (elapsedTicks < w + r + c) return RecoveryStartPose + (elapsedTicks - w - r) / c * (1f - RecoveryStartPose);
            return 1f;
        }

        /// <summary>Converts milliseconds to whole ticks (at least one).</summary>
        public static int MsToTicks(float ms) => math.max(1, (int)math.round(ms * 0.001f * Core.SimConstants.TickRate));
    }
}
