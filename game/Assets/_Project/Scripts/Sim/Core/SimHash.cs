using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Shieldbreak.Sim.Core
{
    /// <summary>
    /// FNV-1a 64 over raw bytes. Hashed structs must not contain padding, otherwise uninitialised padding
    /// bytes make equal states hash differently.
    /// </summary>
    public struct SimHash
    {
        const ulong OffsetBasis = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        public ulong Value;

        public static SimHash Create() => new SimHash { Value = OffsetBasis };

        public unsafe void AddBytes(void* data, int length)
        {
            byte* bytes = (byte*)data;
            ulong h = Value;
            for (int i = 0; i < length; i++)
            {
                h ^= bytes[i];
                h *= Prime;
            }
            Value = h;
        }

        public unsafe void Add<T>(T value) where T : unmanaged
        {
            AddBytes(&value, sizeof(T));
        }

        /// <summary>Hashes length and contents, so arrays of different length never collide by concatenation.</summary>
        public unsafe void Add<T>(NativeArray<T> array) where T : unmanaged
        {
            Add(array.Length);
            AddBytes(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array), array.Length * sizeof(T));
        }
    }
}
