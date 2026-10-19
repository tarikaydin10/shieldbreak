using System;
using Unity.Collections;
using Unity.Mathematics;
using Shieldbreak.Sim.Formations;

namespace Shieldbreak.Sim.Orders
{
    /// <summary>
    /// Turns "put these formations on this front line" into FormationOrders: the line is split among the formations
    /// side by side, in their current left-to-right order so no paths cross. Shared by every client that gives orders
    /// on the map (debug view now, command view and commander AI later). Pure and deterministic.
    /// </summary>
    public static class FormationLinePlanner
    {
        /// <summary>Width of the front of a formation (m): the part a line order lays out.</summary>
        public static float FrontWidth(FormationType type, int files, int members, in FormationSettings settings)
        {
            var s = settings.Spacing(type);
            if (type == FormationType.Circle)
                return math.max(math.max(files, 3) * s.Lateral / math.PI, s.Depth * 2f);
            return (math.max(1, math.min(files, members)) - 1) * s.Lateral;
        }

        /// <summary>Files that make the front of a formation <paramref name="width"/> metres wide.</summary>
        public static int FilesForWidth(FormationType type, float width, int members, in FormationSettings settings)
        {
            var s = settings.Spacing(type);
            int files = type == FormationType.Circle
                ? (int)math.round(width * math.PI / s.Lateral)
                : (int)math.round(width / s.Lateral) + 1;
            return math.clamp(files, 1, math.max(1, members));
        }

        /// <summary>
        /// A drawn front line from <paramref name="a"/> to <paramref name="b"/>. With <paramref name="setWidth"/> every
        /// formation is stretched to its share of the line, otherwise it keeps its width. The group faces away from
        /// where it stands; a line drawn right through the group keeps the current front. One order per formation is
        /// written into <paramref name="orders"/>, same index as <paramref name="ids"/>.
        /// </summary>
        public static void PlanLine(NativeArray<FormationData>.ReadOnly formations, NativeArray<int> ids, int count,
            float2 a, float2 b, bool setWidth, float gap, in FormationSettings settings, NativeArray<FormationOrder> orders)
        {
            if (count <= 0) return;
            float2 mean = MeanAnchor(formations, ids, count, out float2 meanFacing);
            float length = math.length(b - a);
            float2 along = length > 1e-4f ? (b - a) / length : new float2(meanFacing.y, -meanFacing.x);
            float2 facing = new float2(-along.y, along.x);
            float side = math.dot((a + b) * 0.5f - mean, facing);
            if (side < -0.5f || (side <= 0.5f && math.dot(facing, meanFacing) < 0f)) facing = -facing;
            float available = setWidth ? math.max(length - gap * (count - 1), 0f) : -1f;
            Place(formations, ids, count, (a + b) * 0.5f, facing, available, gap, settings, orders);
        }

        /// <summary>
        /// A click instead of a drag: the group keeps its widths and lines up side by side, centred on
        /// <paramref name="target"/> and facing the way it walks.
        /// </summary>
        public static void PlanPoint(NativeArray<FormationData>.ReadOnly formations, NativeArray<int> ids, int count,
            float2 target, float gap, in FormationSettings settings, NativeArray<FormationOrder> orders)
        {
            if (count <= 0) return;
            float2 mean = MeanAnchor(formations, ids, count, out float2 meanFacing);
            float2 walk = target - mean;
            float2 facing = math.lengthsq(walk) > 1f ? math.normalize(walk) : meanFacing;
            Place(formations, ids, count, target, facing, -1f, gap, settings, orders);
        }

        /// <param name="available">Front width to share out (m), or negative to keep every formation's width.</param>
        static void Place(NativeArray<FormationData>.ReadOnly formations, NativeArray<int> ids, int count, float2 centre,
            float2 facing, float available, float gap, in FormationSettings settings, NativeArray<FormationOrder> orders)
        {
            if (count > ids.Length || count > orders.Length) throw new ArgumentOutOfRangeException(nameof(count));
            float2 along = new float2(facing.y, -facing.x);
            float totalNatural = 0f;
            for (int k = 0; k < count; k++)
            {
                var f = formations[ids[k]];
                totalNatural += FrontWidth(f.Type, f.Files, f.MemberCount, settings);
            }
            bool stretch = available >= 0f;
            float total = stretch ? available : totalNatural;

            for (int k = 0; k < count; k++)
            {
                var f = formations[ids[k]];
                float width = Share(f, totalNatural, total, count, settings);

                // Everything left of this formation along the line (current position, ties by id) comes first
                float key = math.dot(f.Position - centre, along);
                float offset = -(total + gap * (count - 1)) * 0.5f;
                for (int j = 0; j < count; j++)
                {
                    if (j == k) continue;
                    var g = formations[ids[j]];
                    float other = math.dot(g.Position - centre, along);
                    if (other < key || (other == key && ids[j] < ids[k]))
                        offset += Share(g, totalNatural, total, count, settings) + gap;
                }

                var order = FormationOrder.MoveTo(f.Id, centre + along * (offset + width * 0.5f), facing);
                int files = f.Files;
                if (stretch)
                {
                    files = FilesForWidth(f.Type, width, f.MemberCount, settings);
                    order.Flags |= FormationOrderFlags.Width;
                    order.Files = files;
                }
                // The circle's anchor is its centre: put the ring behind the line instead of on it
                if (f.Type == FormationType.Circle)
                    order.Destination -= facing * (FrontWidth(f.Type, files, f.MemberCount, settings) * 0.5f);
                orders[k] = order;
            }
        }

        static float Share(in FormationData f, float totalNatural, float total, int count, in FormationSettings settings) =>
            totalNatural > 1e-4f ? total * FrontWidth(f.Type, f.Files, f.MemberCount, settings) / totalNatural : total / count;

        static float2 MeanAnchor(NativeArray<FormationData>.ReadOnly formations, NativeArray<int> ids, int count, out float2 meanFacing)
        {
            float2 mean = float2.zero;
            meanFacing = float2.zero;
            for (int k = 0; k < count; k++)
            {
                mean += formations[ids[k]].Position;
                meanFacing += formations[ids[k]].Facing;
            }
            meanFacing = math.normalizesafe(meanFacing, formations[ids[0]].Facing);
            return mean / count;
        }
    }
}
