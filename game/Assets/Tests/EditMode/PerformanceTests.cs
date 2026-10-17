using System;
using System.Diagnostics;
using NUnit.Framework;
using Shieldbreak.Sim.Core;
using Unity.Mathematics;

namespace Shieldbreak.Tests
{
    /// <summary>
    /// M0 fun gate: 500 agents at 60 Hz under 2 ms sim time per tick. Worst case for M0 systems: two blocks of 250
    /// march into each other, so every agent has a full neighbourhood for separation.
    /// </summary>
    public class PerformanceTests
    {
        const double BudgetMs = 2.0;

        [Test, Category("Performance")]
        public void FiveHundredAgents_ColliderBlocks_MedianStepUnderBudget()
        {
            using var world = new SimWorld(TestScenarios.Config(3, 128, 500));
            for (int team = 0; team < 2; team++)
            for (int k = 0; k < 250; k++)
            {
                float x = team == 0 ? 50f - (k / 25) * 1.1f : 78f + (k / 25) * 1.1f;
                float y = 64f + (k % 25 - 12f) * 1.1f;
                world.SpawnAgent(team, new float2(x, y), TestScenarios.AgentRadius, new float2(team == 0 ? 1f : -1f, 0f));
            }
            world.Submit(0, InputCommandType.SetTargetRange, new int4(0, 250, 0, 0), new float4(78f, 64f, 0f, 0f));
            world.Submit(0, InputCommandType.SetTargetRange, new int4(250, 250, 0, 0), new float4(50f, 64f, 0f, 0f));

            // Warm up (Burst compilation, caches) and let the blocks meet
            for (int t = 0; t < 600; t++) world.Step();

            const int ticks = 600;
            var samples = new double[ticks];
            var watch = new Stopwatch();
            for (int t = 0; t < ticks; t++)
            {
                watch.Restart();
                world.Step();
                watch.Stop();
                samples[t] = watch.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            double median = samples[ticks / 2];
            double p95 = samples[(int)(ticks * 0.95)];
            double max = samples[ticks - 1];
            TestContext.WriteLine($"500 agents, blocks in contact: median {median:0.000} ms, p95 {p95:0.000} ms, max {max:0.000} ms per SimWorld.Step (budget {BudgetMs} ms)");

            Assert.That(median, Is.LessThan(BudgetMs), $"median {median:0.000} ms, p95 {p95:0.000} ms");
        }
    }
}
