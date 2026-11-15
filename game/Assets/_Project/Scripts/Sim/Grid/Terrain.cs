using Unity.Collections;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;

namespace Shieldbreak.Sim.Grid
{
    /// <summary>
    /// The height of the ground (CLAUDE.md 3.4: everything on the one grid). Heights lie on the corners of the cells,
    /// (Width + 1) * (Height + 1) of them, and are bilinear in between; outside the map the border height goes on.
    /// Pure functions on the array, so Burst jobs and the presentation sample the same ground.
    /// </summary>
    public static class Terrain
    {
        public static int CornerCount(in GridLayout layout) => (layout.Width + 1) * (layout.Height + 1);

        public static int CornerIndex(in GridLayout layout, int x, int y) => y * (layout.Width + 1) + x;

        /// <summary>Cell the position lies in and where inside it (0 to 1 per axis).</summary>
        static void Locate(in GridLayout layout, float2 position, out int2 cell, out float2 inside)
        {
            float2 corners = math.clamp((position - layout.Origin) / SimConstants.CellSize, float2.zero, new float2(layout.Width, layout.Height));
            cell = math.min((int2)corners, new int2(layout.Width - 1, layout.Height - 1));
            inside = corners - cell;
        }

        public static float HeightAt(NativeArray<float> heights, in GridLayout layout, float2 position)
        {
            Locate(layout, position, out int2 c, out float2 t);
            int low = CornerIndex(layout, c.x, c.y), high = low + layout.Width + 1;
            return math.lerp(math.lerp(heights[low], heights[low + 1], t.x), math.lerp(heights[high], heights[high + 1], t.x), t.y);
        }

        /// <summary>Rise per metre along x and along the sim's y at this position.</summary>
        public static float2 SlopeAt(NativeArray<float> heights, in GridLayout layout, float2 position)
        {
            Locate(layout, position, out int2 c, out float2 t);
            int low = CornerIndex(layout, c.x, c.y), high = low + layout.Width + 1;
            float alongX = math.lerp(heights[low + 1] - heights[low], heights[high + 1] - heights[high], t.y);
            float alongY = math.lerp(heights[high] - heights[low], heights[high + 1] - heights[low + 1], t.x);
            return new float2(alongX, alongY) / SimConstants.CellSize;
        }

        /// <summary>Raises a round hill: flat at its foot and on top of its slope, no trigonometry. Scenario setup.</summary>
        public static void AddHill(NativeArray<float> heights, in GridLayout layout, float2 centre, float radius, float height)
        {
            for (int y = 0; y <= layout.Height; y++)
            for (int x = 0; x <= layout.Width; x++)
            {
                float2 corner = layout.Origin + new float2(x, y) * SimConstants.CellSize;
                float t = 1f - math.saturate(math.distance(corner, centre) / radius);
                heights[CornerIndex(layout, x, y)] += height * t * t * (3f - 2f * t);
            }
        }

        /// <summary>
        /// Rolling ground over the whole map: smooth value noise in two octaves, <paramref name="amplitude"/> metres up or
        /// down at most, waves about <paramref name="wavelength"/> metres long. Integer hash, no sine: the same on every
        /// peer. Scenario setup.
        /// </summary>
        public static void AddRolling(NativeArray<float> heights, in GridLayout layout, float amplitude, float wavelength, uint seed)
        {
            if (amplitude == 0f || wavelength <= 0f) return;
            for (int y = 0; y <= layout.Height; y++)
            for (int x = 0; x <= layout.Width; x++)
            {
                float2 corner = new float2(x, y) * SimConstants.CellSize / wavelength;
                float wave = ValueNoise(corner, seed) + 0.35f * ValueNoise(corner * 2.3f + 17.1f, seed + 1u);
                heights[CornerIndex(layout, x, y)] += amplitude * wave / 1.35f;
            }
        }

        /// <summary>Smooth noise from -1 to 1 with a lattice step of 1.</summary>
        public static float ValueNoise(float2 p, uint seed)
        {
            float2 cell = math.floor(p);
            float2 t = p - cell;
            t = t * t * (3f - 2f * t);
            int2 c = (int2)cell;
            float a = Lattice(c.x, c.y, seed), b = Lattice(c.x + 1, c.y, seed);
            float d = Lattice(c.x, c.y + 1, seed), e = Lattice(c.x + 1, c.y + 1, seed);
            return math.lerp(math.lerp(a, b, t.x), math.lerp(d, e, t.x), t.y);
        }

        static float Lattice(int x, int y, uint seed)
        {
            uint h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (seed + 1u) * 0xCB1AB31Fu;
            h ^= h >> 13;
            h *= 0x5BD1E995u;
            h ^= h >> 15;
            return (h & 0xFFFFFFu) / 8388608f - 1f;
        }


        /// <summary>Factor on the speed of someone walking along <paramref name="direction"/> (a unit vector) over this slope.</summary>
        public static float SpeedFactor(float2 slope, float2 direction, float uphillSlowdown, float downhillBoost, float least, float most)
        {
            if (uphillSlowdown <= 0f && downhillBoost <= 0f) return 1f;
            float grade = math.dot(slope, direction);
            float factor = grade > 0f ? 1f - uphillSlowdown * grade : 1f - downhillBoost * grade;
            return math.clamp(factor, least, most);
        }
    }
}
