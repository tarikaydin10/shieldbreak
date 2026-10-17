using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Grid;
using Unity.Collections;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    public class GridTests
    {
        static readonly GridLayout Layout = new GridLayout(40, 30, new float2(-20f, -15f));

        static NativeArray<float2> RandomPositions(ulong seed, int count, float margin = 2f)
        {
            var random = new SimRandom(seed);
            var positions = new NativeArray<float2>(count, Allocator.TempJob);
            float2 min = Layout.Origin - margin;
            float2 max = Layout.Origin + new float2(Layout.Width, Layout.Height) * SimConstants.CellSize + margin;
            for (int i = 0; i < count; i++) positions[i] = random.NextFloat2(min, max);
            return positions;
        }

        [Test]
        public void GridLayout_WorldToCell_FloorsRelativeToOrigin()
        {
            Assert.That(Layout.WorldToCell(new float2(-20f, -15f)), Is.EqualTo(new int2(0, 0)));
            Assert.That(Layout.WorldToCell(new float2(-19.01f, -14.5f)), Is.EqualTo(new int2(0, 0)));
            Assert.That(Layout.WorldToCell(new float2(-20.01f, -15f)), Is.EqualTo(new int2(-1, 0)));
            Assert.That(Layout.WorldToCell(new float2(0.5f, 0.5f)), Is.EqualTo(new int2(20, 15)));
            Assert.That(Layout.IndexToCell(Layout.CellIndex(new int2(7, 11))), Is.EqualTo(new int2(7, 11)));
            Assert.That(Layout.CellCenter(new int2(20, 15)), Is.EqualTo(new float2(0.5f, 0.5f)));
        }

        [Test]
        public void GridLayout_OutsidePositions_ClampToBorderCells()
        {
            Assert.That(Layout.ClampedCellIndex(new float2(-100f, -100f)), Is.EqualTo(Layout.CellIndex(new int2(0, 0))));
            Assert.That(Layout.ClampedCellIndex(new float2(100f, 100f)), Is.EqualTo(Layout.CellIndex(new int2(39, 29))));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridLayout(0, 10, float2.zero));
        }

        [Test]
        public void WorldGrid_StartsWithDefaultCost()
        {
            using var grid = new WorldGrid(Layout, Allocator.Temp);
            Assert.That(grid.NavCost.Length, Is.EqualTo(1200));
            foreach (byte cost in grid.NavCost) Assert.That(cost, Is.EqualTo(WorldGrid.DefaultCost));
        }

        [Test]
        public void SpatialHash_Build_BucketsEveryAgentOnceInAscendingIdOrder()
        {
            var positions = RandomPositions(3, 500);
            var hash = new SpatialHash(Layout, 600, Allocator.TempJob);
            try
            {
                hash.Build(positions, 500);
                Assert.That(hash.CellStart[0], Is.EqualTo(0));
                Assert.That(hash.CellStart[Layout.CellCount], Is.EqualTo(500));

                var seen = new bool[500];
                for (int c = 0; c < Layout.CellCount; c++)
                {
                    Assert.That(hash.CellStart[c + 1], Is.GreaterThanOrEqualTo(hash.CellStart[c]));
                    for (int e = hash.CellStart[c]; e < hash.CellStart[c + 1]; e++)
                    {
                        int agent = hash.Entries[e];
                        Assert.That(seen[agent], Is.False, $"agent {agent} bucketed twice");
                        seen[agent] = true;
                        Assert.That(hash.AgentCell[agent], Is.EqualTo(c));
                        Assert.That(Layout.ClampedCellIndex(positions[agent]), Is.EqualTo(c));
                        if (e > hash.CellStart[c]) Assert.That(agent, Is.GreaterThan(hash.Entries[e - 1]));
                    }
                }
                Assert.That(Array.TrueForAll(seen, s => s));
            }
            finally
            {
                hash.Dispose();
                positions.Dispose();
            }
        }

        [Test]
        public void SpatialHash_QueryRadius_MatchesBruteForce()
        {
            var positions = RandomPositions(11, 800, margin: 0f);
            var hash = new SpatialHash(Layout, 800, Allocator.TempJob);
            var results = new NativeList<int>(64, Allocator.Temp);
            try
            {
                hash.Build(positions, 800);
                var random = new SimRandom(99);
                foreach (float radius in new[] { 0.25f, 0.6f, 1f })
                {
                    for (int q = 0; q < 200; q++)
                    {
                        float2 center = random.NextFloat2(Layout.Origin, Layout.Origin + new float2(Layout.Width, Layout.Height));
                        results.Clear();
                        hash.QueryRadius(center, radius, positions, results);

                        var expected = new List<int>();
                        for (int a = 0; a < 800; a++)
                            if (math.distancesq(positions[a], center) <= radius * radius) expected.Add(a);
                        var actual = new List<int>();
                        foreach (int a in results) actual.Add(a);
                        actual.Sort();
                        Assert.That(actual, Is.EqualTo(expected), $"radius {radius}, center {center}");
                    }
                }
                Assert.Throws<ArgumentOutOfRangeException>(() => hash.QueryRadius(float2.zero, 1.5f, positions, results));
            }
            finally
            {
                results.Dispose();
                hash.Dispose();
                positions.Dispose();
            }
        }

        [Test]
        public void SpatialHash_RebuildWithSameInput_GivesIdenticalArraysAndQueryOrder()
        {
            var positions = RandomPositions(5, 300);
            var a = new SpatialHash(Layout, 300, Allocator.TempJob);
            var b = new SpatialHash(Layout, 300, Allocator.TempJob);
            var ra = new NativeList<int>(32, Allocator.Temp);
            var rb = new NativeList<int>(32, Allocator.Temp);
            try
            {
                a.Build(positions, 300);
                using (var other = RandomPositions(77, 300))
                    b.Build(other, 300);   // dirty b first, then rebuild with the same input
                b.Build(positions, 300);
                Assert.That(b.CellStart.ToArray(), Is.EqualTo(a.CellStart.ToArray()));
                Assert.That(b.Entries.ToArray(), Is.EqualTo(a.Entries.ToArray()));

                a.QueryRadius(new float2(1.3f, -2.7f), 1f, positions, ra);
                b.QueryRadius(new float2(1.3f, -2.7f), 1f, positions, rb);
                Assert.That(rb.AsArray().ToArray(), Is.EqualTo(ra.AsArray().ToArray()));
            }
            finally
            {
                ra.Dispose();
                rb.Dispose();
                a.Dispose();
                b.Dispose();
                positions.Dispose();
            }
        }

        [Test]
        public void SimWorld_HashIncludesNavCost()
        {
            var config = new SimConfig { Seed = 1, InputDelayTicks = 6, GridWidth = 16, GridHeight = 16 };
            using var a = new SimWorld(config);
            using var b = new SimWorld(config);
            Assert.That(b.ComputeHash(), Is.EqualTo(a.ComputeHash()));
            var cost = b.Grid.NavCost;
            cost[b.Grid.Layout.CellIndex(new int2(3, 4))] = WorldGrid.Impassable;
            Assert.That(b.ComputeHash(), Is.Not.EqualTo(a.ComputeHash()));
        }
    }
}
