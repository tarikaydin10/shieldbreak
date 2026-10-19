using NUnit.Framework;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Grid;
using Shieldbreak.Sim.Nav;
using Unity.Collections;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    public class NavTests
    {
        /// <summary>A grid with an A* searcher; disposes both.</summary>
        sealed class Map : System.IDisposable
        {
            public readonly WorldGrid Grid;
            public GridAStar AStar;
            public NativeArray<float2> Output;

            public Map(int width, int height)
            {
                Grid = new WorldGrid(new GridLayout(width, height, float2.zero), Allocator.Temp);
                AStar = new GridAStar(Grid.Layout, Allocator.Temp);
                Output = new NativeArray<float2>(64, Allocator.Temp);
            }

            public void Block(int x0, int y0, int x1, int y1) => Grid.SetCost(new int2(x0, y0), new int2(x1, y1), WorldGrid.Impassable);

            public float2[] Find(float2 start, float2 goal, out bool reached, int clearance = 0, int penalty = 0)
            {
                AStar.RefreshClearance(Grid.NavCost);
                var request = new PathRequest { Start = start, Goal = goal, RequiredClearance = clearance, ClearancePenalty = penalty };
                int n = AStar.FindPath(Grid.NavCost, request, Output, out _, out reached);
                var result = new float2[n];
                for (int i = 0; i < n; i++) result[i] = Output[i];
                return result;
            }

            /// <summary>Samples every segment densely and checks it never touches a blocked cell.</summary>
            public void AssertWalkable(float2 start, float2[] path)
            {
                float2 from = start;
                foreach (var to in path)
                {
                    int samples = (int)math.ceil(math.distance(from, to) * 20f) + 1;
                    for (int s = 0; s <= samples; s++)
                    {
                        float2 p = math.lerp(from, to, s / (float)samples);
                        Assert.That(Grid.IsPassable(Grid.Layout.WorldToCell(p)), $"segment {from} -> {to} crosses a blocked cell at {p}");
                    }
                    from = to;
                }
            }

            public void Dispose()
            {
                Output.Dispose();
                AStar.Dispose();
                Grid.Dispose();
            }
        }

        [Test]
        public void OpenField_IsOneStraightLeg()
        {
            using var map = new Map(40, 40);
            var path = map.Find(new float2(3.2f, 4.7f), new float2(35.5f, 30.1f), out bool reached);
            Assert.That(reached);
            Assert.That(path.Length, Is.EqualTo(1));
            Assert.That(path[0], Is.EqualTo(new float2(35.5f, 30.1f)));
        }

        [Test]
        public void Wall_IsPassedThroughTheGap()
        {
            using var map = new Map(40, 40);
            map.Block(20, 0, 21, 29);   // wall with a gap at y 30..39
            var start = new float2(5f, 5f);
            var path = map.Find(start, new float2(35f, 5f), out bool reached);
            Assert.That(reached);
            Assert.That(path.Length, Is.InRange(2, 6), "a few waypoints, not one per cell");
            map.AssertWalkable(start, path);
            bool throughGap = false;
            foreach (var p in path) throughGap |= p.y >= 29f;
            Assert.That(throughGap, "goes around the end of the wall");
        }

        [Test]
        public void NoCornerCutting_BetweenDiagonalBlocks()
        {
            using var map = new Map(10, 10);
            // Two blocks touching only at a corner: the free quarters meet in a single point, which is no passage
            map.Block(0, 0, 4, 4);
            map.Block(5, 5, 9, 9);
            var path = map.Find(new float2(2.5f, 7.5f), new float2(7.5f, 2.5f), out bool reached);
            Assert.That(reached, Is.False);
            map.AssertWalkable(new float2(2.5f, 7.5f), path);
            foreach (var p in path) Assert.That(p.x < 5f && p.y >= 5f, $"stays in its quarter, got {p}");
        }

        [Test]
        public void Enclosed_Goal_EndsAtTheClosestReachableCell()
        {
            using var map = new Map(30, 30);
            map.Block(18, 8, 26, 8);
            map.Block(18, 16, 26, 16);
            map.Block(18, 8, 18, 16);
            map.Block(26, 8, 26, 16);
            var start = new float2(3f, 12f);
            var path = map.Find(start, new float2(22.5f, 12.5f), out bool reached);
            Assert.That(reached, Is.False);
            Assert.That(path.Length, Is.GreaterThan(0));
            float2 end = path[path.Length - 1];
            Assert.That(math.distance(end, new float2(17.5f, 12.5f)), Is.LessThan(0.01f), "right in front of the box");
            map.AssertWalkable(start, path);
        }

        [Test]
        public void WideFormation_TakesTheWideGap_NarrowOneSqueezes()
        {
            using var map = new Map(60, 60);
            // Wall across x = 30 with a 2-cell gap near the direct line and a 14-cell gap further away
            map.Block(30, 0, 31, 21);
            map.Block(30, 24, 31, 39);
            map.Block(30, 54, 31, 59);
            var start = new float2(10f, 22.5f);
            var goal = new float2(50f, 22.5f);

            var narrow = map.Find(start, goal, out _, clearance: 0, penalty: 0);
            var wide = map.Find(start, goal, out _, clearance: 6, penalty: 4);
            float MaxY(float2[] p) { float m = 0f; foreach (var q in p) m = math.max(m, q.y); return m; }
            Assert.That(MaxY(narrow), Is.LessThan(30f), "without width the short way through the narrow gap");
            Assert.That(MaxY(wide), Is.GreaterThan(39f), "a wide front goes round through the wide gap");
            map.AssertWalkable(start, wide);
        }

        [Test]
        public void SamePlanTwice_SameWaypoints()
        {
            using var map = new Map(50, 50);
            var rng = new SimRandom(11);
            for (int k = 0; k < 40; k++)
            {
                int x = rng.NextInt(5, 45), y = rng.NextInt(5, 45);
                map.Block(x, y, x + rng.NextInt(0, 3), y + rng.NextInt(0, 3));
            }
            var a = map.Find(new float2(1.5f, 1.5f), new float2(48.5f, 48.5f), out _, 2, 4);
            var b = map.Find(new float2(1.5f, 1.5f), new float2(48.5f, 48.5f), out _, 2, 4);
            Assert.That(b, Is.EqualTo(a));
            map.AssertWalkable(new float2(1.5f, 1.5f), a);
        }

        [Test]
        public void Clearance_CountsCellsToTheNearestWallOrEdge()
        {
            using var map = new Map(20, 20);
            map.Block(10, 10, 10, 10);
            map.AStar.RefreshClearance(map.Grid.NavCost);
            Assert.That(map.AStar.Clearance(new int2(10, 10)), Is.EqualTo(0));
            Assert.That(map.AStar.Clearance(new int2(11, 11)), Is.EqualTo(1));
            Assert.That(map.AStar.Clearance(new int2(13, 10)), Is.EqualTo(3));
            Assert.That(map.AStar.Clearance(new int2(0, 5)), Is.EqualTo(1), "map edge counts as wall");
            Assert.That(map.AStar.Clearance(new int2(5, 5)), Is.EqualTo(5));
        }
    }
}
