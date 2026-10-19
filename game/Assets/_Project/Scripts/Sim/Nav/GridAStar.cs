using System;
using Unity.Collections;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Grid;

namespace Shieldbreak.Sim.Nav
{
    /// <summary>What a path is for: how much room it wants and how much it pays for less.</summary>
    public struct PathRequest
    {
        public float2 Start;
        public float2 Goal;
        /// <summary>Wanted distance to the nearest obstacle (cells), e.g. half the width of a formation.</summary>
        public int RequiredClearance;
        /// <summary>Extra cost per entered cell and missing cell of clearance; 0 = squeeze through anything.</summary>
        public int ClearancePenalty;
    }

    /// <summary>
    /// A* on the WorldGrid for formation anchors (CLAUDE.md 3.4, 3.6): 8 neighbours without cutting corners, integer
    /// costs (straight 10, diagonal 14, times the cell cost), octile heuristic, ties broken by h and cell index, so
    /// every peer finds the same path. Cells close to obstacles cost extra for wide formations (clearance field).
    /// The cell path is pulled tight to few waypoints. Struct of NativeArrays so it runs inside a Burst job.
    /// </summary>
    public struct GridAStar : IDisposable
    {
        const int Straight = 10, Diagonal = 14;
        const byte Open = 1, Closed = 2;

        GridLayout layout;
        /// <summary>Chebyshev distance (cells) to the nearest blocked cell or the map edge; 0 = blocked.</summary>
        NativeArray<byte> clearance;
        NativeArray<int> g;
        NativeArray<int> parent;
        NativeArray<byte> state;
        NativeArray<int> heap;
        NativeArray<int> heapF;
        NativeArray<int> heapH;
        NativeArray<int> heapPos;
        NativeArray<int> cells;

        public GridAStar(GridLayout layout, Allocator allocator)
        {
            this.layout = layout;
            int n = layout.CellCount;
            clearance = new NativeArray<byte>(n, allocator);
            g = new NativeArray<int>(n, allocator);
            parent = new NativeArray<int>(n, allocator);
            state = new NativeArray<byte>(n, allocator);
            heap = new NativeArray<int>(n, allocator);
            heapF = new NativeArray<int>(n, allocator);
            heapH = new NativeArray<int>(n, allocator);
            heapPos = new NativeArray<int>(n, allocator);
            cells = new NativeArray<int>(n, allocator);
        }

        public GridLayout Layout => layout;

        public byte Clearance(int2 cell) => layout.InBounds(cell) ? clearance[layout.CellIndex(cell)] : (byte)0;

        /// <summary>Recomputes the clearance field (multi-source BFS from blocked cells and the map edge).</summary>
        public void RefreshClearance(NativeArray<byte> navCost)
        {
            int n = layout.CellCount;
            int head = 0, tail = 0;
            // Level 0: blocked cells; level 1: border cells (outside the map counts as blocked)
            for (int i = 0; i < n; i++)
            {
                if (navCost[i] == WorldGrid.Impassable)
                {
                    clearance[i] = 0;
                    cells[tail++] = i;
                }
                else
                {
                    clearance[i] = byte.MaxValue;
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (clearance[i] == 0) continue;
                int2 c = layout.IndexToCell(i);
                if (c.x == 0 || c.y == 0 || c.x == layout.Width - 1 || c.y == layout.Height - 1)
                {
                    clearance[i] = 1;
                    cells[tail++] = i;
                }
            }
            while (head < tail)
            {
                int i = cells[head++];
                int next = math.min(clearance[i] + 1, byte.MaxValue - 1);
                int2 c = layout.IndexToCell(i);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 nb = c + new int2(dx, dy);
                    if (!layout.InBounds(nb)) continue;
                    int j = layout.CellIndex(nb);
                    if (clearance[j] <= next) continue;
                    clearance[j] = (byte)next;
                    cells[tail++] = j;
                }
            }
        }

        /// <summary>
        /// Plans from request.Start to request.Goal and writes the waypoints (start excluded, goal included) into
        /// <paramref name="output"/>. If the goal cannot be reached the path ends at the reachable cell closest to it.
        /// Returns the number of waypoints written; <paramref name="truncated"/> tells that more did not fit.
        /// </summary>
        public int FindPath(NativeArray<byte> navCost, in PathRequest request, NativeArray<float2> output, out bool truncated, out bool reachedGoal)
        {
            truncated = false;
            reachedGoal = false;
            int start = layout.ClampedCellIndex(request.Start);
            int goal = layout.ClampedCellIndex(request.Goal);
            int n = layout.CellCount;
            for (int i = 0; i < n; i++) state[i] = 0;

            int heapCount = 0;
            g[start] = 0;
            parent[start] = -1;
            int2 goalCell = layout.IndexToCell(goal);
            int best = start, bestH = Heuristic(layout.IndexToCell(start), goalCell);
            Push(ref heapCount, start, bestH, bestH);
            state[start] = Open;

            while (heapCount > 0)
            {
                int current = Pop(ref heapCount);
                state[current] = Closed;
                int2 c = layout.IndexToCell(current);
                int h = Heuristic(c, goalCell);
                if (h < bestH || (h == bestH && g[current] < g[best]))
                {
                    best = current;
                    bestH = h;
                }
                if (current == goal)
                {
                    reachedGoal = true;
                    break;
                }

                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int2 nb = c + new int2(dx, dy);
                    if (!Walkable(navCost, nb)) continue;
                    bool diagonal = dx != 0 && dy != 0;
                    if (diagonal && (!Walkable(navCost, c + new int2(dx, 0)) || !Walkable(navCost, c + new int2(0, dy)))) continue;
                    int j = layout.CellIndex(nb);
                    if (state[j] == Closed) continue;
                    int cost = g[current] + StepCost(navCost, j, diagonal, request);
                    if (state[j] == Open && cost >= g[j]) continue;
                    g[j] = cost;
                    parent[j] = current;
                    int hj = Heuristic(nb, goalCell);
                    if (state[j] == Open) DecreaseKey(j, cost + hj, hj);
                    else
                    {
                        state[j] = Open;
                        Push(ref heapCount, j, cost + hj, hj);
                    }
                }
            }

            // Cell path goal -> start, reversed in place
            int end = reachedGoal ? goal : best;
            int length = 0;
            for (int i = end; i >= 0; i = parent[i]) cells[length++] = i;
            for (int a = 0, b = length - 1; a < b; a++, b--)
            {
                int t = cells[a];
                cells[a] = cells[b];
                cells[b] = t;
            }

            float2 last = reachedGoal && Walkable(navCost, goalCell) ? request.Goal : layout.CellCenter(layout.IndexToCell(end));
            return Simplify(navCost, request, length, last, output, out truncated);
        }

        /// <summary>Greedy string pulling: from each waypoint jump to the farthest path cell still in plain sight.</summary>
        int Simplify(NativeArray<byte> navCost, in PathRequest request, int length, float2 last, NativeArray<float2> output, out bool truncated)
        {
            truncated = false;
            int written = 0;
            float2 from = request.Start;
            int i = 0;
            while (i < length - 1)
            {
                int pick = i + 1;
                int allowed = math.max(Score(navCost, cells[i], request), Score(navCost, cells[i + 1], request));
                for (int k = i + 2; k < length; k++)
                {
                    allowed = math.max(allowed, Score(navCost, cells[k], request));
                    float2 to = k == length - 1 ? last : layout.CellCenter(layout.IndexToCell(cells[k]));
                    if (!LineClear(navCost, from, to, allowed, request)) break;
                    pick = k;
                }
                float2 point = pick == length - 1 ? last : layout.CellCenter(layout.IndexToCell(cells[pick]));
                if (written == output.Length)
                {
                    truncated = true;
                    return written;
                }
                output[written++] = point;
                from = point;
                i = pick;
            }
            if (length <= 1 && math.distancesq(request.Start, last) > 1e-8f && written < output.Length) output[written++] = last;
            return written;
        }

        /// <summary>Every cell the segment touches is walkable and no worse than <paramref name="allowed"/> (corners count both sides).</summary>
        bool LineClear(NativeArray<byte> navCost, float2 a, float2 b, int allowed, in PathRequest request)
        {
            int2 cell = layout.WorldToCell(a);
            int2 end = layout.WorldToCell(b);
            float2 d = b - a;
            int2 step = new int2(d.x > 0f ? 1 : d.x < 0f ? -1 : 0, d.y > 0f ? 1 : d.y < 0f ? -1 : 0);
            float2 local = (a - layout.Origin) * (1f / SimConstants.CellSize);
            float2 inv = new float2(d.x != 0f ? math.abs(SimConstants.CellSize / d.x) : float.MaxValue,
                                    d.y != 0f ? math.abs(SimConstants.CellSize / d.y) : float.MaxValue);
            float2 frac = local - math.floor(local);
            float2 tMax = new float2(
                step.x > 0 ? (1f - frac.x) * inv.x : step.x < 0 ? frac.x * inv.x : float.MaxValue,
                step.y > 0 ? (1f - frac.y) * inv.y : step.y < 0 ? frac.y * inv.y : float.MaxValue);
            int guard = math.abs(end.x - cell.x) + math.abs(end.y - cell.y) + 2;
            for (int it = 0; it <= guard; it++)
            {
                if (!Fits(navCost, cell, allowed, request)) return false;
                if (cell.x == end.x && cell.y == end.y) return true;
                if (tMax.x < tMax.y)
                {
                    cell.x += step.x;
                    tMax.x += inv.x;
                }
                else if (tMax.y < tMax.x)
                {
                    cell.y += step.y;
                    tMax.y += inv.y;
                }
                else
                {
                    // Exactly through a corner: both side cells must be free too
                    if (!Fits(navCost, cell + new int2(step.x, 0), allowed, request) || !Fits(navCost, cell + new int2(0, step.y), allowed, request))
                        return false;
                    cell += step;
                    tMax += inv;
                }
            }
            return false;
        }

        bool Fits(NativeArray<byte> navCost, int2 cell, int allowed, in PathRequest request) =>
            Walkable(navCost, cell) && Score(navCost, layout.CellIndex(cell), request) <= allowed;

        /// <summary>Per-cell badness used for straightening: cell cost plus clearance penalty.</summary>
        int Score(NativeArray<byte> navCost, int index, in PathRequest request) =>
            navCost[index] + request.ClearancePenalty * math.max(0, request.RequiredClearance - clearance[index]);

        bool Walkable(NativeArray<byte> navCost, int2 cell) =>
            layout.InBounds(cell) && navCost[layout.CellIndex(cell)] != WorldGrid.Impassable;

        int StepCost(NativeArray<byte> navCost, int index, bool diagonal, in PathRequest request) =>
            (diagonal ? Diagonal : Straight) * navCost[index] +
            Straight * request.ClearancePenalty * math.max(0, request.RequiredClearance - clearance[index]);

        static int Heuristic(int2 a, int2 b)
        {
            int2 d = math.abs(a - b);
            return Straight * math.max(d.x, d.y) + (Diagonal - Straight) * math.min(d.x, d.y);
        }

        // ---- Indexed binary min-heap on (f, h, cell index) ----

        bool Less(int a, int b) =>
            heapF[a] != heapF[b] ? heapF[a] < heapF[b] : heapH[a] != heapH[b] ? heapH[a] < heapH[b] : heap[a] < heap[b];

        void Push(ref int count, int node, int f, int h)
        {
            int i = count++;
            heap[i] = node;
            heapF[i] = f;
            heapH[i] = h;
            heapPos[node] = i;
            SiftUp(i);
        }

        int Pop(ref int count)
        {
            int top = heap[0];
            count--;
            if (count > 0)
            {
                Move(count, 0);
                SiftDown(0, count);
            }
            return top;
        }

        void DecreaseKey(int node, int f, int h)
        {
            int i = heapPos[node];
            heapF[i] = f;
            heapH[i] = h;
            SiftUp(i);
        }

        void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (!Less(i, p)) return;
                Swap(i, p);
                i = p;
            }
        }

        void SiftDown(int i, int count)
        {
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < count && Less(l, m)) m = l;
                if (r < count && Less(r, m)) m = r;
                if (m == i) return;
                Swap(i, m);
                i = m;
            }
        }

        void Move(int from, int to)
        {
            heap[to] = heap[from];
            heapF[to] = heapF[from];
            heapH[to] = heapH[from];
            heapPos[heap[to]] = to;
        }

        void Swap(int a, int b)
        {
            int node = heap[a], f = heapF[a], h = heapH[a];
            heap[a] = heap[b];
            heapF[a] = heapF[b];
            heapH[a] = heapH[b];
            heap[b] = node;
            heapF[b] = f;
            heapH[b] = h;
            heapPos[heap[a]] = a;
            heapPos[heap[b]] = b;
        }

        public void Dispose()
        {
            if (clearance.IsCreated) clearance.Dispose();
            if (g.IsCreated) g.Dispose();
            if (parent.IsCreated) parent.Dispose();
            if (state.IsCreated) state.Dispose();
            if (heap.IsCreated) heap.Dispose();
            if (heapF.IsCreated) heapF.Dispose();
            if (heapH.IsCreated) heapH.Dispose();
            if (heapPos.IsCreated) heapPos.Dispose();
            if (cells.IsCreated) cells.Dispose();
        }
    }
}
