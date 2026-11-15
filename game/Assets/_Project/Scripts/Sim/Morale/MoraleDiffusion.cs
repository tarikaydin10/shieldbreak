using Unity.Collections;
using Unity.Mathematics;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Grid;

namespace Shieldbreak.Sim.Morale
{
    /// <summary>The diffusion step of the panic field (CLAUDE.md 3.4), called from the morale job.</summary>
    public static class MoraleDiffusion
    {
        /// <summary>Panic closer to 0 than this is set to exactly 0, so no denormals reach the field.</summary>
        public const float Floor = 1e-6f;

        /// <summary>
        /// One explicit step of P += dt·(D·ΔP − k·P) with a 4-neighbour Laplacian over the cells
        /// [offset, offset + CellCount) of <paramref name="panic"/>. The map edge lets no panic out (missing neighbours
        /// do not count), so without decay the total stays the same. Walls do not stop it. Stable while
        /// 4·D·dt + k·dt &lt;= 1 (MoraleSettings.Validate).
        /// </summary>
        public static void Step(NativeArray<float> panic, int offset, NativeArray<float> scratch, in GridLayout layout, float diffusion, float decay, float dt)
        {
            var all = Full(layout);
            Step(panic, offset, scratch, layout, diffusion, decay, dt, ref all);
        }

        /// <summary>No cell: min above max.</summary>
        public static int4 Empty => new int4(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);

        /// <summary>Every cell of the map (min x, min y, max x, max y).</summary>
        public static int4 Full(in GridLayout layout) => new int4(0, 0, layout.Width - 1, layout.Height - 1);

        /// <summary>Grows a box by one cell.</summary>
        public static int4 Include(int4 box, int2 cell) => new int4(math.min(box.xy, cell), math.max(box.zw, cell));

        /// <summary>
        /// The same step, only where something can change: <paramref name="active"/> holds every cell that is not 0 (it may
        /// hold more); the step covers it plus one cell around and leaves the tight box of the cells that are not 0 in it.
        /// Every other cell is 0 and has only zero neighbours, so it stays 0: the field is bit for bit the one of the full
        /// step, at the cost of the region where panic is.
        /// </summary>
        public static void Step(NativeArray<float> panic, int offset, NativeArray<float> scratch, in GridLayout layout, float diffusion, float decay, float dt,
            ref int4 active)
        {
            if (active.x > active.z || active.y > active.w) return;
            float a = diffusion * dt / (SimConstants.CellSize * SimConstants.CellSize);
            float k = decay * dt;
            int w = layout.Width, h = layout.Height;
            int x0 = math.max(active.x - 1, 0), y0 = math.max(active.y - 1, 0);
            int x1 = math.min(active.z + 1, w - 1), y1 = math.min(active.w + 1, h - 1);
            var next = Empty;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int index = y * w + x;
                float p = panic[offset + index];
                float sum = 0f;
                int n = 0;
                if (x > 0) { sum += panic[offset + index - 1]; n++; }
                if (x < w - 1) { sum += panic[offset + index + 1]; n++; }
                if (y > 0) { sum += panic[offset + index - w]; n++; }
                if (y < h - 1) { sum += panic[offset + index + w]; n++; }
                float value = p + a * (sum - n * p) - k * p;
                value = math.abs(value) < Floor ? 0f : value;
                scratch[index] = value;
                if (value != 0f) next = Include(next, new int2(x, y));
            }
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int index = y * w + x;
                panic[offset + index] = scratch[index];
            }
            active = next;
        }
    }
}
