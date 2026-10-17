using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;

namespace Shieldbreak.Sim.Grid
{
    /// <summary>
    /// Agent ids bucketed per grid cell, rebuilt every tick by counting sort. Within a cell, ids are ascending
    /// (the sort is stable over agent index), so every query visits agents in the same order on every peer.
    /// No hash maps: CellStart[c]..CellStart[c+1] is the slice of Entries for cell c.
    /// </summary>
    public struct SpatialHash : IDisposable
    {
        public GridLayout Layout;
        /// <summary>Length CellCount + 1; prefix sums of per-cell counts.</summary>
        public NativeArray<int> CellStart;
        /// <summary>Agent indices sorted by cell, then by index.</summary>
        public NativeArray<int> Entries;
        /// <summary>Cell index of each agent from the last build.</summary>
        public NativeArray<int> AgentCell;
        public int Count;

        NativeArray<int> cursor;

        public SpatialHash(GridLayout layout, int capacity, Allocator allocator)
        {
            Layout = layout;
            CellStart = new NativeArray<int>(layout.CellCount + 1, allocator);
            cursor = new NativeArray<int>(layout.CellCount, allocator);
            Entries = new NativeArray<int>(capacity, allocator);
            AgentCell = new NativeArray<int>(capacity, allocator);
            Count = 0;
        }

        public int Capacity => Entries.Length;

        /// <summary>Rebuilds the buckets from the first <paramref name="count"/> positions (Burst, single thread).</summary>
        public void Build(NativeArray<float2> positions, int count)
        {
            if (count > Capacity || count > positions.Length) throw new ArgumentOutOfRangeException(nameof(count));
            Count = count;
            new BuildJob
            {
                Layout = Layout,
                Positions = positions,
                Count = count,
                CellStart = CellStart,
                Cursor = cursor,
                Entries = Entries,
                AgentCell = AgentCell,
            }.Run();
        }

        /// <summary>
        /// Appends every agent within <paramref name="radius"/> of <paramref name="center"/> (inclusive) to
        /// <paramref name="results"/>, scanning the 3x3 cells around the center row by row. Radius must not exceed
        /// the cell size, otherwise agents two cells away would be missed.
        /// </summary>
        public void QueryRadius(float2 center, float radius, NativeArray<float2> positions, NativeList<int> results)
        {
            if (radius > SimConstants.CellSize) throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be <= CellSize for a 3x3 query.");
            float radiusSq = radius * radius;
            int2 c = math.clamp(Layout.WorldToCell(center), int2.zero, new int2(Layout.Width - 1, Layout.Height - 1));
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int2 cell = c + new int2(dx, dy);
                if (!Layout.InBounds(cell)) continue;
                int index = Layout.CellIndex(cell);
                for (int e = CellStart[index]; e < CellStart[index + 1]; e++)
                {
                    int agent = Entries[e];
                    if (math.distancesq(positions[agent], center) <= radiusSq) results.Add(agent);
                }
            }
        }

        public void Dispose()
        {
            if (CellStart.IsCreated) CellStart.Dispose();
            if (cursor.IsCreated) cursor.Dispose();
            if (Entries.IsCreated) Entries.Dispose();
            if (AgentCell.IsCreated) AgentCell.Dispose();
        }

        [BurstCompile]
        struct BuildJob : IJob
        {
            public GridLayout Layout;
            [ReadOnly] public NativeArray<float2> Positions;
            public int Count;
            public NativeArray<int> CellStart;
            public NativeArray<int> Cursor;
            public NativeArray<int> Entries;
            public NativeArray<int> AgentCell;

            public void Execute()
            {
                for (int i = 0; i < CellStart.Length; i++) CellStart[i] = 0;

                for (int a = 0; a < Count; a++)
                {
                    int cell = Layout.ClampedCellIndex(Positions[a]);
                    AgentCell[a] = cell;
                    CellStart[cell + 1]++;
                }

                for (int c = 0; c < Cursor.Length; c++)
                {
                    CellStart[c + 1] += CellStart[c];
                    Cursor[c] = CellStart[c];
                }

                // Ascending agent order keeps each bucket sorted by id
                for (int a = 0; a < Count; a++)
                    Entries[Cursor[AgentCell[a]]++] = a;
            }
        }
    }
}
