using Unity.Mathematics;

namespace Shieldbreak.Sim.Core
{
    /// <summary>
    /// Seeded xorshift64* generator. The only source of randomness allowed in Sim/.
    /// Value type without allocation: hold it by ref (or as a field) so draws advance the shared state.
    /// </summary>
    public struct SimRandom
    {
        public ulong State;

        public SimRandom(ulong seed)
        {
            // SplitMix64 scrambles the seed so that 0 and neighbouring seeds yield unrelated, non-zero states
            ulong z = seed + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            State = z != 0 ? z : 0x9E3779B97F4A7C15UL;
        }

        public ulong NextULong()
        {
            State ^= State >> 12;
            State ^= State << 25;
            State ^= State >> 27;
            return State * 0x2545F4914F6CDD1DUL;
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        /// <summary>Uniform in [0, maxExclusive). maxExclusive must be positive.</summary>
        public int NextInt(int maxExclusive) => (int)(((ulong)NextUInt() * (uint)maxExclusive) >> 32);

        /// <summary>Uniform in [min, maxExclusive).</summary>
        public int NextInt(int min, int maxExclusive) => min + NextInt(maxExclusive - min);

        /// <summary>Uniform in [0, 1). Built from the top 24 bits, so every result is exactly representable.</summary>
        public float NextFloat() => (NextULong() >> 40) * (1f / 16777216f);

        public float NextFloat(float min, float max) => min + (max - min) * NextFloat();

        public float2 NextFloat2(float2 min, float2 max)
        {
            float x = NextFloat(min.x, max.x);
            float y = NextFloat(min.y, max.y);
            return new float2(x, y);
        }
    }
}
