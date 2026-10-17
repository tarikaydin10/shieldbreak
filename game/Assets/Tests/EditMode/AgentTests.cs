using System.Diagnostics;
using NUnit.Framework;
using Shieldbreak.Sim.Core;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    public class AgentTests
    {
        static InputCommand MoveAll(SimWorld world, float2 target) =>
            world.Submit(0, InputCommandType.SetTargetRange, new int4(0, world.AgentCount, 0, 0), new float4(target, 0f, 0f));

        [Test]
        public void SingleAgent_ArrivesAndStops_WithinSpeedAndAccelerationLimits()
        {
            using var world = new SimWorld(TestScenarios.Config(1, 32, 1));
            var m = world.Config.Movement;
            world.SpawnAgent(0, new float2(5f, 5f), TestScenarios.AgentRadius);
            var target = new float2(20f, 12f);
            MoveAll(world, target);

            float2 previousVelocity = float2.zero;
            for (int t = 0; t < 1200; t++)
            {
                world.Step();
                var a = world.Agents[0];
                Assert.That(math.length(a.Velocity), Is.LessThanOrEqualTo(m.MaxSpeed + 1e-4f), $"tick {t}");
                Assert.That(math.length(a.Velocity - previousVelocity), Is.LessThanOrEqualTo(m.MaxAcceleration * SimConstants.DT + 1e-4f), $"tick {t}");
                previousVelocity = a.Velocity;
            }

            var end = world.Agents[0];
            Assert.That(math.distance(end.Position, target), Is.LessThan(m.StopRadius + 0.05f));
            Assert.That(math.length(end.Velocity), Is.LessThan(0.05f));
            Assert.That(math.dot(end.Facing, math.normalize(target - new float2(5f, 5f))), Is.GreaterThan(0.9f), "facing along the walk");
        }

        [Test]
        public void CoincidentAgents_PushEachOtherApart()
        {
            using var world = new SimWorld(TestScenarios.Config(1, 16, 2));
            world.SpawnAgent(0, new float2(8f, 8f), TestScenarios.AgentRadius);
            world.SpawnAgent(1, new float2(8f, 8f), TestScenarios.AgentRadius);
            for (int t = 0; t < 180; t++) world.Step();

            var a = world.Agents[0];
            var b = world.Agents[1];
            Assert.That(math.all(math.isfinite(a.Position)) && math.all(math.isfinite(b.Position)));
            Assert.That(math.distance(a.Position, b.Position), Is.GreaterThan(0.4f));
        }

        [Test]
        public void Crowd_StaysFiniteAndOnTheMap()
        {
            using var world = new SimWorld(TestScenarios.Config(7, 48, 500));
            TestScenarios.SpawnRandom(world, 500);
            MoveAll(world, new float2(24f, 24f));
            for (int t = 0; t < 900; t++) world.Step();

            foreach (var a in world.Agents)
            {
                Assert.That(math.all(math.isfinite(a.Position)) && math.all(math.isfinite(a.Velocity)), $"agent {a.Id}");
                Assert.That(math.all(a.Position >= 0f) && math.all(a.Position < 48f), $"agent {a.Id} at {a.Position}");
            }
        }

        [Test]
        public void InvalidTargetCommands_AreClampedOrIgnored()
        {
            using var world = new SimWorld(TestScenarios.Config(1, 16, 3));
            for (int i = 0; i < 3; i++) world.SpawnAgent(0, new float2(2f + i, 2f), TestScenarios.AgentRadius);
            world.Submit(0, InputCommandType.SetTargetRange, new int4(1, int.MaxValue, 0, 0), new float4(10f, 10f, 0f, 0f));
            world.Submit(0, InputCommandType.SetTargetRange, new int4(-5, 2, 0, 0), new float4(float.NaN, 1f, 0f, 0f));
            for (int t = 0; t < 7; t++) world.Step();

            Assert.That(world.Agents[0].Target, Is.EqualTo(new float2(2f, 2f)));
            Assert.That(world.Agents[1].Target, Is.EqualTo(new float2(10f, 10f)));
            Assert.That(world.Agents[2].Target, Is.EqualTo(new float2(10f, 10f)));
        }

        [Test]
        public void FiveHundredAgents_ReportStepTime()
        {
            using var world = new SimWorld(TestScenarios.Config(3, 128, 500));
            TestScenarios.SpawnRandom(world, 500, margin: 40f);
            MoveAll(world, new float2(64f, 64f));
            for (int t = 0; t < 60; t++) world.Step();   // warm up Burst and caches

            const int ticks = 600;
            var watch = Stopwatch.StartNew();
            for (int t = 0; t < ticks; t++) world.Step();
            watch.Stop();
            double ms = watch.Elapsed.TotalMilliseconds / ticks;
            TestContext.WriteLine($"500 agents: {ms:0.000} ms per SimWorld.Step (target < 2 ms, measured properly in M0 task 7)");
            Assert.Pass($"{ms:0.000} ms per tick");
        }
    }
}
