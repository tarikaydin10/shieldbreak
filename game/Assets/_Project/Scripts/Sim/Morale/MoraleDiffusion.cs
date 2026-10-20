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
            float a = diffusion * dt / (SimConstants.CellSize * SimConstants.CellSize);
            float k = decay * dt;
            int w = layout.Width, h = layout.Height;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int index = y * w + x;
                float p = panic[offset + index];
                float sum = 0f;
                int n = 0;
                if (x > 0) { sum += panic[offset + index - 1]; n++; }
                if (x < w - 1) { sum += panic[offset + index + 1]; n++; }
                if (y > 0) { sum += panic[offset + index - w]; n++; }
                if (y < h - 1) { sum += panic[offset + index + w]; n++; }
                float next = p + a * (sum - n * p) - k * p;
                scratch[index] = math.abs(next) < Floor ? 0f : next;
            }
            for (int index = 0; index < w * h; index++) panic[offset + index] = scratch[index];
        }
    }
}
