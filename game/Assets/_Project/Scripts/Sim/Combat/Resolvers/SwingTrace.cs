using Unity.Mathematics;
using Shieldbreak.Sim.Combat.Grammar;

namespace Shieldbreak.Sim.Combat.Resolvers
{
    /// <summary>
    /// Hit detection for swung weapons (CLAUDE.md 3.5): each tick every blade socket sweeps a segment from its
    /// previous to its current position; the sweep is tested against the body zone capsules of nearby agents.
    /// Pure math on Unity.Mathematics, deterministic, no PhysX.
    /// </summary>
    public static class SwingTrace
    {
        // Body zones in the agent's frame (y up), sized for the 1.8 m dummy: axis bottom/top height and radius
        public const float LegsBottom = 0.10f, LegsTop = 0.85f, LegsRadius = 0.15f;
        public const float TorsoBottom = 0.92f, TorsoTop = 1.42f, TorsoRadius = 0.21f;
        public const float HeadCentre = 1.63f, HeadRadius = 0.13f;
        /// <summary>Half thickness of the blade added to every capsule radius.</summary>
        public const float BladeRadius = 0.02f;

        /// <summary>World position (x, height, y) of a point given in the agent's frame (x right, y up, z forward).</summary>
        public static float3 ToWorld(float2 agentPos, float2 facing, float3 local)
        {
            // Agent frame: forward = facing, right = (facing.y, -facing.x) in the sim plane
            float2 right = new float2(facing.y, -facing.x);
            float2 planar = agentPos + right * local.x + facing * local.z;
            return new float3(planar.x, local.y, planar.y);
        }

        public static float3 DirectionToWorld(float2 facing, float3 local)
        {
            float2 right = new float2(facing.y, -facing.x);
            float2 planar = right * local.x + facing * local.z;
            return new float3(planar.x, local.y, planar.y);
        }

        /// <summary>Capsule axis of a body zone for an agent standing at <paramref name="pos"/>.</summary>
        public static void ZoneCapsule(int zone, float2 pos, out float3 a, out float3 b, out float radius)
        {
            switch (zone)
            {
                case BodyZone.Legs:
                    a = new float3(pos.x, LegsBottom, pos.y);
                    b = new float3(pos.x, LegsTop, pos.y);
                    radius = LegsRadius;
                    return;
                case BodyZone.Torso:
                    a = new float3(pos.x, TorsoBottom, pos.y);
                    b = new float3(pos.x, TorsoTop, pos.y);
                    radius = TorsoRadius;
                    return;
                default:
                    a = b = new float3(pos.x, HeadCentre, pos.y);
                    radius = HeadRadius;
                    return;
            }
        }

        /// <summary>
        /// Closest points between segments p1-q1 and p2-q2 (Ericson, Real-Time Collision Detection 5.1.9).
        /// Returns the squared distance; s and t are the parameters on the first and second segment.
        /// </summary>
        public static float ClosestSegmentSegment(float3 p1, float3 q1, float3 p2, float3 q2, out float s, out float t)
        {
            const float Epsilon = 1e-8f;
            float3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = math.dot(d1, d1), e = math.dot(d2, d2), f = math.dot(d2, r);

            if (a <= Epsilon && e <= Epsilon)
            {
                s = t = 0f;
                return math.dot(r, r);
            }
            if (a <= Epsilon)
            {
                s = 0f;
                t = math.saturate(f / e);
            }
            else
            {
                float c = math.dot(d1, r);
                if (e <= Epsilon)
                {
                    t = 0f;
                    s = math.saturate(-c / a);
                }
                else
                {
                    float b = math.dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > Epsilon ? math.saturate((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = math.saturate(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = math.saturate((b - c) / a);
                    }
                }
            }
            float3 diff = (p1 + d1 * s) - (p2 + d2 * t);
            return math.dot(diff, diff);
        }

        /// <summary>
        /// Earliest contact of a socket sweep with any body zone of an agent at <paramref name="pos"/>.
        /// Returns true with the sweep parameter (0 = previous position) and the zone.
        /// </summary>
        public static bool SweepAgent(float3 from, float3 to, float2 pos, out float sweepT, out int zone)
        {
            sweepT = 2f;
            zone = -1;
            for (int z = 0; z < BodyZone.Count; z++)
            {
                ZoneCapsule(z, pos, out float3 a, out float3 b, out float radius);
                float r = radius + BladeRadius;
                float d2 = ClosestSegmentSegment(from, to, a, b, out float s, out _);
                if (d2 <= r * r && s < sweepT)
                {
                    sweepT = s;
                    zone = z;
                }
            }
            return zone >= 0;
        }

        /// <summary>
        /// Penetration of a contact: cuts (motion across the blade) use the edge factor, thrusts (motion along it)
        /// the point factor; the better of the two counts (stab, cut and blunt emerge from geometry, not flags).
        /// </summary>
        public static float Penetration(float3 motion, float3 blade, float edgeFactor, float pointFactor)
        {
            float3 m = math.normalizesafe(motion);
            float along = math.abs(math.dot(m, math.normalizesafe(blade)));
            float across = math.sqrt(math.max(0f, 1f - along * along));
            return math.max(edgeFactor * across, pointFactor * along);
        }
    }
}
