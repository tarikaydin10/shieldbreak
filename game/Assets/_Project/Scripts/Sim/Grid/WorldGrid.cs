using System;
using Unity.Collections;

namespace Shieldbreak.Sim.Grid
{
    /// <summary>
    /// The one grid shared by navigation, spatial queries and (from M2) the morale field. Navigation cost lives
    /// per cell and may change at runtime, which is how destruction will plug in later (CLAUDE.md 3.4).
    /// </summary>
    public sealed class WorldGrid : IDisposable
    {
        public const byte DefaultCost = 1;
        public const byte Impassable = byte.MaxValue;

        public readonly GridLayout Layout;
        public NativeArray<byte> NavCost;

        public WorldGrid(GridLayout layout, Allocator allocator)
        {
            Layout = layout;
            NavCost = new NativeArray<byte>(layout.CellCount, allocator, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < NavCost.Length; i++) NavCost[i] = DefaultCost;
        }

        public void Dispose()
        {
            if (NavCost.IsCreated) NavCost.Dispose();
        }
    }
}
