using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Morale;
using Shieldbreak.Sim.Orders;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Shieldbreak.Tests
{
    /// <summary>The scenarios as they are shipped: the real assets, not test copies. M3 task 6: 50 against 50.</summary>
    public class BattleScenarioTests
    {
        const string Folder = "Assets/_Project/Data/Scenarios/";
        const ushort Player = CompanyScenarioDefinition.PlayerId;

        static CompanyScenarioDefinition Load(string name)
        {
            var scenario = AssetDatabase.LoadAssetAtPath<CompanyScenarioDefinition>(Folder + name + ".asset");
            Assert.That(scenario, Is.Not.Null, name);
            return scenario;
        }

        static SimWorld World(CompanyScenarioDefinition scenario)
        {
            var movement = AssetDatabase.LoadAssetAtPath<AgentMovementDefinition>("Assets/_Project/Data/Agents/AgentMovement_Default.asset");
            return scenario.CreateWorld(movement.ToSettings(), out _);
        }

        [Test]
        public void Battle_HasFiveFormationsAndFiftySoldiersASide_AndACommander()
        {
            using var world = World(Load("Battle_50v50"));
            var heads = new int[2];
            var formations = new int[2];
            for (int i = 1; i < world.AgentCount; i++) heads[world.Agents[i].Team]++;
            foreach (var f in world.Formations) formations[f.Team]++;
            Assert.That(heads, Is.EqualTo(new[] { 50, 50 }));
            Assert.That(formations, Is.EqualTo(new[] { 5, 5 }));
            Assert.That(world.Commanders.Length, Is.EqualTo(1));
            Assert.That(world.Config.MaxProjectiles, Is.GreaterThan(0));
        }

        [Test]
        public void ShippedScenarios_HaveArchersThatShoot()
        {
            foreach (string name in new[] { "Company_M2", "Battle_50v50" })
            {
                using var world = World(Load(name));
                int shots = 0;
                for (int t = 0; t < 40 * 60; t++)
                {
                    world.Step();
                    foreach (var e in world.LastEvents) if (e.Type == CombatEventType.Shot) shots++;
                }
                Assert.That(shots, Is.GreaterThan(10), name);
            }
        }

        [Test]
        public void Battle_IsDecided_WithoutAnyOrder()
        {
            var scenario = Object.Instantiate(Load("Battle_50v50"));
            try
            {
                int wins = 0;
                string line = "";
                for (int seed = 1; seed <= 6; seed++)
                {
                    scenario.seed = seed;
                    using var world = World(scenario);
                    var result = BattleResult.Undecided;
                    while (result == BattleResult.Undecided && world.Tick < 300 * 60)
                    {
                        world.Step();
                        result = BattleOutcome.Evaluate(world.Agents, 0, 0);
                    }
                    Assert.That(result, Is.Not.EqualTo(BattleResult.Undecided), $"seed {seed}, tactic {world.Commanders[0].Tactic}");
                    if (result == BattleResult.Victory) wins++;
                    line += $" {result.ToString()[0]}{world.Tick / 60}";
                }
                TestContext.WriteLine($"idle hero wins {wins}/6:{line}");
            }
            finally
            {
                Object.DestroyImmediate(scenario);
            }
        }

        /// <summary>The Feldherr test of the fun gate: the hero stays with the bot brain from the first tick.</summary>
        [Test]
        public void Battle_FromTheCommandView_TheHeroLastsLongEnoughToCommand()
        {
            var scenario = Object.Instantiate(Load("Battle_50v50"));
            try
            {
                string line = "";
                for (int seed = 1; seed <= 6; seed++)
                {
                    scenario.seed = seed;
                    using var world = World(scenario);
                    world.Submit(Player, InputCommandType.HeroUnattended, new int4(0, 1, 0, 0));
                    var result = BattleResult.Undecided;
                    while (result == BattleResult.Undecided && world.Tick < 300 * 60)
                    {
                        world.Step();
                        result = BattleOutcome.Evaluate(world.Agents, 0, 0);
                    }
                    Assert.That(result, Is.Not.EqualTo(BattleResult.Undecided), $"seed {seed}");
                    Assert.That(world.Tick, Is.GreaterThan(60 * 60), $"seed {seed}: a battle from above needs time to be led");
                    line += $" {result.ToString()[0]}{world.Tick / 60}{(world.Agents[0].IsDead ? "+" : "")}";
                }
                TestContext.WriteLine($"hero unattended, no orders (+ = hero fell):{line}");
            }
            finally
            {
                Object.DestroyImmediate(scenario);
            }
        }

        [Test, Category("Performance")]
        public void Battle_MedianStepUnderBudget_WhileEverybodyFights()
        {
            using var world = World(Load("Battle_50v50"));
            for (int t = 0; t < 40 * 60; t++) world.Step();
            const int ticks = 900;
            var samples = new double[ticks];
            var watch = new Stopwatch();
            for (int t = 0; t < ticks; t++)
            {
                watch.Restart();
                world.Step();
                samples[t] = watch.Elapsed.TotalMilliseconds;
            }
            System.Array.Sort(samples);
            double median = samples[ticks / 2], p95 = samples[ticks * 95 / 100], max = samples[ticks - 1];
            TestContext.WriteLine($"100 soldiers in battle: median {median:0.000} ms, p95 {p95:0.000} ms, max {max:0.000} ms per SimWorld.Step (budget 2 ms)");
            Assert.That(median, Is.LessThan(2.0));
        }
    }
}
