using System;
using Unity.Collections;

namespace Shieldbreak.Sim.Formations
{
    /// <summary>
    /// Optimal assignment of n soldiers to n slots minimising the summed squared distance (Hungarian method with
    /// potentials, O(n³)). Exact and free of transcendental math, so the result is bit-identical on every peer; ties
    /// resolve by index order. Squared distance keeps paths short and the order stable: a formation that only moves
    /// keeps every soldier in the same slot. See DECISIONS.md (2026-10-19) for why this replaces Sinkhorn.
    /// </summary>
    public struct SlotAssignment : IDisposable
    {
        NativeArray<double> u, v, minv;
        NativeArray<int> p, way;
        NativeArray<byte> used;

        public SlotAssignment(int maxSize, Allocator allocator)
        {
            u = new NativeArray<double>(maxSize + 1, allocator);
            v = new NativeArray<double>(maxSize + 1, allocator);
            minv = new NativeArray<double>(maxSize + 1, allocator);
            p = new NativeArray<int>(maxSize + 1, allocator);
            way = new NativeArray<int>(maxSize + 1, allocator);
            used = new NativeArray<byte>(maxSize + 1, allocator);
        }

        public int MaxSize => p.Length - 1;

        /// <summary>
        /// <paramref name="cost"/> is row-major n×n (row = soldier, column = slot); writes the slot of every row into
        /// <paramref name="slotOfRow"/>.
        /// </summary>
        public void Solve(NativeArray<float> cost, int n, NativeArray<int> slotOfRow)
        {
            if (n < 0 || n > MaxSize || cost.Length < n * n || slotOfRow.Length < n) throw new ArgumentOutOfRangeException(nameof(n));
            for (int j = 0; j <= n; j++)
            {
                u[j] = 0.0;
                v[j] = 0.0;
                p[j] = 0;
                way[j] = 0;
            }

            // 1-based indices, column 0 is the virtual start of each augmenting path
            for (int i = 1; i <= n; i++)
            {
                p[0] = i;
                int j0 = 0;
                for (int j = 0; j <= n; j++)
                {
                    minv[j] = double.MaxValue;
                    used[j] = 0;
                }
                do
                {
                    used[j0] = 1;
                    int i0 = p[j0];
                    double delta = double.MaxValue;
                    int j1 = 0;
                    for (int j = 1; j <= n; j++)
                    {
                        if (used[j] != 0) continue;
                        double cur = cost[(i0 - 1) * n + (j - 1)] - u[i0] - v[j];
                        if (cur < minv[j])
                        {
                            minv[j] = cur;
                            way[j] = j0;
                        }
                        if (minv[j] < delta)
                        {
                            delta = minv[j];
                            j1 = j;
                        }
                    }
                    for (int j = 0; j <= n; j++)
                    {
                        if (used[j] != 0)
                        {
                            u[p[j]] += delta;
                            v[j] -= delta;
                        }
                        else
                        {
                            minv[j] -= delta;
                        }
                    }
                    j0 = j1;
                } while (p[j0] != 0);
                do
                {
                    int j1 = way[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                } while (j0 != 0);
            }

            for (int j = 1; j <= n; j++) slotOfRow[p[j] - 1] = j - 1;
        }

        public void Dispose()
        {
            if (u.IsCreated) u.Dispose();
            if (v.IsCreated) v.Dispose();
            if (minv.IsCreated) minv.Dispose();
            if (p.IsCreated) p.Dispose();
            if (way.IsCreated) way.Dispose();
            if (used.IsCreated) used.Dispose();
        }
    }
}
