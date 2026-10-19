using System;
using Unity.Collections;
using Unity.Mathematics;

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

        /// <summary>Bumped on every cost change, so derived data (clearance, planned paths) knows when to refresh.</summary>
        public int Version { get; private set; }

        public bool IsPassable(int2 cell) => Layout.InBounds(cell) && NavCost[Layout.CellIndex(cell)] != Impassable;

        /// <summary>
        /// Sets the cost of every cell in the inclusive rectangle (clamped to the map). Scenario setup, or later a
        /// deterministic sim event (destruction); never from presentation.
        /// </summary>
        public void SetCost(int2 min, int2 max, byte cost)
        {
            if (cost == 0) throw new ArgumentOutOfRangeException(nameof(cost), "Cost 0 is not allowed; 1 is the cheapest cell.");
            int2 lo = math.max(math.min(min, max), int2.zero);
            int2 hi = math.min(math.max(min, max), new int2(Layout.Width - 1, Layout.Height - 1));
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
                NavCost[Layout.CellIndex(new int2(x, y))] = cost;
            Version++;
        }

        public void Dispose()
        {
            if (NavCost.IsCreated) NavCost.Dispose();
        }
    }
}
