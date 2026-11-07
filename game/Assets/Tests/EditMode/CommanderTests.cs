using System.Collections.Generic;
using NUnit.Framework;
using Shieldbreak.Data;
using Shieldbreak.Sim.AI;
using Shieldbreak.Sim.Combat.Grammar;
using Shieldbreak.Sim.Core;
using Shieldbreak.Sim.Formations;
using Shieldbreak.Sim.Morale;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shieldbreak.Tests
{
    /// <summary>The commander AI: how it scores (pure) and what it does with a company (through FormationOrders only).</summary>
    public class CommanderTests
    {
        const int Enemy = CompanyScenarioDefinition.EnemyId;
        readonly List<Object> created = new List<Object>();
        CompanyScenarioDefinition scenario;
        CommanderDefinition commander;
        AgentMovementDefinition movement;

        [SetUp]
        public void SetUp()
        {
            scenario = ScriptableObject.CreateInstance<CompanyScenarioDefinition>();
            scenario.ApplyDefaultLayout();
            scenario.weapons = TestCombat.Weapons(created, out var tuning);
            scenario.combatTuning = tuning;
            scenario.morale = ScriptableObject.CreateInstance<MoraleDefinition>();
            commander = ScriptableObject.CreateInstance<CommanderDefinition>();
            commander.scoreNoise = 0f;
            scenario.commander = commander;
            movement = ScriptableObject.CreateInstance<AgentMovementDefinition>();
            created.Add(scenario);
            created.Add(scenario.morale);
            created.Add(commander);
            created.Add(movement);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        SimWorld World() => scenario.CreateWorld(movement.ToSettings(), out _);

        static void Run(SimWorld world, int ticks)
        {
            for (int t = 0; t < ticks; t++) world.Step();
        }

        static CommanderFeatures Even => new CommanderFeatures
        {
            OwnFighters = 24, EnemyFighters = 24, OwnFormations = 2, Ratio = 1f, OwnMorale = 1f, EnemyMorale = 1f,
            OwnFront = 14f, EnemyFront = 14f, Distance = 36f, OpenLeft = 0.5f, OpenRight = 0.5f,
        };

        CommanderScores Score(in CommanderFeatures f) => CommanderUtility.Score(f, commander.ToSettings());

        // ---- Scores ----

        [Test]
        public void Scores_TheStrongerAttack_TheWeakerStand()
        {
            var strong = Even;
            strong.Ratio = 1.6f;
            var s = Score(strong);
            Assert.That(s.Frontal, Is.GreaterThan(s.Hold), "stronger: attack");
            var weak = Even;
            weak.Ratio = 0.7f;
            s = Score(weak);
            Assert.That(s.Hold, Is.GreaterThan(s.Frontal), "weaker: stand");
            s = Score(Even);
            Assert.That(s.Frontal, Is.GreaterThan(s.Hold), "at even strength someone has to start");
        }

        [Test]
        public void Scores_AHigherMorale_FavoursTheAttack()
        {
            var steady = Even;
            steady.EnemyMorale = 0.5f;
            Assert.That(Score(steady).Frontal, Is.GreaterThan(Score(Even).Frontal));
        }

        [Test]
        public void Scores_TheFlankGoesToTheEmptySide_AndNeedsRoomAndASecondFormation()
        {
            var open = Even;
            open.OpenLeft = 0.8f;
            open.OpenRight = 0.2f;
            var s = Score(open);
            Assert.That(s.FlankLeft, Is.GreaterThan(s.FlankRight), "the side with few enemies");

            var close = open;
            close.Distance = 5f;
            s = Score(close);
            Assert.That(s.FlankLeft, Is.Zero, "no room left");
            var alone = open;
            alone.OwnFormations = 1;
            Assert.That(Score(alone).FlankLeft, Is.Zero, "one formation cannot pin and flank");
            commander.allowFlank = false;
            Assert.That(Score(open).FlankLeft, Is.Zero, "an easier commander does not flank");
        }

        [Test]
        public void Scores_RetreatComesWithWeaknessAndPanic()
        {
            Assert.That(Score(Even).Retreat, Is.Zero);
            var beaten = Even;
            beaten.Ratio = 0.25f;
            beaten.OwnMorale = 0.2f;
            var s = Score(beaten);
            Assert.That(s.Retreat, Is.GreaterThan(s.Hold));
            Assert.That(s.Retreat, Is.GreaterThan(s.Frontal));
        }

        [Test]
        public void Decide_HysteresisKeepsThePlan_MinimumTimeToo_ButRetreatWins()
        {
            var settings = commander.ToSettings();
            var rng = new SimRandom(1);
            var scores = new CommanderScores { Hold = 0.5f, Frontal = 0.55f };
            Assert.That(CommanderUtility.Decide(ref scores, CommanderTactic.Hold, 100000, settings, ref rng), Is.EqualTo(CommanderTactic.Hold), "a close rival does not change the plan");
            scores = new CommanderScores { Hold = 0.2f, Frontal = 0.9f };
            Assert.That(CommanderUtility.Decide(ref scores, CommanderTactic.Hold, 100000, settings, ref rng), Is.EqualTo(CommanderTactic.Frontal), "a clear winner does");
            scores = new CommanderScores { Hold = 0.2f, Frontal = 0.9f };
            Assert.That(CommanderUtility.Decide(ref scores, CommanderTactic.Hold, 10, settings, ref rng), Is.EqualTo(CommanderTactic.Hold), "not before the minimum time");
            scores = new CommanderScores { Hold = 0.2f, Retreat = 0.9f };
            Assert.That(CommanderUtility.Decide(ref scores, CommanderTactic.Hold, 10, settings, ref rng), Is.EqualTo(CommanderTactic.Retreat), "a retreat does not wait");
        }

        [Test]
        public void Decide_NoiseNeverWakesAnImpossibleTactic()
        {
            var settings = commander.ToSettings();
            settings.ScoreNoise = 5f;
            settings.Hysteresis = 0f;
            var rng = new SimRandom(3);
            for (int i = 0; i < 200; i++)
            {
                var scores = new CommanderScores { Hold = 0.1f, Frontal = 0.1f };
                var tactic = CommanderUtility.Decide(ref scores, CommanderTactic.Frontal, 100000, settings, ref rng);
                Assert.That(tactic, Is.EqualTo(CommanderTactic.Hold).Or.EqualTo(CommanderTactic.Frontal));
            }
        }

        [Test]
        public void FlankPoint_LiesBeyondTheEndOfTheLine_FacingAlongIt()
        {
            var f = Even;
            f.EnemyCentre = new float2(50f, 60f);
            f.Axis = new float2(0f, 1f);    // looking north: right is east
            f.SpreadRight = 8f;
            f.SpreadLeft = 5f;
            var settings = commander.ToSettings();
            float2 right = CommanderUtility.FlankPoint(f, CommanderTactic.FlankRight, settings, out float2 facing);
            Assert.That(right, Is.EqualTo(new float2(50f + 8f + commander.flankMargin, 60f)));
            Assert.That(facing, Is.EqualTo(new float2(-1f, 0f)));
            float2 left = CommanderUtility.FlankPoint(f, CommanderTactic.FlankLeft, settings, out facing);
            Assert.That(left, Is.EqualTo(new float2(50f - 5f - commander.flankMargin, 60f)));
            Assert.That(facing, Is.EqualTo(new float2(1f, 0f)));
        }

        // ---- In the company ----

        static CommanderState State(SimWorld world) => world.Commanders[0];

        [Test]
        public void StateStructs_HaveNoPadding()
        {
            Assert.That(UnsafeUtility.SizeOf<CommanderFeatures>(), Is.EqualTo(76));
            Assert.That(UnsafeUtility.SizeOf<CommanderScores>(), Is.EqualTo(20));
            Assert.That(UnsafeUtility.SizeOf<CommanderState>(), Is.EqualTo(136));
        }

        [Test]
        public void WithoutACommander_NothingChanges()
        {
            scenario.commander = null;
            using var world = World();
            Assert.That(world.Commanders.Length, Is.Zero);
            Run(world, 300);
            Assert.That(world.Formations[2].Behavior, Is.EqualTo(FormationBehavior.Advance), "the start behavior stays");
        }

        [Test]
        public void Commander_ReadsTheField_AndAttacksAtEvenStrength()
        {
            commander.allowFlank = false;
            using var world = World();
            Run(world, 2);
            var c = State(world);
            Assert.That(c.Owner, Is.EqualTo(Enemy));
            Assert.That(c.Features.OwnFighters, Is.EqualTo(24));
            Assert.That(c.Features.EnemyFighters, Is.EqualTo(25), "the hero counts");
            Assert.That(c.Features.OwnFormations, Is.EqualTo(2));
            Assert.That(c.Features.Distance, Is.InRange(30f, 40f));
            Assert.That(c.Tactic, Is.EqualTo(CommanderTactic.Frontal));
            Run(world, 600);
            Assert.That(world.Formations[2].Behavior, Is.EqualTo(FormationBehavior.Advance));
            Assert.That(world.Formations[2].Position.y, Is.LessThan(61f), "it marches");
        }

        [Test]
        public void Commander_OnlyCommandsItsOwnFormations()
        {
            using var world = World();
            Run(world, 1200);
            Assert.That(world.Formations[0].Owner, Is.Not.EqualTo(Enemy));
            Assert.That(world.Formations[0].Behavior, Is.EqualTo(FormationBehavior.Hold), "the player's formations are left alone");
            Assert.That(world.Formations[0].Position, Is.EqualTo(new float2(40f, 30f)));
        }

        [Test]
        public void Commander_TheWeakerSideStands()
        {
            scenario.units[2].count = 4;
            scenario.units[3].count = 4;
            using var world = World();
            Run(world, 600);
            var c = State(world);
            Assert.That(c.Tactic, Is.EqualTo(CommanderTactic.Hold));
            Assert.That(world.Formations[2].Behavior, Is.EqualTo(FormationBehavior.Hold), "it gives up its start advance");
            Assert.That(world.Formations[2].Stance, Is.EqualTo(FormationStance.React), "and waits to meet whoever comes near");
        }

        [Test]
        public void Commander_SendsTheOuterFormationRound_TheOtherPins_AndTheFlankerFightsOnArrival()
        {
            commander.frontalBase = 0f;
            commander.holdBase = 0f;
            commander.flankBase = 1f;
            using var world = World();
            Run(world, 2);
            var c = State(world);
            Assert.That(c.Tactic, Is.EqualTo(CommanderTactic.FlankLeft).Or.EqualTo(CommanderTactic.FlankRight));
            Assert.That(c.Flanker, Is.EqualTo(2).Or.EqualTo(3));
            int flanker = c.Flanker, pin = flanker == 2 ? 3 : 2;
            Assert.That(world.Formations[flanker].Stance, Is.EqualTo(FormationStance.Aggressive));
            Assert.That(world.Formations[flanker].Behavior, Is.EqualTo(FormationBehavior.Hold), "marching to the end of the line");
            Assert.That(world.Formations[pin].Behavior, Is.EqualTo(FormationBehavior.Advance), "the other one pins the enemy");

            // On arrival the stance takes it into the enemy
            bool attacked = false;
            for (int t = 0; t < 90 * 60 && !attacked; t++)
            {
                world.Step();
                attacked = world.Formations[flanker].Reacting != 0 || world.Formations[flanker].MemberCount == 0;
            }
            Assert.That(attacked, "the flanker attacks once it has arrived");
        }

        [Test]
        public void Commander_ChoosesToRetreat_WhenBeaten()
        {
            commander.retreatRatio = 2f;    // this commander already thinks himself beaten
            using var world = World();
            Run(world, 2);
            Assert.That(State(world).Tactic, Is.EqualTo(CommanderTactic.Retreat));
            Assert.That(world.Formations[2].Behavior, Is.EqualTo(FormationBehavior.Retreat));
            Assert.That(world.Formations[3].Behavior, Is.EqualTo(FormationBehavior.Retreat));
        }

        [Test]
        public void Commander_KeepsItsPlan_ThroughABattle()
        {
            commander.scoreNoise = 0.1f;
            using var world = World();
            int changes = 0;
            var last = CommanderTactic.Frontal;
            for (int t = 0; t < 90 * 60; t++)
            {
                world.Step();
                if (world.Commanders[0].Tactic != last)
                {
                    last = world.Commanders[0].Tactic;
                    changes++;
                }
            }
            int limit = 90 * 60 / commander.ToSettings().MinTacticTicks + 1;
            Assert.That(changes, Is.LessThanOrEqualTo(limit), "no dithering");
        }

        [Test]
        public void Commander_SameSeed_SameBattle_OtherSeed_OtherBattle()
        {
            commander.scoreNoise = 0.3f;
            ulong Battle(int seed)
            {
                scenario.seed = seed;
                using var world = World();
                Run(world, 40 * 60);
                return world.ComputeHash();
            }
            Assert.That(Battle(1), Is.EqualTo(Battle(1)));
            Assert.That(Battle(1), Is.Not.EqualTo(Battle(2)));
        }

        [Test]
        public void Commander_DoesNotStandForEver()
        {
            var f = Even;
            var settings = commander.ToSettings();
            float frontal = CommanderUtility.Score(f, settings).Frontal;
            Assert.That(CommanderUtility.Score(f, settings, 0).Hold + settings.Hysteresis, Is.GreaterThan(frontal), "holding at first");
            Assert.That(CommanderUtility.Score(f, settings, 60 * 60).Hold + settings.Hysteresis, Is.LessThan(frontal), "not after a minute");
        }

        [Test]
        public void Company_EverySeed_IsDecided_NobodyStandsAndStares()
        {
            commander.scoreNoise = 0.1f;
            int wins = 0;
            string line = "";
            for (int seed = 1; seed <= 12; seed++)
            {
                scenario.seed = seed;
                using var world = World();
                var result = BattleResult.Undecided;
                while (result == BattleResult.Undecided && world.Tick < 240 * 60)
                {
                    world.Step();
                    result = BattleOutcome.Evaluate(world.Agents, 0, 0);
                }
                Assert.That(result, Is.Not.EqualTo(BattleResult.Undecided), $"seed {seed} after {world.Tick / 60} s, tactic {State(world).Tactic}");
                if (result == BattleResult.Victory) wins++;
                line += $" {result.ToString()[0]}{world.Tick / 60}";
            }
            TestContext.WriteLine($"idle hero wins {wins}/12:{line}");
        }

        [Test]
        public void Company_WithACommander_IsDecided_AndHasCasualties()
        {
            using var world = World();
            var log = new List<CombatEvent>();
            var result = BattleResult.Undecided;
            for (int t = 0; t < 180 * 60 && result == BattleResult.Undecided; t++)
            {
                world.Step();
                foreach (var e in world.LastEvents) if (e.Type == CombatEventType.Death) log.Add(e);
                result = BattleOutcome.Evaluate(world.Agents, 0, 0);
            }
            Assert.That(result, Is.Not.EqualTo(BattleResult.Undecided));
            Assert.That(log.Count, Is.GreaterThan(0));
            TestContext.WriteLine($"{result} after {world.Tick * SimConstants.DT:0} s, {log.Count} dead, last tactic {State(world).Tactic}");
        }
    }
}
