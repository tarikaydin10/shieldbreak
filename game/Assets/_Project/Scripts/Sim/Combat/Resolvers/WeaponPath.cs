using Unity.Collections;
using Unity.Mathematics;

namespace Shieldbreak.Sim.Combat.Resolvers
{
    public enum Ease : byte
    {
        Linear = 0,
        /// <summary>Starts slow, ends fast (u^p).</summary>
        In = 1,
        /// <summary>Starts fast, ends slow.</summary>
        Out = 2,
        /// <summary>Slow at both ends, fastest in the middle.</summary>
        InOut = 3,
    }

    /// <summary>
    /// One key of a weapon path in the attacker's local frame (x right, y up, z forward, origin at the feet).
    /// The segment from this key to the next uses EaseToNext with exponent Power.
    /// </summary>
    public struct WeaponPathKey
    {
        /// <summary>Normalized pose time in [0, 1]; keys are sorted by time, first at 0, last at 1.</summary>
        public float Time;
        /// <summary>Grip position (m).</summary>
        public float3 Hand;
        /// <summary>Direction from grip to blade tip (normalized on evaluation).</summary>
        public float3 Blade;
        public Ease EaseToNext;
        public float Power;
    }

    /// <summary>
    /// Evaluates a weapon path from keys and easing curves. This is the sim's source for blade sockets (SwingTrace,
    /// M1): pure math, no Unity animator, identical on every peer (CLAUDE.md 3.5).
    /// </summary>
    public static class WeaponPath
    {
        public static float ApplyEase(Ease ease, float power, float u)
        {
            u = math.saturate(u);
            float p = math.max(power, 1f);
            switch (ease)
            {
                case Ease.In: return math.pow(u, p);
                case Ease.Out: return 1f - math.pow(1f - u, p);
                case Ease.InOut: return u < 0.5f ? 0.5f * math.pow(2f * u, p) : 1f - 0.5f * math.pow(2f - 2f * u, p);
                default: return u;
            }
        }

        public static void Evaluate(NativeArray<WeaponPathKey> keys, float poseTime, out float3 hand, out float3 blade)
        {
            float t = math.saturate(poseTime);
            int last = keys.Length - 1;
            int i = 0;
            while (i < last - 1 && t >= keys[i + 1].Time) i++;
            var a = keys[i];
            var b = keys[math.min(i + 1, last)];
            float span = b.Time - a.Time;
            float u = span > 1e-6f ? (t - a.Time) / span : 1f;
            float e = ApplyEase(a.EaseToNext, a.Power, u);
            hand = math.lerp(a.Hand, b.Hand, e);
            blade = Slerp(math.normalizesafe(a.Blade, new float3(0f, 1f, 0f)), math.normalizesafe(b.Blade, new float3(0f, 1f, 0f)), e);
        }

        /// <summary>
        /// Constant angular speed between two unit vectors. A normalized lerp would spike the blade's angular velocity
        /// in the middle of large swings (and with it the contact energy).
        /// </summary>
        public static float3 Slerp(float3 from, float3 to, float t)
        {
            float d = math.clamp(math.dot(from, to), -1f, 1f);
            if (d > 0.9995f) return math.normalizesafe(math.lerp(from, to, t), to);
            float angle = math.acos(d);
            float s = math.sin(angle);
            if (s < 1e-5f)
            {
                // Opposite vectors: rotate through any perpendicular axis
                float3 axis = math.normalizesafe(math.cross(from, new float3(0f, 1f, 0f)), new float3(1f, 0f, 0f));
                return math.normalize(math.rotate(quaternion.AxisAngle(axis, angle * t), from));
            }
            return (math.sin((1f - t) * angle) * from + math.sin(t * angle) * to) / s;
        }

        /// <summary>Point along the blade, <paramref name="along"/> metres from the grip.</summary>
        public static float3 Socket(float3 hand, float3 blade, float along) => hand + blade * along;
    }
}
