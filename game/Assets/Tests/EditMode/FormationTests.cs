using System;
using NUnit.Framework;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Collections;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    public class FormationTests
    {
        static readonly FormationType[] AllTypes =
            { FormationType.Line, FormationType.ShieldWall, FormationType.Wedge, FormationType.Circle, FormationType.Loose };

        static SimWorld World(int maxAgents, int size = 96, ulong seed = 1)
        {
            var config = TestScenarios.Config(seed, size, maxAgents);
            config.MaxFormations = 8;
            config.Formations = TestScenarios.Formations;
            return new SimWorld(config);
        }

        static void Run(SimWorld world, int ticks)
        {
            for (int t = 0; t < ticks; t++) world.Step();
        }

        static float MaxSlotError(SimWorld world, int formation)
        {
            var f = world.Formations[formation];
            float max = 0f;
            for (int i = 0; i < world.AgentCount; i++)
            {
                var m = world.FormationMembers[i];
                if (m.Formation != formation) continue;
                max = math.max(max, math.distance(world.Agents[i].Position, f.ToWorld(m.Local)));
            }
            return max;
        }

        // ---- Slots ----

        [Test]
        public void Slots_AreDistinct_AndKeepTheTypeSpacing([ValueSource(nameof(AllTypes))] FormationType type)
        {
            var settings = TestScenarios.Formations;
            var spacing = settings.Spacing(type);
            float minGap = math.min(spacing.Lateral, spacing.Depth) * 0.99f;
            using var local = new NativeArray<float2>(120, Allocator.Temp);
            using var facing = new NativeArray<float2>(120, Allocator.Temp);
            foreach (int count in new[] { 1, 2, 7, 20, 41, 120 })
            foreach (int files in new[] { 1, 5, 12, 200 })
            {
                SlotGenerator.Generate(type, count, files, settings, local, facing);
                for (int i = 0; i < count; i++)
                {
                    Assert.That(math.all(math.isfinite(local[i])), $"{type} n={count} f={files} slot {i}");
                    Assert.That(math.length(facing[i]), Is.EqualTo(1f).Within(1e-4f));
                    for (int j = 0; j < i; j++)
                        Assert.That(math.distance(local[i], local[j]), Is.GreaterThanOrEqualTo(minGap), $"{type} n={count} f={files} slots {j},{i}");
                }
            }
        }

        [Test]
        public void Line_FrontRankIsCentredOnTheAnchor_RanksBehind()
        {
            var s = TestScenarios.Formations;
            using var local = new NativeArray<float2>(23, Allocator.Temp);
            using var facing = new NativeArray<float2>(23, Allocator.Temp);
            SlotGenerator.Generate(FormationType.Line, 23, 10, s, local, facing);
            float sum = 0f;
            for (int i = 0; i < 10; i++)
            {
                Assert.That(local[i].y, Is.EqualTo(0f));
                sum += local[i].x;
            }
            Assert.That(sum, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(local[10].y, Is.EqualTo(-s.Line.Depth).Within(1e-5f));
            // Last rank (3 soldiers) is centred too
            Assert.That(local[20].x + local[21].x + local[22].x, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Wedge_HasItsTipAtTheAnchor_AndWidensByTwo()
        {
            using var local = new NativeArray<float2>(9, Allocator.Temp);
            using var facing = new NativeArray<float2>(9, Allocator.Temp);
            SlotGenerator.Generate(FormationType.Wedge, 9, 9, TestScenarios.Formations, local, facing);
            Assert.That(local[0], Is.EqualTo(float2.zero));
            Assert.That(local[1].y, Is.EqualTo(local[3].y), "rank 1 holds three");
            Assert.That(local[4].y, Is.LessThan(local[3].y), "rank 2 starts at slot 4");
        }

        [Test]
        public void Circle_FacesOutwards_AroundTheAnchor()
        {
            using var local = new NativeArray<float2>(40, Allocator.Temp);
            using var facing = new NativeArray<float2>(40, Allocator.Temp);
            SlotGenerator.Generate(FormationType.Circle, 40, 24, TestScenarios.Formations, local, facing);
            float2 centroid = float2.zero;
            for (int i = 0; i < 40; i++)
            {
                centroid += local[i];
                if (math.lengthsq(local[i]) > 1e-6f)
                    Assert.That(math.dot(math.normalize(local[i]), facing[i]), Is.GreaterThan(0.999f), $"slot {i}");
            }
            Assert.That(math.length(centroid / 40f), Is.LessThan(0.3f));
        }

        // ---- Assignment ----

        [Test]
        public void Assignment_IsOptimal_AgainstBruteForce()
        {
            var rng = new SimRandom(5);
            using var solver = new SlotAssignment(7, Allocator.Temp);
            using var result = new NativeArray<int>(7, Allocator.Temp);
            var cost = new NativeArray<float>(49, Allocator.Temp);
            try
            {
            for (int trial = 0; trial < 200; trial++)
            {
                int n = 1 + trial % 7;
                for (int k = 0; k < n * n; k++) cost[k] = rng.NextFloat(0f, 50f);
                solver.Solve(cost, n, result);

                float got = 0f;
                var seen = new bool[n];
                for (int r = 0; r < n; r++)
                {
                    Assert.That(seen[result[r]], Is.False, "slots are unique");
                    seen[result[r]] = true;
                    got += cost[r * n + result[r]];
                }
                Assert.That(got, Is.EqualTo(BruteForce(cost, n)).Within(1e-3f), $"trial {trial}, n={n}");
            }
            }
            finally
            {
                cost.Dispose();
            }
        }

        static float BruteForce(NativeArray<float> cost, int n)
        {
            var perm = new int[n];
            for (int i = 0; i < n; i++) perm[i] = i;
            float best = float.MaxValue;
            Permute(perm, 0, cost, n, ref best);
            return best;
        }

        static void Permute(int[] perm, int k, NativeArray<float> cost, int n, ref float best)
        {
            if (k == n)
            {
                float sum = 0f;
                for (int r = 0; r < n; r++) sum += cost[r * n + perm[r]];
                best = math.min(best, sum);
                return;
            }
            for (int i = k; i < n; i++)
            {
                (perm[k], perm[i]) = (perm[i], perm[k]);
                Permute(perm, k + 1, cost, n, ref best);
                (perm[k], perm[i]) = (perm[i], perm[k]);
            }
        }

        // ---- Formation in the world ----

        [Test]
        public void SpawnedFormation_StandsInItsSlots_FacingForward()
        {
            using var world = World(30);
            int f = world.SpawnFormation(0, 0, FormationType.Line, 10, 30, new float2(48f, 48f), new float2(1f, 0f), TestScenarios.AgentRadius);
            Run(world, 60);
            Assert.That(MaxSlotError(world, f), Is.LessThan(0.15f));
            for (int i = 0; i < 30; i++)
                Assert.That(math.dot(world.Agents[i].Facing, new float2(1f, 0f)), Is.GreaterThan(0.99f), $"agent {i}");
            Assert.That(world.FormationMembers[0].Slot, Is.EqualTo(0), "spawn order = slot order");
        }

        [Test]
        public void MoveOrder_MarchesTogether_AndArrives()
        {
            using var world = World(30);
            int f = world.SpawnFormation(0, 0, FormationType.Line, 10, 30, new float2(20f, 40f), new float2(1f, 0f), TestScenarios.AgentRadius);
            Run(world, 10);
            var dest = new float2(50f, 40f);
            world.SubmitFormationOrder(0, FormationOrder.MoveTo(f, dest));

            float worstWhileMarching = 0f;
            for (int t = 0; t < 1800; t++)
            {
                world.Step();
                worstWhileMarching = math.max(worstWhileMarching, MaxSlotError(world, f));
            }
            Assert.That(math.distance(world.Formations[f].Position, dest), Is.LessThan(1e-3f));
            Assert.That(MaxSlotError(world, f), Is.LessThan(0.2f), "arrived in the slots");
            Assert.That(worstWhileMarching, Is.LessThan(0.8f), "the line stays a line while marching");
            for (int i = 0; i < 30; i++)
                Assert.That(world.FormationMembers[i].Slot, Is.EqualTo(i), "a pure move keeps every soldier in the same slot");
        }

        [Test]
        public void AboutFace_RearRankBecomesTheFront_WithShortWalks()
        {
            using var world = World(30);
            int f = world.SpawnFormation(0, 0, FormationType.Line, 10, 30, new float2(40f, 40f), new float2(1f, 0f), TestScenarios.AgentRadius);
            Run(world, 10);
            var before = new float2[30];
            for (int i = 0; i < 30; i++) before[i] = world.Agents[i].Position;

            var anchor = world.Formations[f].Position;
            world.SubmitFormationOrder(0, FormationOrder.MoveTo(f, anchor, new float2(-1f, 0f)));
            Run(world, 900);

            var formation = world.Formations[f];
            Assert.That(math.dot(formation.Facing, new float2(-1f, 0f)), Is.GreaterThan(0.999f));
            Assert.That(MaxSlotError(world, f), Is.LessThan(0.2f));
            float depth = 2f * TestScenarios.Formations.Line.Depth;
            for (int i = 0; i < 30; i++)
            {
                Assert.That(math.distance(before[i], world.Agents[i].Position), Is.LessThan(depth + 0.5f), $"agent {i} walks at most through the formation");
                Assert.That(math.dot(world.Agents[i].Facing, new float2(-1f, 0f)), Is.GreaterThan(0.99f), $"agent {i} faces the new front");
            }
            // The former rear rank (agents 20..29) is now the front rank (slots 0..9)
            for (int i = 20; i < 30; i++) Assert.That(world.FormationMembers[i].Slot, Is.LessThan(10), $"agent {i}");
        }

        [Test]
        public void Reshape_AndWidth_ReformOnTheSpot([ValueSource(nameof(AllTypes))] FormationType type)
        {
            using var world = World(24);
            int f = world.SpawnFormation(0, 0, FormationType.Line, 8, 24, new float2(48f, 48f), new float2(0f, 1f), TestScenarios.AgentRadius);
            Run(world, 10);
            float2 centroidBefore = Centroid(world);
            world.SubmitFormationOrder(0, FormationOrder.Reshape(f, type));
            world.SubmitFormationOrder(0, FormationOrder.SetWidth(f, 6));
            Run(world, 1200);

            Assert.That(world.Formations[f].Type, Is.EqualTo(type));
            Assert.That(world.Formations[f].Files, Is.EqualTo(6));
            Assert.That(MaxSlotError(world, f), Is.LessThan(0.3f), $"{type}");
            Assert.That(math.distance(Centroid(world), centroidBefore), Is.LessThan(1.5f), "reforms where the soldiers stand");
            var slots = new bool[24];
            for (int i = 0; i < 24; i++)
            {
                int s = world.FormationMembers[i].Slot;
                Assert.That(slots[s], Is.False);
                slots[s] = true;
            }
        }

        static float2 Centroid(SimWorld world)
        {
            float2 c = float2.zero;
            for (int i = 0; i < world.AgentCount; i++) c += world.Agents[i].Position;
            return c / world.AgentCount;
        }

        [Test]
        public void Orders_FromOtherPlayers_OrInvalid_AreIgnored()
        {
            using var world = World(20);
            int f = world.SpawnFormation(0, 0, FormationType.Line, 5, 10, new float2(20f, 20f), new float2(1f, 0f), TestScenarios.AgentRadius);
            int g = world.SpawnFormation(1, 1, FormationType.Line, 5, 10, new float2(60f, 20f), new float2(-1f, 0f), TestScenarios.AgentRadius);
            world.SubmitFormationOrder(1, FormationOrder.MoveTo(f, new float2(40f, 40f)));          // not the owner
            world.SubmitFormationOrder(0, FormationOrder.MoveTo(7, new float2(40f, 40f)));          // no such formation
            world.SubmitFormationOrder(0, FormationOrder.MoveTo(f, new float2(float.NaN, 1f)));     // not finite
            world.SubmitFormationOrder(0, FormationOrder.Reshape(f, (FormationType)99));            // no such type
            world.SubmitFormationOrder(0, FormationOrder.SetWidth(f, 0));                           // no width
            Run(world, 60);

            Assert.That(world.Formations[f].Destination, Is.EqualTo(new float2(20f, 20f)));
            Assert.That(world.Formations[f].Type, Is.EqualTo(FormationType.Line));
            Assert.That(world.Formations[f].Files, Is.EqualTo(5));
            Assert.That(world.Formations[g].Destination, Is.EqualTo(new float2(60f, 20f)));

            // The commander AI uses the same order directly, with the same ownership rule
            Assert.That(world.ApplyFormationOrder(FormationOrder.MoveTo(g, new float2(50f, 20f)), 0), Is.False);
            Assert.That(world.ApplyFormationOrder(FormationOrder.MoveTo(g, new float2(50f, 20f)), 1), Is.True);
        }

        [Test]
        public void Setup_RejectsHeroesInFormations_AndOverfullFormations()
        {
            using var world = World(4);
            int hero = world.SpawnAgent(0, new float2(10f, 10f), TestScenarios.AgentRadius);
            int soldier = world.SpawnAgent(0, new float2(12f, 10f), TestScenarios.AgentRadius);
            int enemy = world.SpawnAgent(1, new float2(14f, 10f), TestScenarios.AgentRadius);
            world.AssignPlayerAgent(0, hero);
            int f = world.CreateFormation(0, 0, FormationType.Line, 4, new float2(12f, 12f), new float2(0f, 1f));
            Assert.Throws<InvalidOperationException>(() => world.AddToFormation(f, hero));
            Assert.Throws<InvalidOperationException>(() => world.AddToFormation(f, enemy), "wrong team");
            world.AddToFormation(f, soldier);
            Assert.Throws<InvalidOperationException>(() => world.AddToFormation(f, soldier), "twice");
            Assert.Throws<InvalidOperationException>(() => world.AssignPlayerAgent(1, soldier));
        }

        [Test]
        public void SameOrders_SameHash_DifferentOrders_DifferentHash()
        {
            ulong Play(float2 destination)
            {
                using var world = World(60, 96, 3);
                int a = world.SpawnFormation(0, 0, FormationType.ShieldWall, 10, 30, new float2(30f, 48f), new float2(1f, 0f), TestScenarios.AgentRadius);
                int b = world.SpawnFormation(1, 1, FormationType.Wedge, 9, 30, new float2(66f, 48f), new float2(-1f, 0f), TestScenarios.AgentRadius);
                for (int t = 0; t < 2000; t++)
                {
                    if (t == 10) world.SubmitFormationOrder(0, FormationOrder.MoveTo(a, destination, new float2(0f, 1f)));
                    if (t == 300) world.SubmitFormationOrder(1, FormationOrder.Reshape(b, FormationType.Circle));
                    if (t == 900) world.SubmitFormationOrder(0, FormationOrder.SetWidth(a, 6));
                    world.Step();
                }
                return world.ComputeHash();
            }

            ulong h1 = Play(new float2(45f, 40f));
            Assert.That(Play(new float2(45f, 40f)), Is.EqualTo(h1));
            Assert.That(Play(new float2(45f, 41f)), Is.Not.EqualTo(h1));
        }

        [Test]
        public void OrderCodec_RoundTrips()
        {
            var order = new FormationOrder
            {
                Formation = 3,
                Flags = FormationOrderFlags.Move | FormationOrderFlags.Shape | FormationOrderFlags.Width,
                Type = FormationType.Loose,
                Files = 37,
                Destination = new float2(1.5f, -2f),
                Facing = new float2(0f, -1f),
            };
            order.ToArgs(out int4 i, out float4 f);
            var back = FormationOrder.FromCommand(new InputCommand { Type = InputCommandType.FormationOrder, IntArgs = i, FloatArgs = f });
            Assert.That(back.Formation, Is.EqualTo(3));
            Assert.That(back.Flags, Is.EqualTo(order.Flags));
            Assert.That(back.Type, Is.EqualTo(FormationType.Loose));
            Assert.That(back.Files, Is.EqualTo(37));
            Assert.That(back.Destination, Is.EqualTo(order.Destination));
            Assert.That(back.Facing, Is.EqualTo(order.Facing));
        }
    }
}
