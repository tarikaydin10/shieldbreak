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
            OwnFighters = 24, EnemyFighters = 24, OwnFormations = 2, Ratio = 1f, Balance = 1f, OwnMorale = 1f, EnemyMorale = 1f,
            OwnFront = 14f, EnemyFront = 14f, Distance = 36f, OpenLeft = 0.5f, OpenRight = 0.5f,
        };

        CommanderScores Score(in CommanderFeatures f) => CommanderUtility.Score(f, commander.ToSettings());

        // ---- Scores ----

        [Test]
        public void Scores_TheStrongerAttack_TheWeakerStand()
        {
            var strong = Even;
            strong.Balance = 1.6f;
            var s = Score(strong);
            Assert.That(s.Frontal, Is.GreaterThan(s.Hold), "stronger: attack");
            var weak = Even;
            weak.Balance = 0.7f;
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
            Assert.That(CommanderUtility.Score(close, commander.ToSettings(), 0, CommanderTactic.FlankLeft).FlankLeft, Is.GreaterThan(0f), "but a flank under way goes on");
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
            beaten.Balance = 0.25f;
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
        public void Decide_APlanThatFadedToNothing_IsNotKept()
        {
            var settings = commander.ToSettings();
            var rng = new SimRandom(1);
            var scores = new CommanderScores { Frontal = 0.05f, Hold = -1f };
            Assert.That(CommanderUtility.Decide(ref scores, CommanderTactic.Retreat, 100000, settings, ref rng), Is.EqualTo(CommanderTactic.Frontal), "no bonus for a retreat at 0");
            var hopeless = Even;
            hopeless.Balance = 0.1f;
            hopeless.OwnMorale = 0.1f;
            Assert.That(Score(hopeless).Frontal, Is.GreaterThan(0f), "attacking stays possible");
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

        // ---- Lanchester ----

        static LanchesterSide Side(float soldiers, float worth, float front, float inFront) =>
            new LanchesterSide { Soldiers = soldiers, Strength = soldiers * worth, Front = front, FrontSoldiers = inFront };

        static LanchesterForecast Melee(in LanchesterSide own, in LanchesterSide enemy) => Lanchester.Melee(own, enemy, 0.5f, 0.06f);

        [Test]
        public void Lanchester_Even_IsAnEvenFight_ToTheLastMan()
        {
            var f = Melee(Side(20f, 1f, 10f, 10f), Side(20f, 1f, 10f, 10f));
            Assert.That(f.Balance, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(f.OwnLossShare, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(f.Seconds, Is.EqualTo(20f / (0.06f * 10f)).Within(0.01f), "20 soldiers at 10 x 0.06 per second");
        }

        [Test]
        public void Lanchester_IsLinear_DoubleTheSoldiers_DoubleThePower()
        {
            var f = Melee(Side(40f, 1f, 10f, 10f), Side(20f, 1f, 10f, 10f));
            Assert.That(f.Balance, Is.EqualTo(2f).Within(1e-5f), "not 4 as in the square law: the ranks behind wait");
            Assert.That(f.OwnLossShare, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(Melee(Side(20f, 1f, 10f, 10f), Side(40f, 1f, 10f, 10f)).Balance, Is.EqualTo(0.5f).Within(1e-5f), "and the other way round");
            Assert.That(Melee(Side(20f, 1f, 10f, 10f), Side(40f, 1f, 10f, 10f)).OwnLossShare, Is.EqualTo(1f), "the loser loses all");
        }

        [Test]
        public void Lanchester_WorthWidthAndDensity_AllCount()
        {
            Assert.That(Melee(Side(20f, 1.5f, 10f, 10f), Side(20f, 1f, 10f, 10f)).Balance, Is.EqualTo(1.5f).Within(1e-5f), "better soldiers");
            Assert.That(Melee(Side(20f, 1f, 8f, 7f), Side(20f, 1f, 11f, 10f)).Balance, Is.LessThan(0.95f), "the narrower line is flanked");
            Assert.That(Melee(Side(20f, 1f, 11f, 14f), Side(20f, 1f, 11f, 10f)).Balance, Is.EqualTo(1.4f).Within(1e-5f), "a shield wall as wide puts more soldiers on the front");
        }

        [Test]
        public void Lanchester_EmptySides_DoNotBreakIt()
        {
            Assert.That(Melee(default, Side(20f, 1f, 10f, 10f)).Balance, Is.Zero);
            Assert.That(Melee(Side(20f, 1f, 10f, 10f), default).Balance, Is.EqualTo(10f));
            var heroAlone = new LanchesterSide { Soldiers = 1f, Strength = 4f };
            Assert.That(Melee(heroAlone, Side(20f, 1f, 10f, 10f)).Balance, Is.GreaterThan(0f), "a hero without a formation still counts");
            Assert.That(float.IsFinite(Melee(heroAlone, heroAlone).Balance));
        }

        [Test]
        public void Worth_OwnLevelsCount_EnemyLevelsAreNotKnown_AHeroAlwaysShows()
        {
            var s = commander.ToSettings();
            var soldier = default(Shieldbreak.Sim.Agents.AgentData);
            Assert.That(CommanderUtility.Worth(soldier, 3, 1f, true, s), Is.EqualTo(1f));
            Assert.That(CommanderUtility.Worth(soldier, 5, 1f, true, s), Is.EqualTo(1.69f).Within(1e-4f));
            Assert.That(CommanderUtility.Worth(soldier, 1, 1f, true, s), Is.EqualTo(1f / 1.69f).Within(1e-4f));
            Assert.That(CommanderUtility.Worth(soldier, 5, 1f, false, s), Is.EqualTo(1f), "the enemy's level is not on the field to read");
            Assert.That(CommanderUtility.Worth(soldier, 3, 0.5f, true, s), Is.EqualTo(0.75f).Within(1e-5f), "a shaken soldier is worth less");
            Assert.That(CommanderUtility.Worth(soldier, 0, 0.2f, false, s), Is.EqualTo(commander.heroStrength), "a hero is a hero");
        }

        /// <summary>One formation each, both advance, no morale: who is left after the fight, by the forecast and in the sim.</summary>
        (float balance, float seconds, int ownWins, float fought) Fight(FormationType typeA, int filesA, FormationType typeB, int filesB)
        {
            scenario.commander = commander;
            scenario.morale.enabled = false;
            scenario.heroPosition = new Vector2(4f, 4f);
            scenario.units = new[]
            {
                new CompanyScenarioDefinition.Unit { name = "A", team = 1, formationType = typeA, count = 20, files = filesA, front = new Vector2(48f, 66f), faceTowards = new Vector2(48f, 30f), startBehavior = FormationBehavior.Advance },
                new CompanyScenarioDefinition.Unit { name = "B", team = 0, formationType = typeB, count = 20, files = filesB, front = new Vector2(48f, 40f), faceTowards = new Vector2(48f, 70f), startBehavior = FormationBehavior.Advance },
            };
            // The forecast comes from a commander, the fights run without one: it would make the weaker side hold
            float balance, seconds;
            using (var reader = World())
            {
                reader.Step();
                balance = reader.Commanders[0].Features.Balance;
                seconds = reader.Commanders[0].Features.ForecastSeconds;
            }
            scenario.commander = null;
            int wins = 0;
            float fought = 0f;
            for (int seed = 1; seed <= 6; seed++)
            {
                scenario.seed = seed;
                using var world = World();
                int contact = -1;
                int[] left = { 0, 0 };
                do
                {
                    world.Step();
                    foreach (var e in world.LastEvents) if (contact < 0 && e.Type == CombatEventType.Hit) contact = world.Tick;
                    left[0] = left[1] = 0;
                    for (int i = 1; i < world.AgentCount; i++) if (!world.Agents[i].IsDead) left[world.Agents[i].Team]++;
                }
                while (left[0] > 0 && left[1] > 0 && world.Tick < 300 * 60);
                if (left[1] > left[0]) wins++;
                fought += (world.Tick - contact) * SimConstants.DT / 6f;
            }
            return (balance, seconds, wins, fought);
        }

        [Test]
        public void Fairness_WhoSpawnsFirst_GivesNoAdvantage()
        {
            // Agents act one after the other; a fixed order gave the first-spawned side 5 soldiers of margin
            scenario.commander = null;
            scenario.morale.enabled = false;
            scenario.heroPosition = new Vector2(4f, 4f);
            float Margin(bool wallFirst)
            {
                var wall = new CompanyScenarioDefinition.Unit { name = "W", team = 1, formationType = FormationType.ShieldWall, count = 20, files = 14, front = new Vector2(48f, 66f), faceTowards = new Vector2(48f, 30f), startBehavior = FormationBehavior.Advance };
                var line = new CompanyScenarioDefinition.Unit { name = "L", team = 0, formationType = FormationType.Line, count = 20, files = 10, front = new Vector2(48f, 40f), faceTowards = new Vector2(48f, 70f), startBehavior = FormationBehavior.Advance };
                scenario.units = wallFirst ? new[] { wall, line } : new[] { line, wall };
                float margin = 0f;
                for (int seed = 1; seed <= 16; seed++)
                {
                    scenario.seed = seed;
                    using var world = World();
                    int[] alive = { 0, 0 };
                    do
                    {
                        world.Step();
                        alive[0] = alive[1] = 0;
                        for (int i = 1; i < world.AgentCount; i++) if (!world.Agents[i].IsDead) alive[world.Agents[i].Team]++;
                    }
                    while (alive[0] > 0 && alive[1] > 0 && world.Tick < 300 * 60);
                    margin += alive[1] - alive[0];
                }
                return margin / 16f;
            }
            float first = Margin(true), second = Margin(false);
            TestContext.WriteLine($"margin of the same wall: spawned first {first:0.0}, second {second:0.0}");
            Assert.That(math.abs(first - second), Is.LessThan(2f), "the same force wins by the same margin");
            Assert.That(first, Is.GreaterThan(4f), "and the wall still wins");
        }

        [Test]
        public void Forecast_PicksTheWinnerTheSimProduces_AndARoughDuration()
        {
            commander.heroStrength = 0.001f;    // the idle hero far away is not in this fight
            string line = "";
            void Check(string name, FormationType a, int filesA, FormationType b, int filesB)
            {
                var r = Fight(a, filesA, b, filesB);
                line += $"{name}: balance {r.balance:0.00}, A wins {r.ownWins}/6, forecast {r.seconds:0} s, fought {r.fought:0} s\n";
                // Near 1 the fight is open: only a clear forecast has to come true
                if (r.balance > 1.25f) Assert.That(r.ownWins, Is.GreaterThanOrEqualTo(4), $"{name}: {line}");
                if (r.balance < 0.8f) Assert.That(r.ownWins, Is.LessThanOrEqualTo(2), $"{name}: {line}");
                Assert.That(r.seconds, Is.InRange(r.fought * 0.5f, r.fought * 2f), $"{name}: {line}");
            }
            Check("wall 14 vs line 10", FormationType.ShieldWall, 14, FormationType.Line, 10);
            Check("line 7 vs line 10", FormationType.Line, 7, FormationType.Line, 10);
            Check("line 10 vs wall 14", FormationType.Line, 10, FormationType.ShieldWall, 14);
            Check("line 10 vs line 10", FormationType.Line, 10, FormationType.Line, 10);
            TestContext.WriteLine(line);
        }

        // ---- In the company ----

        static CommanderState State(SimWorld world) => world.Commanders[0];

        [Test]
        public void StateStructs_HaveNoPadding()
        {
            Assert.That(UnsafeUtility.SizeOf<CommanderFeatures>(), Is.EqualTo(100));
            Assert.That(UnsafeUtility.SizeOf<CommanderScores>(), Is.EqualTo(20));
            Assert.That(UnsafeUtility.SizeOf<CommanderState>(), Is.EqualTo(168));
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
            commander.heroStrength = 1f;    // even: the hero as one soldier
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
        public void Commander_CountsTheHero_AndTheFrontInItsForecast()
        {
            commander.heroStrength = 1f;
            float Balance()
            {
                using var world = World();
                world.Step();
                return State(world).Features.Balance;
            }
            float plain = Balance();
            commander.heroStrength = 5f;
            Assert.That(Balance(), Is.LessThan(plain), "a hero is worth more than a head");
            commander.heroStrength = 1f;
            scenario.units[2].files = 12;   // a thinner, wider enemy line against the same heads
            Assert.That(Balance(), Is.Not.EqualTo(plain), "the shape of the front counts, not only heads");
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
            scenario.units[2].count = 8;
            scenario.units[3].count = 8;
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
                // In contact on arrival it needs no reaction: its soldiers strike from where they stand
                foreach (var e in world.LastEvents)
                    if (e.Type == CombatEventType.AttackStart && e.Attacker >= 0 && world.FormationMembers[e.Attacker].Formation == flanker) attacked = true;
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
            f.Balance = 0.9f;
            var settings = commander.ToSettings();
            float frontal = CommanderUtility.Score(f, settings).Frontal;
            Assert.That(CommanderUtility.Score(f, settings, 0).Hold + settings.Hysteresis, Is.GreaterThan(frontal), "holding at first");
            Assert.That(CommanderUtility.Score(f, settings, 60 * 60).Hold + settings.Hysteresis, Is.LessThan(frontal), "not after a minute");
            var beaten = f;
            beaten.Balance = 0.3f;
            float retreat = CommanderUtility.Score(beaten, settings).Retreat;
            Assert.That(retreat, Is.GreaterThan(0.1f));
            Assert.That(CommanderUtility.Score(beaten, settings, 120 * 60).Retreat, Is.Zero, "a retreat does not last for ever either: the last few turn and fight");
        }

        [Test]
        public void Company_EverySeed_IsDecided_NobodyStandsAndStares()
        {
            commander.scoreNoise = 0.1f;
            int wins = 0;
            float firstRout = 0f, deadAtEnd = 0f, seconds = 0f;
            string line = "";
            for (int seed = 1; seed <= 12; seed++)
            {
                scenario.seed = seed;
                using var world = World();
                var result = BattleResult.Undecided;
                var dead = new int[2];
                var deadAtFirstRout = new[] { -1, -1 };
                while (result == BattleResult.Undecided && world.Tick < 240 * 60)
                {
                    world.Step();
                    foreach (var e in world.LastEvents)
                    {
                        int team = e.Target >= 0 ? world.Agents[e.Target].Team : 0;
                        if (e.Type == CombatEventType.Death) dead[team]++;
                        if (e.Type == CombatEventType.Rout && deadAtFirstRout[team] < 0) deadAtFirstRout[team] = dead[team];
                    }
                    result = BattleOutcome.Evaluate(world.Agents, 0, 0);
                }
                int loser = result == BattleResult.Victory ? 1 : 0;
                firstRout += deadAtFirstRout[loser] < 0 ? dead[loser] : deadAtFirstRout[loser];
                deadAtEnd += dead[loser];
                seconds += world.Tick / 60f;
                Assert.That(result, Is.Not.EqualTo(BattleResult.Undecided), $"seed {seed} after {world.Tick / 60} s, tactic {State(world).Tactic}");
                if (result == BattleResult.Victory) wins++;
                line += $" {result.ToString()[0]}{world.Tick / 60}";
            }
            TestContext.WriteLine($"idle hero wins {wins}/12:{line}; loser: first rout after {firstRout / 12f:0.0} of 24 dead, {deadAtEnd / 12f:0.0} dead at the end; {seconds / 12f:0} s");
        }

        [Test]
        public void Company_WithArchers_IsDecided_AndTheCommanderScreensThem()
        {
            var bow = ScriptableObject.CreateInstance<WeaponDefinition>();
            bow.ApplyBowDefaults();
            var shot = ScriptableObject.CreateInstance<AttackDefinition>();
            shot.ApplyDefaults(AttackDirection.Thrust);
            shot.windupMs = 900f;
            shot.releaseMs = 100f;
            shot.recoveryMs = 900f;
            shot.shootsProjectile = true;
            for (int d = 0; d < AttackDirection.Count; d++) bow.attacks[d] = shot;
            created.Add(bow);
            created.Add(shot);
            scenario.weapons = new[] { scenario.weapons[0], scenario.weapons[1], bow };
            CompanyScenarioDefinition.Unit Archers(int team, float y, float towards) => new CompanyScenarioDefinition.Unit
            {
                name = "S", team = team, formationType = FormationType.Loose, count = 8, files = 8, weapon = 2, sideWeapon = 0, botLevel = 3,
                front = new Vector2(48f, y), faceTowards = new Vector2(48f, towards), startBehavior = FormationBehavior.Screen,
            };
            var units = new List<CompanyScenarioDefinition.Unit>(scenario.units) { Archers(0, 23f, 70f), Archers(1, 73f, 30f) };
            scenario.units = units.ToArray();
            commander.scoreNoise = 0.1f;
            int wins = 0, shots = 0, deaths = 0;
            string line = "";
            for (int seed = 1; seed <= 12; seed++)
            {
                scenario.seed = seed;
                using var world = World();
                var result = BattleResult.Undecided;
                while (result == BattleResult.Undecided && world.Tick < 240 * 60)
                {
                    world.Step();
                    Assert.That(world.Formations[5].Behavior, Is.Not.EqualTo(FormationBehavior.Advance), "the commander never sends shooters into the line");
                    Assert.That(world.Commanders[0].Flanker, Is.Not.EqualTo(5));
                    foreach (var e in world.LastEvents)
                    {
                        if (e.Type == CombatEventType.Shot) shots++;
                        if (e.Type == CombatEventType.Death) deaths++;
                    }
                    result = BattleOutcome.Evaluate(world.Agents, 0, 0);
                }
                Assert.That(result, Is.Not.EqualTo(BattleResult.Undecided), $"seed {seed}");
                if (result == BattleResult.Victory) wins++;
                line += $" {result.ToString()[0]}{world.Tick / 60}";
            }
            Assert.That(shots, Is.GreaterThan(100));
            TestContext.WriteLine($"with archers: idle hero wins {wins}/12:{line}; {shots / 12} shots and {deaths / 12} dead per battle");
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
