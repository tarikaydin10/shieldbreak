using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Shieldbreak.Sim.Combat.Resolvers
{
    /// <summary>One arrow in flight (CLAUDE.md 3.5: Fixed-Step-Ballistik). Hashed raw: 40 bytes, no padding.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ProjectileData
    {
        /// <summary>World position: x and z on the map, y up (the same frame as the swing traces).</summary>
        public float3 Position;
        public float3 Velocity;
        /// <summary>Who shot it; stays valid when the shooter falls.</summary>
        public int Owner;
        public int Team;
        public int WeaponId;
        /// <summary>1 = in flight; a free slot is 0.</summary>
        public int Active;
    }

    /// <summary>Ballistics without trigonometry: everything here is exact in the sim's float rules.</summary>
    public static class Projectile
    {
        /// <summary>
        /// Velocity that carries an arrow from <paramref name="from"/> to <paramref name="to"/> at
        /// <paramref name="speed"/> across the ground, on the arc the fall needs.
        /// </summary>
        public static float3 Launch(float3 from, float3 to, float speed, float gravity)
        {
            float2 ground = to.xz - from.xz;
            float distance = math.length(ground);
            if (distance < 1e-3f) return new float3(0f, 0f, speed);
            float time = distance / speed;
            float2 across = ground / distance * speed;
            return new float3(across.x, 0.5f * gravity * time + (to.y - from.y) / time, across.y);
        }

        /// <summary>One fixed step: the fall changes the velocity, then the arrow moves.</summary>
        public static void Step(ref ProjectileData p, float gravity, float dt, out float3 from)
        {
            from = p.Position;
            p.Velocity.y -= gravity * dt;
            p.Position += p.Velocity * dt;
        }
    }
}
