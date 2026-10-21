using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Orders;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shieldbreak.Tests
{
    /// <summary>Quick orders (radial menu) and the M2 company scenario: the hero leads his own formations.</summary>
    public class CompanyTests
    {
        const ushort Player = CompanyScenarioDefinition.PlayerId;
        readonly List<Object> created = new List<Object>();
        CompanyScenarioDefinition scenario;
        AgentMovementDefinition movement;

        [SetUp]
        public void SetUp()
        {
            scenario = ScriptableObject.CreateInstance<CompanyScenarioDefinition>();
            scenario.ApplyDefaultLayout();
            scenario.weapons = TestCombat.Weapons(created, out var tuning);
            scenario.combatTuning = tuning;
            scenario.morale = ScriptableObject.CreateInstance<MoraleDefinition>();
            movement = ScriptableObject.CreateInstance<AgentMovementDefinition>();
            created.Add(scenario);
            created.Add(scenario.morale);
            created.Add(movement);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        SimWorld World(out int hero) => scenario.CreateWorld(movement.ToSettings(), out hero);

        static List<int> Commanded(SimWorld world, int owner)
        {
            using var ids = new NativeArray<int>(world.Formations.Length, Allocator.Temp);
            int n = QuickOrders.Commanded(world.Formations, owner, ids);
            var list = new List<int>();
            for (int k = 0; k < n; k++) list.Add(ids[k]);
            return list;
        }

        static void Run(SimWorld world, int ticks)
        {
            for (int t = 0; t < ticks; t++) world.Step();
        }

        // ---- Quick orders ----

        [Test]
        public void QuickCommands_BecomeTheMatchingFormationOrders()
        {
            var expected = new (QuickCommand command, FormationOrderFlags flags, FormationBehavior behavior, FormationType type)[]
            {
                (QuickCommand.Hold, FormationOrderFlags.Behavior, FormationBehavior.Hold, default),
                (QuickCommand.Advance, FormationOrderFlags.Behavior, FormationBehavior.Advance, default),
                (QuickCommand.Charge, FormationOrderFlags.Behavior, FormationBehavior.Charge, default),
                (QuickCommand.Follow, FormationOrderFlags.Behavior, FormationBehavior.Follow, default),
                (QuickCommand.Retreat, FormationOrderFlags.Behavior, FormationBehavior.Retreat, default),
                (QuickCommand.Line, FormationOrderFlags.Shape, default, FormationType.Line),
                (QuickCommand.ShieldWall, FormationOrderFlags.Shape, default, FormationType.ShieldWall),
                (QuickCommand.Wedge, FormationOrderFlags.Shape, default, FormationType.Wedge),
            };
            Assert.That(expected.Length, Is.EqualTo(QuickOrders.CommandCount));
            foreach (var e in expected)
            {
                Assert.That(QuickOrders.TryBuild(e.command, 3, 7, out var order), e.command.ToString());
                Assert.That(order.Formation, Is.EqualTo(3));
                Assert.That(order.Flags, Is.EqualTo(e.flags), e.command.ToString());
                if (e.flags == FormationOrderFlags.Behavior) Assert.That(order.Behavior, Is.EqualTo(e.behavior));
                else Assert.That(order.Type, Is.EqualTo(e.type));
            }
            QuickOrders.TryBuild(QuickCommand.Follow, 3, 7, out var follow);
            Assert.That(follow.FollowAgent, Is.EqualTo(7));
            Assert.That(QuickOrders.TryBuild(QuickCommand.Follow, 3, -1, out _), Is.False, "nobody to follow without a hero");
            Assert.That(QuickOrders.IsAvailable(QuickCommand.Follow, -1), Is.False);
            Assert.That(QuickOrders.IsAvailable(QuickCommand.Charge, -1));
        }

        // ---- Company scenario ----

        [Test]
        public void Company_HeroLeadsHisOwnFormations_TheEnemyAdvances()
        {
            using var world = World(out int hero);
            Assert.That(hero, Is.EqualTo(0));
            Assert.That(world.PlayerAgent(Player), Is.EqualTo(hero));
            Assert.That(world.Agents[hero].BaseSpeedScale, Is.EqualTo(scenario.heroSpeedScale));
            Assert.That(world.Combat[hero].BotProfile, Is.EqualTo(0), "the player plays the hero");
            Assert.That(world.HasCombat && world.HasMorale);
            Assert.That(world.AgentCount, Is.EqualTo(scenario.TotalAgents));

            Assert.That(Commanded(world, Player), Is.EqualTo(new List<int> { 0, 1 }), "the player commands his two formations");
            Assert.That(Commanded(world, CompanyScenarioDefinition.EnemyId), Is.EqualTo(new List<int> { 2, 3 }));
            for (int f = 0; f < 4; f++)
            {
                var data = world.Formations[f];
                Assert.That(data.Team, Is.EqualTo(f < 2 ? 0 : 1));
                Assert.That(data.Behavior, Is.EqualTo(f < 2 ? FormationBehavior.Hold : FormationBehavior.Advance), scenario.units[f].name);
            }
            for (int i = 1; i < world.AgentCount; i++) Assert.That(world.Combat[i].BotProfile, Is.EqualTo(3), $"soldier {i}");

            Run(world, 600);
            Assert.That(world.Formations[2].Position.y, Is.LessThan(66f - 5f), "the enemy marches towards the hero");
            Assert.That(world.Formations[0].Position, Is.EqualTo(new float2(40f, 30f)), "own formations wait for orders");
        }

        [Test]
        public void QuickOrders_GoThroughTheInputQueue_AndOnlyToOwnFormations()
        {
            using var world = World(out int hero);
            foreach (int f in Commanded(world, Player))
            {
                Assert.That(QuickOrders.TryBuild(QuickCommand.Follow, f, hero, out var order));
                world.SubmitFormationOrder(Player, order);
            }
            // Orders for the enemy's formations are ignored
            Assert.That(QuickOrders.TryBuild(QuickCommand.Retreat, 2, hero, out var foreign));
            world.SubmitFormationOrder(Player, foreign);
            Run(world, scenario.inputDelayTicks + 1);
            Assert.That(world.Formations[0].Behavior, Is.EqualTo(FormationBehavior.Follow));
            Assert.That(world.Formations[0].FollowAgent, Is.EqualTo(hero));
            Assert.That(world.Formations[1].Behavior, Is.EqualTo(FormationBehavior.Follow));
            Assert.That(world.Formations[2].Behavior, Is.EqualTo(FormationBehavior.Advance));

            // The hero walks south; his formations come after him
            for (int t = 0; t < 900; t++)
            {
                if (t % 30 == 0) world.Submit(Player, InputCommandType.PlayerMove, new int4(hero, 0, 0, 0), new float4(0f, -1f, 0f, -1f));
                world.Step();
            }
            Assert.That(world.Agents[hero].Position.y, Is.LessThan(15f));
            Assert.That(world.Formations[0].Position.y, Is.LessThan(24f), "the line follows the hero");
        }

        [Test]
        public void Company_SameQuickOrders_SameHash()
        {
            ulong Battle(QuickCommand command)
            {
                using var world = World(out int hero);
                for (int t = 0; t < 1800; t++)
                {
                    if (t == 120)
                        foreach (int f in Commanded(world, Player))
                            if (QuickOrders.TryBuild(command, f, hero, out var order)) world.SubmitFormationOrder(Player, order);
                    world.Step();
                }
                return world.ComputeHash();
            }
            Assert.That(Battle(QuickCommand.Charge), Is.EqualTo(Battle(QuickCommand.Charge)));
            Assert.That(Battle(QuickCommand.Charge), Is.Not.EqualTo(Battle(QuickCommand.ShieldWall)));
        }

        [Test]
        public void Company_BattleHappens_WithoutTheHeroDoingAnything()
        {
            using var world = World(out _);
            var log = new List<CombatEvent>();
            for (int t = 0; t < 90 * 60; t++)
            {
                world.Step();
                foreach (var e in world.LastEvents) if (e.Type == CombatEventType.Death || e.Type == CombatEventType.Rout) log.Add(e);
            }
            Assert.That(log.Exists(e => e.Type == CombatEventType.Death), "the companies meet and fight");
            Assert.That(log.Exists(e => e.Type == CombatEventType.Rout), "and someone breaks");
        }
    }
}
