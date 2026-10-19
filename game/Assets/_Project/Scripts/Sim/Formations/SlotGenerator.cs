using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Shieldbreak.Sim.Formations
{
    /// <summary>
    /// Slot layouts in the formation frame (x = right, y = forward). Linear types put the anchor at the centre of the
    /// front rank and ranks behind it (negative y); Circle puts it in the centre. Pure function of its arguments, so
    /// every peer generates identical slots.
    /// </summary>
    public static class SlotGenerator
    {
        public static void Generate(FormationType type, int count, int files, in FormationSettings settings,
            NativeArray<float2> local, NativeArray<float2> localFacing)
        {
            if (count < 0 || count > local.Length || count > localFacing.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (count == 0) return;
            files = math.clamp(files, 1, FormationLimits.MaxFiles);
            var spacing = settings.Spacing(type);
            switch (type)
            {
                case FormationType.Wedge:
                    Wedge(count, files, spacing, local);
                    break;
                case FormationType.Circle:
                    Circle(count, files, spacing, local, localFacing);
                    return;
                default:
                    Ranks(count, files, spacing, type == FormationType.Loose, local);
                    break;
            }
            for (int i = 0; i < count; i++) localFacing[i] = new float2(0f, 1f);
        }

        /// <summary>Full ranks of <paramref name="files"/>, the last rank centred; Loose shifts every second rank by half a slot.</summary>
        static void Ranks(int count, int files, FormationSpacing s, bool stagger, NativeArray<float2> local)
        {
            files = math.min(files, count);
            for (int i = 0; i < count; i++)
            {
                int rank = i / files;
                int inRank = math.min(files, count - rank * files);
                float x = (i % files - (inRank - 1) * 0.5f) * s.Lateral;
                if (stagger && (rank & 1) == 1 && inRank == files) x += s.Lateral * 0.5f;
                local[i] = new float2(x, -rank * s.Depth);
            }
        }

        /// <summary>Tip at the anchor, every rank two wider than the one before, up to <paramref name="files"/>.</summary>
        static void Wedge(int count, int files, FormationSpacing s, NativeArray<float2> local)
        {
            int i = 0;
            for (int rank = 0; i < count; rank++)
            {
                int width = math.min(1 + 2 * rank, math.max(files, 1));
                int inRank = math.min(width, count - i);
                for (int k = 0; k < inRank; k++, i++)
                    local[i] = new float2((k - (inRank - 1) * 0.5f) * s.Lateral, -rank * s.Depth);
            }
        }

        /// <summary>
        /// Concentric rings around the anchor, facing outwards, outer ring first. The outer ring holds about
        /// <paramref name="files"/> soldiers; it grows when the inner rings cannot take the rest.
        /// </summary>
        static void Circle(int count, int files, FormationSpacing s, NativeArray<float2> local, NativeArray<float2> localFacing)
        {
            float outer = math.max(math.max(files, 3) * s.Lateral / (2f * math.PI), s.Depth);
            while (RingCapacity(outer, s) < count) outer += s.Depth;

            int i = 0;
            for (int ring = 0; i < count; ring++)
            {
                float r = outer - ring * s.Depth;
                int capacity = RingSize(r, s);
                int n = math.min(capacity, count - i);
                if (capacity == 1)
                {
                    local[i] = float2.zero;
                    localFacing[i++] = new float2(0f, 1f);
                    continue;
                }
                // Alternate rings are rotated half a slot so the soldiers behind look through the gaps
                float offset = (ring & 1) == 1 ? math.PI / n : 0f;
                for (int k = 0; k < n; k++, i++)
                {
                    math.sincos(offset + 2f * math.PI * k / n, out float sin, out float cos);
                    var dir = new float2(sin, cos);
                    local[i] = dir * r;
                    localFacing[i] = dir;
                }
            }
        }

        static int RingSize(float radius, FormationSpacing s) =>
            radius < s.Depth * 0.5f ? 1 : math.max(1, (int)math.floor(2f * math.PI * radius / s.Lateral));

        static int RingCapacity(float outer, FormationSpacing s)
        {
            int total = 0;
            for (float r = outer; ; r -= s.Depth)
            {
                int n = RingSize(r, s);
                total += n;
                if (n == 1) return total;
            }
        }
    }
}
